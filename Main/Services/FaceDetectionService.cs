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

namespace Services
{
    public class FaceDetectionService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<FaceDetectionService> _logger;
        private readonly PythonFaceRecognitionClient _pythonClient;

        // Configurable thresholds
        private const double IoUThreshold = 0.25;      // overlap threshold to consider same face
        private const double EmbeddingDistanceThreshold = 0.6; // Euclidean distance threshold (lower = more similar)

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

            byte[] imageData = shot.FullScreen;

            if (imageData == null || imageData.Length == 0)
            {
                _logger.LogWarning($"Shot {shotId} has no fullscreen image data");
                return 0;
            }

            // Call Python service to detect faces and get encodings in one call (on fullscreen)
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
            string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "faces");
            if (!Directory.Exists(cacheDir)) Directory.CreateDirectory(cacheDir);

            string cachePath = Path.Combine(cacheDir, $"{faceDetectionId}.jpg");
            if (File.Exists(cachePath))
            {
                return await File.ReadAllBytesAsync(cachePath);
            }

            var detection = await _context.FaceDetections
                .Include(fd => fd.Shot)
                .FirstOrDefaultAsync(fd => fd.FaceDetectionId == faceDetectionId);

            if (detection == null || detection.Shot == null)
            {
                return null;
            }

            // Use only FullScreen image (no preview)
            byte[] imageData = detection.Shot.FullScreen;
            if (imageData == null || imageData.Length == 0)
            {
                _logger.LogWarning($"Shot {detection.Shot.ShotId} has no fullscreen image data for face extraction.");
                return null;
            }

            using var fullImage = Image.Load(imageData);

            // Use detection coordinates directly (already in FullScreen scale)
            int x = detection.X;
            int y = detection.Y;
            int width = detection.Width;
            int height = detection.Height;

            // Add padding (50%) to the face rectangle
            float paddingFactor = 0.5f;
            int padX = (int)(width * paddingFactor / 2);
            int padY = (int)(height * paddingFactor / 2);

            x -= padX;
            y -= padY;
            width += padX * 2;
            height += padY * 2;

            // Make square for consistent display
            int maxDim = Math.Max(width, height);
            int centerX = x + width / 2;
            int centerY = y + height / 2;

            x = centerX - maxDim / 2;
            y = centerY - maxDim / 2;
            width = maxDim;
            height = maxDim;

            // Clamp to image bounds
            x = Math.Max(0, x);
            y = Math.Max(0, y);
            if (x + width > fullImage.Width) width = fullImage.Width - x;
            if (y + height > fullImage.Height) height = fullImage.Height - y;

            if (width <= 0) width = 1;
            if (height <= 0) height = 1;

            var cropRect = new Rectangle(x, y, width, height);

            fullImage.Mutate(img => img.Crop(cropRect));

            // Resize to max 300x300 keeping aspect ratio
            int targetSize = 300;
            if (fullImage.Width > targetSize || fullImage.Height > targetSize)
            {
                double scale = Math.Min((double)targetSize / fullImage.Width, (double)targetSize / fullImage.Height);
                int newWidth = (int)(fullImage.Width * scale);
                int newHeight = (int)(fullImage.Height * scale);
                fullImage.Mutate(img => img.Resize(newWidth, newHeight));
            }

            using var ms = new MemoryStream();
            await fullImage.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 90 });
            var bytes = ms.ToArray();

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

        /// <summary>
        /// Robust: Re-run detection and try to extract encoding for a face detection.
        /// Matching: prefer IoU on same detection-image. fallback to embedding distance.
        /// Returns stored byte[] encoding or empty array when not found.
        /// </summary>
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

            if (detection?.Shot?.FullScreen == null || detection.Shot.FullScreen.Length == 0)
            {
                _logger.LogWarning($"Cannot extract encoding for face detection {faceDetectionId}: no fullscreen image");
                return new byte[0];
            }

            try
            {
                byte[] imageBytes = detection.Shot.FullScreen;

                using var detectionImage = Image.Load(imageBytes);

                // The stored detection coordinates are already in FullScreen scale, no scaling needed
                var detectionRect = new Rectangle(
                    detection.X,
                    detection.Y,
                    Math.Max(1, detection.Width),
                    Math.Max(1, detection.Height)
                );

                // Run python detection on fullscreen image bytes
                var response = await _pythonClient.DetectFacesAsync(imageBytes);
                if (response == null || response.Faces == null || response.Faces.Count == 0)
                {
                    _logger.LogWarning($"Re-detection found no faces for faceDetection {faceDetectionId}");
                    return new byte[0];
                }

                // Find best matching face by IoU (intersection over union)
                dynamic bestFace = null;
                double bestIoU = 0.0;

                foreach (var face in response.Faces)
                {
                    var faceRect = new Rectangle(face.Location.Left, face.Location.Top, face.Location.Width, face.Location.Height);
                    double iou = ComputeIoU(detectionRect, faceRect);
                    if (iou > bestIoU)
                    {
                        bestIoU = iou;
                        bestFace = face;
                    }
                }

                if (bestFace == null || bestIoU < IoUThreshold)
                {
                    _logger.LogWarning($"No suitable face found for faceDetection {faceDetectionId} (best IoU {bestIoU})");
                    return new byte[0];
                }

                if (bestFace.Encoding == null || bestFace.Encoding.Length != 128)
                {
                    _logger.LogWarning($"Best candidate embedding invalid for faceDetection {faceDetectionId}");
                    return new byte[0];
                }

                var encodingFloats = (float[])bestFace.Encoding;
                var byteEncoding = new byte[encodingFloats.Length * 4];
                Buffer.BlockCopy(encodingFloats, 0, byteEncoding, 0, byteEncoding.Length);

                var faceEncoding = new FaceEncoding
                {
                    FaceDetectionId = faceDetectionId,
                    Encoding = byteEncoding
                };
                _context.FaceEncodings.Add(faceEncoding);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Stored re-detected embedding for faceDetection {faceDetectionId}");
                return byteEncoding;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to extract encoding for face detection {faceDetectionId}");
            }

            return new byte[0];
        }

private double ComputeIoU(Rectangle a, Rectangle b)
            {
                int ax1 = a.X;
                int ay1 = a.Y;
                int ax2 = a.X + a.Width;
                int ay2 = a.Y + a.Height;

                int bx1 = b.X;
                int by1 = b.Y;
                int bx2 = b.X + b.Width;
                int by2 = b.Y + b.Height;

                int interLeft = Math.Max(ax1, bx1);
                int interTop = Math.Max(ay1, by1);
                int interRight = Math.Min(ax2, bx2);
                int interBottom = Math.Min(ay2, by2);

                int interWidth = interRight - interLeft;
                int interHeight = interBottom - interTop;

                if (interWidth <= 0 || interHeight <= 0) return 0.0;

                double interArea = interWidth * interHeight;
                double unionArea = a.Width * a.Height + b.Width * b.Height - interArea;

                if (unionArea <= 0) return 0.0;
                return interArea / unionArea;
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
    }
}
