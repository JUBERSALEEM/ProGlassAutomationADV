using ProGlassAutomation.Services.Inquiry;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    /// <summary>
    /// S5 regression: inquiry-number generation parses the numeric suffix of
    /// existing numbers to compute the next sequence. Junk suffixes (manual
    /// imports, tampered data) must parse as 0 instead of throwing.
    /// </summary>
    public class InquirySequenceTests
    {
        [Theory]
        [InlineData("INQ-2026-0007", 7)]
        [InlineData("INQ-2026-1042", 1042)]
        [InlineData("INQ-2026-0", 0)]
        public void Parses_numeric_suffix(string number, int expected) =>
            Assert.Equal(expected, InquiryRepository.ParseSequenceSuffix(number, "INQ-2026-"));

        [Theory]
        [InlineData("INQ-2026-XXXX")]
        [InlineData("INQ-2026-")]
        [InlineData("")]
        public void Junk_suffixes_parse_as_zero(string number) =>
            Assert.Equal(0, InquiryRepository.ParseSequenceSuffix(number, "INQ-2026-"));
    }
}
