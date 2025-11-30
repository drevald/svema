using Xunit;

namespace Tests
{
    public class BasicTests
    {
        [Fact]
        public void DummyMathTest()
        {
            int a = 2;
            int b = 3;
            int sum = a + b;

            Assert.Equal(5, sum); // ✅ This will pass
        }
    }
}
