using Xunit;
using System.IO;
using OpenCvSharp;
using System.ComponentModel.DataAnnotations;
using System;

namespace Tests {
    public class FaceDetectionExample {

        [Fact]
        public void DetectPersons() {

            var imagePathOne = "Resources\\PICT0097.JPG";
            var imagePathTwo = "Resources\\PICT0023.JPG";
            Assert.True(File.Exists(imagePathOne), $"Image file not found: {imagePathOne}");
            Assert.True(File.Exists(imagePathTwo), $"Image file not found: {imagePathTwo}");

            var imageOne = Cv2.ImRead(imagePathOne);
            var gray = new Mat();
            Cv2.CvtColor(imageOne, gray, ColorConversionCodes.BGR2GRAY);

            var cascadePath = "Resources\\haarcascade_frontalface_default.xml";
            Assert.True(File.Exists(cascadePath), $"Cascade file not found: {cascadePath}");
            var faceCascade = new CascadeClassifier(cascadePath);

            var facesOne = faceCascade.DetectMultiScale(gray, scaleFactor: 1.1, minNeighbors: 10, flags: HaarDetectionTypes.ScaleImage);
            Assert.True(facesOne.Length > 0);
            Console.WriteLine(facesOne.Length);


            foreach (var face in facesOne) {
                Cv2.Rectangle(imageOne, face, Scalar.Red, thickness: 3);
            }
            Cv2.NamedWindow("Detected Faces", WindowFlags.Normal); // Allow resizing
            Cv2.ResizeWindow("Detected Faces", imageOne.Width, imageOne.Height); // Force full size
            Cv2.ImShow("Detected Faces", imageOne);
            Cv2.WaitKey(0);


            var imageTwo = Cv2.ImRead(imagePathTwo);
            Cv2.CvtColor(imageTwo, gray, ColorConversionCodes.BGR2GRAY);

            var facesTwo = faceCascade.DetectMultiScale(gray, scaleFactor: 1.1, minNeighbors: 10, flags: HaarDetectionTypes.ScaleImage);
            Assert.True(facesTwo.Length > 0);
            Console.WriteLine(facesTwo.Length);


            foreach (var face in facesTwo) {
                Cv2.Rectangle(imageTwo, face, Scalar.Red, thickness: 3);
            }
            Cv2.NamedWindow("Detected Faces", WindowFlags.Normal); // Allow resizing
            Cv2.ResizeWindow("Detected Faces", imageTwo.Width, imageTwo.Height); // Force full size
            Cv2.ImShow("Detected Faces", imageTwo);
            Cv2.WaitKey(0);
            

        }


        // [Fact]
        // public void DetectFaces() {
        //     var imagePath = "Resources\\PICT1612.JPG";
        //     Assert.True(File.Exists(imagePath), $"Image file not found: {imagePath}");

        //     using var image = Cv2.ImRead(imagePath);
        //     using var gray = new Mat();
        //     Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

        //     var cascadePath = "Resources\\haarcascade_frontalface_default.xml";
        //     Assert.True(File.Exists(cascadePath), $"Cascade file not found: {cascadePath}");

        //     using var faceCascade = new CascadeClassifier(cascadePath);
        //     var faces = faceCascade.DetectMultiScale(gray, scaleFactor: 1.1, minNeighbors: 10, flags: HaarDetectionTypes.ScaleImage);

        //     foreach (var face in faces)
        //     {
        //         Cv2.Rectangle(image, face, Scalar.Red, thickness: 3);
        //     }

            // Cv2.NamedWindow("Detected Faces", WindowFlags.Normal); // Allow resizing
            // Cv2.ResizeWindow("Detected Faces", image.Width, image.Height); // Force full size
            // Cv2.ImShow("Detected Faces", image);
            // Cv2.WaitKey(0);

        // }
    }
}
