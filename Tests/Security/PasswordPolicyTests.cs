// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Security/PasswordPolicyTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Security — password acceptance policy
//
// Locks the PasswordPolicy contract: minimum 6, NO maximum, known-bad
// rejections (blocklist, repeats, runs, identity), and strength scoring
// that guides without rejecting. The policy governs only passwords being
// chosen now — nothing here can lock out an existing user.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Security;
using Xunit;

namespace ProGlassAutomation.Tests.Security
{
    public sealed class PasswordPolicyTests
    {
        // ── Floor: 6, no ceiling ─────────────────────────────────────

        [Fact]
        public void Five_characters_are_rejected()
        {
            var (accepted, error) = PasswordPolicy.ValidateNewPassword("abc12");
            Assert.False(accepted);
            Assert.Contains("6", error);
        }

        [Fact]
        public void Six_characters_pass_the_length_floor()
        {
            var (accepted, _) = PasswordPolicy.ValidateNewPassword("k7#mQ2");
            Assert.True(accepted);
        }

        [Fact]
        public void There_is_no_maximum_length()
        {
            // 200 characters of non-trivial content must be accepted — the
            // operator is free to use as many characters as they want.
            string longPw = "Correct-Horse-9-Battery-Staple-Glass-Factory-007-"
                + "N0-maximum-length-here-at-all-*&^%$#@!~`[]{}|:;<>,.?/"
                + "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            var (accepted, _) = PasswordPolicy.ValidateNewPassword(longPw);
            Assert.True(accepted);
        }

        [Fact]
        public void Blank_passwords_are_rejected()
        {
            Assert.False(PasswordPolicy.ValidateNewPassword("").Accepted);
            Assert.False(PasswordPolicy.ValidateNewPassword("      ").Accepted);
            Assert.False(PasswordPolicy.ValidateNewPassword(null).Accepted);
        }

        // ── Known-bad rejections ─────────────────────────────────────

        [Theory]
        [InlineData("password")]
        [InlineData("Password")]
        [InlineData("PASSWORD")]
        [InlineData("password1")]
        [InlineData("password123")]
        [InlineData("admin123")]
        [InlineData("qwerty")]
        [InlineData("letmein")]
        [InlineData("123456")]
        [InlineData("proglass")]
        [InlineData("glass123")]
        [InlineData("dubai123")]
        public void Blocklisted_passwords_are_rejected(string candidate)
        {
            Assert.False(PasswordPolicy.ValidateNewPassword(candidate).Accepted);
            Assert.True(PasswordPolicy.IsBlocked(candidate));
        }

        [Theory]
        [InlineData("aaaaaa")]
        [InlineData("111111")]
        [InlineData("........")]
        public void Single_character_repeats_are_rejected(string candidate)
        {
            Assert.False(PasswordPolicy.ValidateNewPassword(candidate).Accepted);
        }

        [Theory]
        [InlineData("abcdef")]
        [InlineData("fedcba")]
        [InlineData("654321")]
        public void Simple_sequences_are_rejected(string candidate)
        {
            Assert.False(PasswordPolicy.ValidateNewPassword(candidate).Accepted);
        }

        [Fact]
        public void Password_equal_to_username_is_rejected()
        {
            Assert.False(
                PasswordPolicy.ValidateNewPassword("juberops", "JuberOps").Accepted);
        }

        [Fact]
        public void Password_containing_a_long_username_is_rejected()
        {
            Assert.False(
                PasswordPolicy.ValidateNewPassword("juber123!", "juber").Accepted);
        }

        [Fact]
        public void Short_username_inside_an_ordinary_word_is_allowed()
        {
            // "ali" (3 chars) must not veto "quality123" — the containment
            // rule needs a 4+ character username to avoid false positives.
            Assert.True(
                PasswordPolicy.ValidateNewPassword("quality123", "ali").Accepted);
        }

        [Fact]
        public void Password_equal_to_display_name_is_rejected()
        {
            Assert.False(
                PasswordPolicy.ValidateNewPassword(
                    "jubersaleem", "juber", "Juber Saleem").Accepted);
        }

        // ── Strength scoring guides, never rejects ───────────────────

        [Fact]
        public void Weak_but_not_known_bad_is_accepted_with_low_score()
        {
            var eval = PasswordPolicy.Evaluate("k7#mQ2");
            Assert.True(eval.Accepted);
            Assert.True(eval.Score <= 2);
            Assert.NotEqual("", eval.Guidance);
        }

        [Fact]
        public void Long_varied_password_scores_strong_with_no_guidance()
        {
            var eval = PasswordPolicy.Evaluate("Correct-Horse-9-Battery!");
            Assert.True(eval.Accepted);
            Assert.Equal(4, eval.Score);
            Assert.Equal("Strong", eval.StrengthLabel);
            Assert.Equal("", eval.Guidance);
        }

        [Fact]
        public void Rejected_passwords_score_zero_with_a_reason()
        {
            var eval = PasswordPolicy.Evaluate("password");
            Assert.False(eval.Accepted);
            Assert.Equal(0, eval.Score);
            Assert.NotEqual("", eval.RejectionReason);
            Assert.Equal("", eval.Guidance);
        }

        [Fact]
        public void Hint_names_the_same_floor_as_the_rule()
        {
            // The 12→6 incident was a hint promising 6 while code enforced
            // 12. This test pins the hint to the constant so the two can
            // never silently disagree again.
            Assert.Contains(
                PasswordPolicy.MinLength.ToString(), PasswordPolicy.HintText);
        }
    }
}
