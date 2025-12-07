using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

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

    public async Task<List<Rect>> DetectFacesAsync(byte[] imageData)
    {
        var response = await _pythonClient.DetectFacesAsync(imageData);

        var rects = new List<Rect>();
        foreach (var face in response.Faces)
        {
            // Convert from top/left/bottom/right to x/y/width/height format
            var rect = new Rect(
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
        bool usingFullScreen = true;

        if (imageData == null || imageData.Length == 0)
        {
            usingFullScreen = false;
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
                using var previewMat = Mat.FromImageData(detection.Shot.Preview, ImreadModes.Color);
                var previewRect = new Rect(detection.X, detection.Y, detection.Width, detection.Height);
                previewRect = ClampRect(previewRect, previewMat.Width, previewMat.Height);
                using var previewFace = new Mat(previewMat, previewRect);
                return previewFace.ToBytes(".jpg");
            }
        }

        using var fullMat = Mat.FromImageData(imageData, ImreadModes.Color);

        // Get preview dimensions to calculate scale factor
        using var previewMatForScale = Mat.FromImageData(detection.Shot.Preview, ImreadModes.Color);
        double scaleX = (double)fullMat.Width / previewMatForScale.Width;
        double scaleY = (double)fullMat.Height / previewMatForScale.Height;

        // Scale face coordinates to full resolution
        var scaledRect = new Rect(
            (int)(detection.X * scaleX),
            (int)(detection.Y * scaleY),
            (int)(detection.Width * scaleX),
            (int)(detection.Height * scaleY)
        );

        // Add padding to the face rectangle (expand by 50%)
        float paddingFactor = 0.5f;
        int padX = (int)(scaledRect.Width * paddingFactor / 2);
        int padY = (int)(scaledRect.Height * paddingFactor / 2);

        scaledRect.X -= padX;
        scaledRect.Y -= padY;
        scaledRect.Width += (padX * 2);
        scaledRect.Height += (padY * 2);

        // Make square to ensure consistent display
        int maxDim = Math.Max(scaledRect.Width, scaledRect.Height);
        int centerX = scaledRect.X + scaledRect.Width / 2;
        int centerY = scaledRect.Y + scaledRect.Height / 2;

        scaledRect.X = centerX - maxDim / 2;
        scaledRect.Y = centerY - maxDim / 2;
        scaledRect.Width = maxDim;
        scaledRect.Height = maxDim;

        // Ensure rect is within image bounds
        // Shift to fit within bounds while maintaining square aspect ratio if possible
        if (scaledRect.X < 0) scaledRect.X = 0;
        if (scaledRect.Y < 0) scaledRect.Y = 0;
        if (scaledRect.Right > fullMat.Width) scaledRect.X = fullMat.Width - scaledRect.Width;
        if (scaledRect.Bottom > fullMat.Height) scaledRect.Y = fullMat.Height - scaledRect.Height;

        // Re-check left/top boundaries in case the previous shift pushed them out (if crop is larger than image)
        if (scaledRect.X < 0) scaledRect.X = 0;
        if (scaledRect.Y < 0) scaledRect.Y = 0;

        // Finally clamp size if it's still too big
        scaledRect = ClampRect(scaledRect, fullMat.Width, fullMat.Height);

        // Extract face region
        using var faceMat = new Mat(fullMat, scaledRect);

        // Resize to a reasonable size for web display (300x300 max while maintaining aspect ratio)
        int targetSize = 300;
        double scale = Math.Min((double)targetSize / faceMat.Width, (double)targetSize / faceMat.Height);
        if (scale < 1.0) // Only downscale, don't upscale small faces
        {
            int newWidth = (int)(faceMat.Width * scale);
            int newHeight = (int)(faceMat.Height * scale);
            using var resized = new Mat();
            Cv2.Resize(faceMat, resized, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Lanczos4);
            var bytes = resized.ToBytes(".jpg");
            await File.WriteAllBytesAsync(cachePath, bytes);
            return bytes;
        }

        var fullBytes = faceMat.ToBytes(".jpg");
        await File.WriteAllBytesAsync(cachePath, fullBytes);
        return fullBytes;
    }

    private Rect ClampRect(Rect rect, int maxWidth, int maxHeight)
    {
        rect.X = Math.Max(0, rect.X);
        rect.Y = Math.Max(0, rect.Y);
        if (rect.X + rect.Width > maxWidth) rect.Width = maxWidth - rect.X;
        if (rect.Y + rect.Height > maxHeight) rect.Height = maxHeight - rect.Y;
        return rect;
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

        using var mat = Mat.FromImageData(shot.Preview, ImreadModes.Color);
        int imgWidth = mat.Width;
        int imgHeight = mat.Height;

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
