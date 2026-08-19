using System.Globalization;
using System.Windows.Data;
using ProGlassAutomation.Converters;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DecimalTypingConverterTests
    {
        private readonly DecimalTypingConverter _c = new();

        [Theory]
        [InlineData("150.")]
        [InlineData("150,")]
        [InlineData(".")]
        [InlineData("-")]
        [InlineData("-.")]
        public void Incomplete_decimal_is_left_in_the_box(string typed)
        {
            object result = _c.ConvertBack(
                typed, typeof(decimal), null, CultureInfo.InvariantCulture);

            Assert.Same(Binding.DoNothing, result);
        }

        [Fact]
        public void Finished_decimal_parses()
        {
            object result = _c.ConvertBack(
                "150.51", typeof(decimal), null, CultureInfo.InvariantCulture);

            Assert.Equal(150.51m, result);
        }

        [Fact]
        public void Empty_clears_to_zero()
        {
            object result = _c.ConvertBack(
                "", typeof(decimal), null, CultureInfo.InvariantCulture);

            Assert.Equal(0m, result);
        }

        [Fact]
        public void Convert_hides_zero()
        {
            object result = _c.Convert(
                0m, typeof(string), null, CultureInfo.InvariantCulture);

            Assert.Equal(string.Empty, result);
        }

        [Fact]
        public void Convert_keeps_two_decimal_places_when_needed()
        {
            object result = _c.Convert(
                150.51m, typeof(string), null, CultureInfo.InvariantCulture);

            Assert.Equal("150.51", result);
        }
    }
}
