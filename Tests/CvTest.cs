using OpenCvSharp;
using Xunit;

namespace Tests {

    public class CvTest {

        [Fact]
        public void LoadImage() {
            var mat = Cv2.ImRead("Resources\\PICT0023.jpg");
            Assert.NotEmpty(mat.ToBytes());
        }
    }
}