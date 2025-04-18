using OpenCvSharp;
using Xunit;

namespace Tests {

    public class CvTest {

        [Fact]
        public void LoadImage() {
            var mat = Cv2.ImRead("Resources\\IMG_20161210_184748.jpg");
            Assert.NotEmpty(mat.ToBytes());
        }
    }
}