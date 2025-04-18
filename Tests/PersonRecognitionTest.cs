using Xunit;
using System.IO;
using OpenCvSharp;

namespace Tests
{
    public class FaceDetectionExample
    {
        [Fact]
        public void DetectFaces()
        {
            var imagePath = "Resources\\IMG_20161210_184748.jpg";
            Assert.True(File.Exists(imagePath), $"Image file not found: {imagePath}");

            using var image = Cv2.ImRead(imagePath);
            using var gray = new Mat();
            Cv2.CvtColor(image, gray, ColorConversionCodes.BGR2GRAY);

            var cascadePath = "Resources\\haarcascade_frontalface_default.xml";
            Assert.True(File.Exists(cascadePath), $"Cascade file not found: {cascadePath}");

            using var faceCascade = new CascadeClassifier(cascadePath);
            var faces = faceCascade.DetectMultiScale(gray, scaleFactor: 1.1, minNeighbors: 10, flags: HaarDetectionTypes.ScaleImage);

            foreach (var face in faces)
            {
                Cv2.Rectangle(image, face, Scalar.Red, thickness: 3);
            }

            Cv2.ImShow("Detected Faces", image);
            Cv2.WaitKey(0);

        }
    }
}
