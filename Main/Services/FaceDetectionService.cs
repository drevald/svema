using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Services;

public class FaceDetectionService
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<FaceDetectionService> _logger;
    private readonly string _cascadePath;
    private readonly string _modelPath;
    private InferenceSession _session;
    private readonly object _sessionLock = new object();

    public FaceDetectionService(ApplicationDbContext context, ILogger<FaceDetectionService> logger)
    {
        _context = context;
        _logger = logger;
        // Assuming the cascade file is copied to the output directory or available in Resources
        _cascadePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "haarcascade_frontalface_default.xml");
        _modelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "face_recognition_model.onnx");
    }

    private InferenceSession GetOrCreateSession()
    {
        if (_session != null)
            return _session;

        lock (_sessionLock)
        {
            if (_session != null)
                return _session;

            if (File.Exists(_modelPath))
            {
                try
                {
                    _session = new InferenceSession(_modelPath);
                    _logger.LogInformation($"Loaded face recognition model from {_modelPath}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load face recognition model.");
                }
            }
            else
            {
                _logger.LogWarning($"Face recognition model not found at {_modelPath}");
            }

            return _session;
        }
    }

    public async Task<List<Rect>> DetectFacesAsync(byte[] imageData)
    {
        if (!File.Exists(_cascadePath))
        {
            _logger.LogError($"Haar cascade file not found at {_cascadePath}");
            return new List<Rect>();
        }

        using var mat = Mat.FromImageData(imageData, ImreadModes.Color);
        using var gray = new Mat();
        Cv2.CvtColor(mat, gray, ColorConversionCodes.BGR2GRAY);

        using var faceCascade = new CascadeClassifier(_cascadePath);

        // Calculate max size as 80% of the smaller dimension to avoid detecting the whole image as a face
        int maxDimension = (int)(Math.Min(mat.Width, mat.Height) * 0.8);

        var faces = faceCascade.DetectMultiScale(
            gray,
            scaleFactor: 1.1,
            minNeighbors: 6,  // Increased from 5 to reduce false positives
            flags: HaarDetectionTypes.ScaleImage,
            minSize: new Size(30, 30),
            maxSize: new Size(maxDimension, maxDimension)
        );

        // Filter out detections that are suspiciously large (> 50% of image area)
        var imageArea = mat.Width * mat.Height;
        var validFaces = faces.Where(face =>
        {
            var faceArea = face.Width * face.Height;
            var areaRatio = (double)faceArea / imageArea;
            return areaRatio < 0.5; // Reject if face covers more than 50% of image
        }).ToList();

        if (validFaces.Count < faces.Length)
        {
            _logger.LogInformation($"Filtered out {faces.Length - validFaces.Count} suspicious face detection(s) (too large)");
        }

        return validFaces;
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

        var faces = await DetectFacesAsync(imageData);

        _logger.LogInformation($"Shot {shotId}: Detected {faces.Count} face(s).");

        if (faces.Count == 0)
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

        using var mat = Mat.FromImageData(imageData, ImreadModes.Color);
        var session = GetOrCreateSession();

        foreach (var face in faces)
        {
            var detection = new FaceDetection
            {
                ShotId = shotId,
                X = face.X,
                Y = face.Y,
                Width = face.Width,
                Height = face.Height,
                DetectedAt = DateTime.UtcNow,
                IsConfirmed = false
            };
            _context.FaceDetections.Add(detection);
            await _context.SaveChangesAsync(); // Save to get ID

            if (session != null)
            {
                try
                {
                    // Extract face crop
                    var faceRect = face;
                    // Ensure rect is within image bounds
                    faceRect.X = Math.Max(0, faceRect.X);
                    faceRect.Y = Math.Max(0, faceRect.Y);
                    if (faceRect.X + faceRect.Width > mat.Width) faceRect.Width = mat.Width - faceRect.X;
                    if (faceRect.Y + faceRect.Height > mat.Height) faceRect.Height = mat.Height - faceRect.Y;

                    using var faceMat = new Mat(mat, faceRect);

                    // Preprocess for ArcFace (112x112, [-1, 1])

                    // 1. Resize to 112x112
                    using var resized = new Mat();
                    Cv2.Resize(faceMat, resized, new Size(112, 112));

                    // 2. Convert to float32
                    using var floatMat = new Mat();
                    resized.ConvertTo(floatMat, MatType.CV_32FC3);

                    // 3. Normalize: (pixel - 127.5) / 127.5 -> [-1, 1]
                    floatMat.ConvertTo(floatMat, -1, 1.0 / 127.5, -1.0);

                    // 4. Convert BGR -> RGB
                    Cv2.CvtColor(floatMat, floatMat, ColorConversionCodes.BGR2RGB);

                    // 5. Create NCHW tensor (1, 3, 112, 112)
                    var tensor = new DenseTensor<float>(new[] { 1, 3, 112, 112 });

                    // Split channels and copy to tensor
                    var channels = Cv2.Split(floatMat);
                    for (int c = 0; c < 3; c++)
                    {
                        var channelData = new float[112 * 112];
                        Marshal.Copy(channels[c].Data, channelData, 0, channelData.Length);

                        for (int h = 0; h < 112; h++)
                        {
                            for (int w = 0; w < 112; w++)
                            {
                                tensor[0, c, h, w] = channelData[h * 112 + w];
                            }
                        }
                        channels[c].Dispose();
                    }

                    // 6. Run inference
                    var inputs = new List<NamedOnnxValue>
                    {
                        NamedOnnxValue.CreateFromTensor("data", tensor)
                    };

                    using var results = session.Run(inputs);
                    var output = results.FirstOrDefault()?.AsEnumerable<float>().ToArray();

                    if (output != null && output.Length == 512)
                    {
                        // Convert to byte array for storage
                        var byteEncoding = new byte[output.Length * 4];
                        Buffer.BlockCopy(output, 0, byteEncoding, 0, byteEncoding.Length);

                        var faceEncoding = new FaceEncoding
                        {
                            FaceDetectionId = detection.FaceDetectionId,
                            Encoding = byteEncoding
                        };
                        _context.FaceEncodings.Add(faceEncoding);

                        _logger.LogDebug($"Generated 512D embedding for face detection {detection.FaceDetectionId}");
                    }
                    else
                    {
                        _logger.LogWarning($"Unexpected output dimension: {output?.Length ?? 0}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to extract encoding for face detection {detection.FaceDetectionId}");
                }
            }
        }

        shot.IsFaceProcessed = true;
        await _context.SaveChangesAsync();
        return faces.Count;
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

        // If no encoding exists, try to generate it
        if (detection?.Shot?.Preview == null)
        {
            return new byte[0];
        }

        var session = GetOrCreateSession();
        if (session == null)
        {
            return new byte[0];
        }

        try
        {
            using var mat = Mat.FromImageData(detection.Shot.Preview, ImreadModes.Color);
            var faceRect = new Rect(detection.X, detection.Y, detection.Width, detection.Height);

            // Ensure rect is within image bounds
            faceRect.X = Math.Max(0, faceRect.X);
            faceRect.Y = Math.Max(0, faceRect.Y);
            if (faceRect.X + faceRect.Width > mat.Width) faceRect.Width = mat.Width - faceRect.X;
            if (faceRect.Y + faceRect.Height > mat.Height) faceRect.Height = mat.Height - faceRect.Y;

            using var faceMat = new Mat(mat, faceRect);

            // Preprocess and run inference (same as above)
            using var resized = new Mat();
            Cv2.Resize(faceMat, resized, new Size(112, 112));

            using var floatMat = new Mat();
            resized.ConvertTo(floatMat, MatType.CV_32FC3);
            floatMat.ConvertTo(floatMat, -1, 1.0 / 127.5, -1.0);
            Cv2.CvtColor(floatMat, floatMat, ColorConversionCodes.BGR2RGB);

            var tensor = new DenseTensor<float>(new[] { 1, 3, 112, 112 });
            var channels = Cv2.Split(floatMat);

            for (int c = 0; c < 3; c++)
            {
                var channelData = new float[112 * 112];
                Marshal.Copy(channels[c].Data, channelData, 0, channelData.Length);

                for (int h = 0; h < 112; h++)
                {
                    for (int w = 0; w < 112; w++)
                    {
                        tensor[0, c, h, w] = channelData[h * 112 + w];
                    }
                }
                channels[c].Dispose();
            }

            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor("data", tensor)
            };

            using var results = session.Run(inputs);
            var output = results.FirstOrDefault()?.AsEnumerable<float>().ToArray();

            if (output != null && output.Length == 512)
            {
                var byteEncoding = new byte[output.Length * 4];
                Buffer.BlockCopy(output, 0, byteEncoding, 0, byteEncoding.Length);
                return byteEncoding;
            }
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
