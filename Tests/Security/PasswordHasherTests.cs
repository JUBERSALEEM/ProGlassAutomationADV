// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Security/PasswordHasherTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Security — password hashing and legacy migration
//
// WHY THIS FILE EXISTS
// ────────────────────
// PasswordHasher had no tests at all, and Phase 9 changed the one line that
// decides whether existing users can still sign in.
//
// VerifyLegacySha256 used to end with:
//
//     return CryptographicOperations.FixedTimeEquals(hash, storedBytes)
//         || string.Equals(hex, storedHashHex, OrdinalIgnoreCase);
//
// under a comment claiming the byte comparison avoided "the timing leak of a
// string compare". The || reintroduced exactly that leak. It was also
// provably dead: if two hex strings are equal ignoring case they differ only
// in case, hex decoding is case-insensitive, so the decoded bytes are equal
// and the first arm has already returned true. The second arm could only ever
// run when the answer was already no.
//
// These tests are the net for removing it. The casing cases are the ones that
// matter: they are precisely what the removed arm appeared to be for, and they
// all pass without it, because comparing decoded bytes never cared about case.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Security.Cryptography;
using System.Text;
using ProGlassAutomation.Services.Security;
using Xunit;

namespace ProGlassAutomation.Tests.Security
{
    public sealed class PasswordHasherTests
    {
        private const string SaltHex = "1A2B3C4D5E6F7081";
        private const string Password = "correct horse battery staple";

        /// <summary>
        /// Reproduces the legacy scheme exactly as the production code does:
        /// SHA256(saltHex + password), rendered as hex.
        /// </summary>
        private static string LegacyHash(string password, string saltHex = SaltHex) =>
            Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(saltHex + password)));

        // ───────────────────────────────────────────────────────────────
        // Legacy verification survives the removal of the string compare
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void Legacy_uppercase_hash_verifies_and_flags_for_upgrade()
        {
            var result = PasswordHasher.Verify(Password, LegacyHash(Password), SaltHex);

            Assert.True(result.Success);
            Assert.True(result.NeedsUpgrade);   // caller re-hashes to PBKDF2
        }

        [Fact]
        public void Legacy_lowercase_hash_verifies()
        {
            // This is the case the removed string.Equals arm looked like it
            // existed for. It passes without it: Convert.FromHexString decodes
            // lower-case hex to the same bytes, and FixedTimeEquals compares
            // bytes, not characters.
            var result = PasswordHasher.Verify(
                Password, LegacyHash(Password).ToLowerInvariant(), SaltHex);

            Assert.True(result.Success);
            Assert.True(result.NeedsUpgrade);
        }

        [Fact]
        public void Legacy_mixed_case_hash_verifies()
        {
            var hex = LegacyHash(Password);
            var mixed = new char[hex.Length];
            for (int i = 0; i < hex.Length; i++)
                mixed[i] = i % 2 == 0
                    ? char.ToLowerInvariant(hex[i])
                    : char.ToUpperInvariant(hex[i]);

            var result = PasswordHasher.Verify(Password, new string(mixed), SaltHex);

            Assert.True(result.Success);
        }

        [Fact]
        public void Legacy_wrong_password_fails_without_flagging_an_upgrade()
        {
            var result = PasswordHasher.Verify("wrong password", LegacyHash(Password), SaltHex);

            Assert.False(result.Success);
            // NeedsUpgrade must stay false: re-hashing on a failed login would
            // overwrite the stored credential with a hash of the wrong password.
            Assert.False(result.NeedsUpgrade);
        }

        [Theory]
        [InlineData("ZZ")]                                  // not hex
        [InlineData("1A2B3C")]                              // valid hex, wrong length
        [InlineData("1A2B3C4D5E6F70811A2B3C4D5E6F70811A2B3C4D5E6F70811A2B3C4D5E6F7081FF")]  // 66 hex chars = 33 bytes, not a SHA-256 length
        [InlineData("1A2B3C4D5")]                            // odd length — FromHexString throws
        [InlineData("")]                                    // empty
        public void A_malformed_legacy_hash_reads_as_a_wrong_password(string stored)
        {
            // Verify is documented never to throw — a malformed stored value
            // must reach the login screen as "wrong password", not as an
            // exception the UI has to catch.
            var result = PasswordHasher.Verify(Password, stored, SaltHex);

            Assert.False(result.Success);
            Assert.False(result.NeedsUpgrade);
        }

        [Fact]
        public void An_empty_stored_hash_fails()
        {
            Assert.False(PasswordHasher.Verify(Password, "", SaltHex).Success);
            Assert.False(PasswordHasher.Verify(Password, null!, SaltHex).Success);
        }

        // ───────────────────────────────────────────────────────────────
        // Current scheme
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void Current_scheme_round_trips_and_needs_no_upgrade()
        {
            var stored = PasswordHasher.Hash(Password);

            Assert.True(PasswordHasher.IsCurrentScheme(stored));

            var result = PasswordHasher.Verify(Password, stored, SaltHex);
            Assert.True(result.Success);
            Assert.False(result.NeedsUpgrade);
        }

        [Fact]
        public void Current_scheme_rejects_a_wrong_password()
        {
            var stored = PasswordHasher.Hash(Password);

            Assert.False(PasswordHasher.Verify("not the password", stored, SaltHex).Success);
        }

        [Fact]
        public void Hashing_the_same_password_twice_produces_different_salts()
        {
            // A fresh salt per hash. If these were equal the salt would be
            // constant and identical passwords would share a digest.
            Assert.NotEqual(PasswordHasher.Hash(Password), PasswordHasher.Hash(Password));
        }

        // ───────────────────────────────────────────────────────────────
        // Policy
        // ───────────────────────────────────────────────────────────────

        [Fact]
        public void Minimum_length_is_six_and_single_sourced_from_policy()
        {
            // Was 12 (Phase 9.1), lowered to 6 on 2026-09-09 to match the
            // on-screen hint — but this assertion was never updated, so it
            // has been failing since. Asserted here so a future change is a
            // deliberate edit to a test, not a silent constant bump. This
            // governs only passwords being chosen now — Verify never checks
            // length, so no existing user is locked out.
            Assert.Equal(6, PasswordHasher.MinLength);
            Assert.Equal(PasswordPolicy.MinLength, PasswordHasher.MinLength);
        }
    }
}
