// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/PhoneNumbersTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins wa.me number normalization: trunk-0 swap, explicit
// international verbatim (+/00), long-numbers-verbatim, default-code
// prepend, and the 7–15 digit validity gate.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Sharing.Contacts;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class PhoneNumbersTests
    {
        [Theory]
        [InlineData("050 123 4567", "971501234567")]
        [InlineData("050-123-4567", "971501234567")]
        [InlineData("(050) 1234567", "971501234567")]
        public void Normalize_swaps_trunk_zero_for_country_code(
            string raw, string expected)
        {
            Assert.Equal(expected, PhoneNumbers.Normalize(raw));
        }

        [Theory]
        [InlineData("+971501234567", "971501234567")]
        [InlineData("+971 50 123 4567", "971501234567")]
        [InlineData("00971501234567", "971501234567")]
        [InlineData("+1 415 555 2671", "14155552671")]
        public void Normalize_keeps_explicit_international_verbatim(
            string raw, string expected)
        {
            Assert.Equal(expected, PhoneNumbers.Normalize(raw));
        }

        // ── Stage 3 hardening: country-code + trunk-0 collision ──────────
        // These were the numbers that made WhatsApp open then close — the
        // trunk 0 wedged after the country code produced an invalid number.
        [Theory]
        [InlineData("+971 050 123 4567", "971501234567")]  // + code + trunk 0
        [InlineData("00971 050 1234567", "971501234567")]  // 00 code + trunk 0
        [InlineData("971 050 1234567", "971501234567")]    // bare code + trunk 0
        [InlineData("971 50 123 4567", "971501234567")]    // bare code, no trunk
        [InlineData("+1 0 415 555 2671", "14155552671")]   // intl code + trunk 0
        public void Normalize_drops_trunk_zero_after_country_code(
            string raw, string expected)
        {
            Assert.Equal(expected, PhoneNumbers.Normalize(raw));
        }

        [Theory]
        [InlineData("050-1234567 / 04-2345678", "971501234567")] // two numbers
        [InlineData("Mr Ali 050 123 4567", "971501234567")]      // name prefix
        [InlineData("050 123 4567 ext 12", "97150123456712")]    // extension digits appended (still dialable)
        public void Normalize_reads_number_from_messy_fields(
            string raw, string expected)
        {
            Assert.Equal(expected, PhoneNumbers.Normalize(raw));
        }

        [Fact]
        public void Normalize_keeps_long_unprefixed_verbatim()
        {
            Assert.Equal("971501234567",
                PhoneNumbers.Normalize("971501234567"));
        }

        [Fact]
        public void Normalize_prepends_code_to_short_local()
        {
            Assert.Equal("9715012345",
                PhoneNumbers.Normalize("5012345"));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("no digits here")]
        [InlineData("0000")]
        public void Normalize_returns_empty_without_usable_digits(string? raw)
        {
            Assert.Equal("", PhoneNumbers.Normalize(raw));
        }

        [Theory]
        [InlineData("971501234567", true)]
        [InlineData("12345678", true)]              // 8 digits — minimum
        [InlineData("123456789012345", true)]       // 15 digits — maximum
        [InlineData("1234567", false)]              // 7 digits — too short now
        [InlineData("1234567890123456", false)]     // 16 digits — too long
        [InlineData("97150A234567", false)]         // contains a letter
        [InlineData("", false)]
        [InlineData(null, false)]
        public void IsValidNormalized_enforces_8_to_15_digits(
            string? normalized, bool expected)
        {
            Assert.Equal(expected,
                PhoneNumbers.IsValidNormalized(normalized));
        }

        [Fact]
        public void NormalizeValidated_returns_digits_or_empty()
        {
            Assert.Equal("971501234567",
                PhoneNumbers.NormalizeValidated("050 123 4567"));
            Assert.Equal("",
                PhoneNumbers.NormalizeValidated("123"));
        }
    }
}
