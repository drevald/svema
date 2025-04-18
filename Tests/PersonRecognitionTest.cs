using Xunit;
using System;
using System.IO;
using System.Collections.Generic;
using OpenCvSharp;
using DlibDotNet;
using DlibDotNet.Extensions;
using DlibDotNet.Dnn;

namespace Tests
{
    public class PersonRecognitionTest
    {
        [Fact]
        public void DetectAndEncodeFaces()
        {
            // Load image using OpenCvSharp
            var imagePath = "Resources\\PICT1612.JPG";
            Assert.True(File.Exists(imagePath), $"Image not found: {imagePath}");
            var image = Cv2.ImRead(imagePath);

            // Convert to grayscale for face detection
            var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            // Load face detector models
            var shapePredictorPath = "Resources\\shape_predictor_68_face_landmarks.dat";
            var faceRecognitionModelPath = "Resources\\dlib_face_recognition_resnet_model_v1.dat";

            Assert.True(File.Exists(shapePredictorPath));
            Assert.True(File.Exists(faceRecognitionModelPath));

            using var detector = Dlib.GetFrontalFaceDetector();
            using var shapePredictor = ShapePredictor.Deserialize(shapePredictorPath);
            using var faceRecognitionModel = DlibDotNet.Dnn.FaceRecognitionModelV1.Deserialize(faceRecognitionModelPath);

            // Convert OpenCV image to Dlib image
            using var dlibImage = Dlib.LoadImage<RgbPixel>(imagePath);

            // Detect faces
            var faces = detector.Operator(dlibImage);

            var descriptors = new List<Matrix<float>>();

            foreach (var rect in faces)
            {
                var shape = shapePredictor.Detect(dlibImage, rect);
                var descriptor = faceRecognitionModel.ComputeFaceDescriptor(dlibImage, shape);
                descriptors.Add(descriptor);
            }

            Assert.True(descriptors.Count > 0, "No face descriptors found.");
        }
    }
}