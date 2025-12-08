using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Services;

public class FaceDetectionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FaceDetectionService> _logger;
    private readonly PythonFaceRecognitionClient _pythonClient;

    public FaceDetectionService(
        ApplicationDbContext context,
        ILogger<FaceDetectionService> logger,
        PythonFaceRecognitionClient pythonClient)
    {
        _context = context;
        _logger = logger;
        _pythonClient = pythonClient;
    }

    public async Task<List<FaceRectangle>> DetectFacesAsync(byte[] imageData)
    {
        var response = await _pythonClient.DetectFacesAsync(imageData);

        var rects = new List<FaceRectangle>();
        foreach (var face in response.Faces)
        {
            // Convert from top/left/bottom/right to x/y/width/height format
            var rect = new FaceRectangle(
                face.Location.Left,
                face.Location.Top,
                face.Location.Width,
                face.Location.Height
            );
            rects.Add(rect);
        }

        _logger.LogInformation($"Python service detected {rects.Count} face(s)");
        return rects;
    }

    public async Task<int> DetectAndStoreFacesAsync(int shotId)
    {
        var shot = await _context.Shots.FindAsync(shotId);
        if (shot == null)
        {
            _logger.LogWarning($"Shot {shotId} not found");
            return 0;
        }

        byte[] imageData = shot.Preview;

        if (imageData == null || imageData.Length == 0)
        {
            _logger.LogWarning($"Shot {shotId} has no preview image data");
            return 0;
        }

        // Call Python service to detect faces and get encodings in one call
        var response = await _pythonClient.DetectFacesAsync(imageData);

        _logger.LogInformation($"Shot {shotId}: Detected {response.Count} face(s).");

        if (response.Count == 0)
        {
            shot.IsFaceProcessed = true;
            await _context.SaveChangesAsync();
            return 0;
        }

        var existingDetections = await _context.FaceDetections
            .Where(fd => fd.ShotId == shotId)
            .ToListAsync();

        _context.FaceDetections.RemoveRange(existingDetections);
        await _context.SaveChangesAsync(); // Save removal first

        foreach (var face in response.Faces)
        {
            var detection = new FaceDetection
            {
                ShotId = shotId,
                X = face.Location.Left,
                Y = face.Location.Top,
                Width = face.Location.Width,
                Height = face.Location.Height,
                DetectedAt = DateTime.UtcNow,
                IsConfirmed = false
            };
            _context.FaceDetections.Add(detection);
            await _context.SaveChangesAsync(); // Save to get ID

            if (face.Encoding != null && face.Encoding.Length == 128)
            {
                try
                {
                    // Convert float[] to byte array for storage (128 floats = 512 bytes)
                    var byteEncoding = new byte[face.Encoding.Length * 4];
                    Buffer.BlockCopy(face.Encoding, 0, byteEncoding, 0, byteEncoding.Length);

                    var faceEncoding = new FaceEncoding
                    {
                        FaceDetectionId = detection.FaceDetectionId,
                        Encoding = byteEncoding
                    };
                    _context.FaceEncodings.Add(faceEncoding);

                    _logger.LogDebug($"Generated 128D embedding for face detection {detection.FaceDetectionId}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to store encoding for face detection {detection.FaceDetectionId}");
                }
            }
        }

        shot.IsFaceProcessed = true;
        await _context.SaveChangesAsync();
        return response.Count;
    }

    public async Task<byte[]> GetFaceImageAsync(int faceDetectionId)
    {
        // Check cache first
        string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "faces");
        if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

        string cachePath = Path.Combine(cacheDir, $"{faceDetectionId}.jpg");
        if (File.Exists(cachePath))
        {
            return await File.ReadAllBytesAsync(cachePath);
        }

        var detection = await _context.FaceDetections
            .Include(fd => fd.Shot)
            .ThenInclude(s => s.Storage)
            .FirstOrDefaultAsync(fd => fd.FaceDetectionId == faceDetectionId);

        if (detection == null || detection.Shot == null)
        {
            return null;
        }

        // Try to use FullScreen image first (faster than original, better than preview)
        byte[] imageData = detection.Shot.FullScreen;

        if (imageData == null || imageData.Length == 0)
        {
            // Fallback to original if FullScreen is not available
            var originalStream = await Storage.GetFile(detection.Shot);
            if (originalStream != null)
            {
                using var memStream = new MemoryStream();
                await originalStream.CopyToAsync(memStream);
                imageData = memStream.ToArray();
            }
            else
            {
                _logger.LogWarning($"Could not load original file for shot {detection.Shot.ShotId}, falling back to preview");
                // Fallback to preview if original is not available
                if (detection.Shot.Preview == null) return null;

                using var previewImage = Image.Load(detection.Shot.Preview);
                var rect = new Rectangle(detection.X, detection.Y, detection.Width, detection.Height);
                rect = ClampRect(rect, previewImage.Width, previewImage.Height);

                previewImage.Mutate(x => x.Crop(rect));

                using var ms = new MemoryStream();
                await previewImage.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 });
                return ms.ToArray();
            }
        }

        using var fullImage = Image.Load(imageData);

        // Get preview dimensions to calculate scale factor
        using var previewImageForScale = Image.Load(detection.Shot.Preview);
        double scaleX = (double)fullImage.Width / previewImageForScale.Width;
        double scaleY = (double)fullImage.Height / previewImageForScale.Height;

        // Scale face coordinates to full resolution
        int scaledX = (int)(detection.X * scaleX);
        int scaledY = (int)(detection.Y * scaleY);
        int scaledWidth = (int)(detection.Width * scaleX);
        int scaledHeight = (int)(detection.Height * scaleY);

        // Add padding to the face rectangle (expand by 50%)
        float paddingFactor = 0.5f;
        int padX = (int)(scaledWidth * paddingFactor / 2);
        int padY = (int)(scaledHeight * paddingFactor / 2);

        scaledX -= padX;
        scaledY -= padY;
        scaledWidth += (padX * 2);
        scaledHeight += (padY * 2);

        // Make square to ensure consistent display
        int maxDim = Math.Max(scaledWidth, scaledHeight);
        int centerX = scaledX + scaledWidth / 2;
        int centerY = scaledY + scaledHeight / 2;

        scaledX = centerX - maxDim / 2;
        scaledY = centerY - maxDim / 2;
        scaledWidth = maxDim;
        scaledHeight = maxDim;

        // Ensure rect is within image bounds
        if (scaledX < 0) scaledX = 0;
        if (scaledY < 0) scaledY = 0;
        if (scaledX + scaledWidth > fullImage.Width) scaledX = fullImage.Width - scaledWidth;
        if (scaledY + scaledHeight > fullImage.Height) scaledY = fullImage.Height - scaledHeight;

        // Re-check boundaries in case shift pushed them out
        if (scaledX < 0) scaledX = 0;
        if (scaledY < 0) scaledY = 0;

        // Finally clamp size if it's still too big
        var finalRect = ClampRect(new Rectangle(scaledX, scaledY, scaledWidth, scaledHeight), fullImage.Width, fullImage.Height);

        // Extract face region
        fullImage.Mutate(x => x.Crop(finalRect));

        // Resize to a reasonable size for web display (300x300 max while maintaining aspect ratio)
        int targetSize = 300;
        if (fullImage.Width > targetSize || fullImage.Height > targetSize)
        {
            double scale = Math.Min((double)targetSize / fullImage.Width, (double)targetSize / fullImage.Height);
            int newWidth = (int)(fullImage.Width * scale);
            int newHeight = (int)(fullImage.Height * scale);

            fullImage.Mutate(x => x.Resize(newWidth, newHeight));
        }

        using var outputStream = new MemoryStream();
        await fullImage.SaveAsJpegAsync(outputStream, new JpegEncoder { Quality = 90 });
        var bytes = outputStream.ToArray();

        await File.WriteAllBytesAsync(cachePath, bytes);
        return bytes;
    }

    private Rectangle ClampRect(Rectangle rect, int maxWidth, int maxHeight)
    {
        int x = Math.Max(0, rect.X);
        int y = Math.Max(0, rect.Y);
        int width = rect.Width;
        int height = rect.Height;

        if (x + width > maxWidth) width = maxWidth - x;
        if (y + height > maxHeight) height = maxHeight - y;

        return new Rectangle(x, y, width, height);
    }

    public async Task<byte[]> ExtractFaceEncodingAsync(int faceDetectionId)
    {
        var detection = await _context.FaceDetections
            .Include(fd => fd.Shot)
            .Include(fd => fd.FaceEncoding)
            .FirstOrDefaultAsync(fd => fd.FaceDetectionId == faceDetectionId);

        if (detection?.FaceEncoding != null)
        {
            return detection.FaceEncoding.Encoding;
        }

        // If no encoding exists, re-run detection on this shot
        if (detection?.Shot?.Preview == null)
        {
            _logger.LogWarning($"Cannot extract encoding for face detection {faceDetectionId}: no preview image");
            return new byte[0];
        }

        try
        {
            // Re-detect faces to get encoding
            var response = await _pythonClient.DetectFacesAsync(detection.Shot.Preview);

            // Try to match the face by location
            foreach (var face in response.Faces)
            {
                // Check if this face roughly matches our detection coordinates
                int deltaX = Math.Abs(face.Location.Left - detection.X);
                int deltaY = Math.Abs(face.Location.Top - detection.Y);

                if (deltaX < 20 && deltaY < 20 && face.Encoding != null && face.Encoding.Length == 128)
                {
                    // Convert float[] to byte array
                    var byteEncoding = new byte[face.Encoding.Length * 4];
                    Buffer.BlockCopy(face.Encoding, 0, byteEncoding, 0, byteEncoding.Length);

                    // Store it for future use
                    var faceEncoding = new FaceEncoding
                    {
                        FaceDetectionId = faceDetectionId,
                        Encoding = byteEncoding
                    };
                    _context.FaceEncodings.Add(faceEncoding);
                    await _context.SaveChangesAsync();

                    return byteEncoding;
                }
            }

            _logger.LogWarning($"Could not find matching face in re-detection for face detection {faceDetectionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to extract encoding for face detection {faceDetectionId}");
        }

        return new byte[0];
    }
    public async Task<List<FaceDetectionResult>> GetAdjustedFaceDetectionsAsync(int shotId)
    {
        var shot = await _context.Shots.FindAsync(shotId);
        if (shot == null || shot.Preview == null) return new List<FaceDetectionResult>();

        var faces = await _context.FaceDetections
            .Where(fd => fd.ShotId == shotId)
            .Include(fd => fd.Person)
            .ToListAsync();

        if (!faces.Any()) return new List<FaceDetectionResult>();

        using var image = Image.Load(shot.Preview);
        int imgWidth = image.Width;
        int imgHeight = image.Height;

        var results = new List<FaceDetectionResult>();

        foreach (var face in faces)
        {
            // Start with original coordinates
            int x = face.X;
            int y = face.Y;
            int w = face.Width;
            int h = face.Height;

            // Apply Flip (Horizontal)
            if (shot.Flip)
            {
                x = imgWidth - x - w;
            }

            // Apply Rotate (Clockwise)
            int rotatedX = x;
            int rotatedY = y;
            int rotatedW = w;
            int rotatedH = h;
            int rotatedImgWidth = imgWidth;
            int rotatedImgHeight = imgHeight;

            switch (shot.Rotate)
            {
                case 90:
                    rotatedX = imgHeight - y - h;
                    rotatedY = x;
                    rotatedW = h;
                    rotatedH = w;
                    rotatedImgWidth = imgHeight;
                    rotatedImgHeight = imgWidth;
                    break;
                case 180:
                    rotatedX = imgWidth - x - w;
                    rotatedY = imgHeight - y - h;
                    rotatedImgWidth = imgWidth;
                    rotatedImgHeight = imgHeight;
                    break;
                case 270:
                    rotatedX = y;
                    rotatedY = imgWidth - x - w;
                    rotatedW = h;
                    rotatedH = w;
                    rotatedImgWidth = imgHeight;
                    rotatedImgHeight = imgWidth;
                    break;
            }

            results.Add(new FaceDetectionResult
            {
                FaceDetectionId = face.FaceDetectionId,
                X = (float)rotatedX / rotatedImgWidth,
                Y = (float)rotatedY / rotatedImgHeight,
                Width = (float)rotatedW / rotatedImgWidth,
                Height = (float)rotatedH / rotatedImgHeight,
                PersonId = face.PersonId,
                PersonName = face.Person != null ? $"{face.Person.FirstName} {face.Person.LastName}" : null
            });
        }

        return results;
    }
}

public class FaceDetectionResult
{
    public int FaceDetectionId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public int? PersonId { get; set; }
    public string PersonName { get; set; }
}

public class FaceRectangle
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }

    public FaceRectangle(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
