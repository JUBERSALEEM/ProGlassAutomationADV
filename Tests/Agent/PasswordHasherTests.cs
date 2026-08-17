// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/PasswordHasherTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Covers audit finding A-2: passwords were hashed with a SINGLE round of
// SHA-256. The salt was correct (16 random bytes per user) but SHA-256
// is fast by design, so audit.db — an ordinary %AppData% file that is
// also copied into every backup set — was cheap to brute-force.
//
// The replacement is PBKDF2-HMAC-SHA256 at 210,000 iterations.
//
// THE MOST IMPORTANT TEST IN THIS FILE is the migration pair. Existing
// installations already hold SHA-256 hashes. If the new code only
// understood the new format, every user would be locked out of their
// own ERP the moment they upgraded. Verify() must therefore still accept
// a legacy hash AND report that it needs upgrading.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Security;
using System;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class PasswordHasherTests
    {
        // ── Current scheme ────────────────────────────────────────────

        [Fact]
        public void Hash_then_verify_succeeds()
        {
            var stored = PasswordHasher.Hash("correct horse battery staple");
            var r = PasswordHasher.Verify(
                "correct horse battery staple", stored, "");

            Assert.True(r.Success);
            Assert.False(r.NeedsUpgrade);
        }

        [Fact]
        public void Wrong_password_fails()
        {
            var stored = PasswordHasher.Hash("admin");
            Assert.False(PasswordHasher.Verify("Admin", stored, "").Success);
            Assert.False(PasswordHasher.Verify("admin ", stored, "").Success);
            Assert.False(PasswordHasher.Verify("", stored, "").Success);
        }

        /// <summary>
        /// A random salt per hash means the same password must never
        /// produce the same stored value twice. If it does, the salt is
        /// not being applied and rainbow tables work again.
        /// </summary>
        [Fact]
        public void Same_password_hashes_differently_each_time()
        {
            var a = PasswordHasher.Hash("same");
            var b = PasswordHasher.Hash("same");

            Assert.NotEqual(a, b);
            Assert.True(PasswordHasher.Verify("same", a, "").Success);
            Assert.True(PasswordHasher.Verify("same", b, "").Success);
        }

        /// <summary>
        /// The stored value must record algorithm and iteration count so
        /// the count can be raised later without invalidating existing
        /// credentials.
        /// </summary>
        [Fact]
        public void Stored_format_is_self_describing()
        {
            var stored = PasswordHasher.Hash("x");
            var parts = stored.Split('$');

            Assert.Equal(4, parts.Length);
            Assert.Equal("PBKDF2", parts[0]);
            Assert.True(int.Parse(parts[1]) >= 210_000);
            Assert.True(PasswordHasher.IsCurrentScheme(stored));
        }

        /// <summary>
        /// AppUser.PasswordHash is MaxLength(128). If the encoded value
        /// exceeded that, EF would silently truncate and every login
        /// would fail after the upgrade.
        /// </summary>
        [Fact]
        public void Stored_value_fits_the_database_column()
        {
            Assert.True(PasswordHasher.Hash("any password").Length <= 128);
        }

        // ── Legacy migration — the regression that matters ────────────

        private static string LegacySha256(string password, string saltHex)
        {
            var input = Encoding.UTF8.GetBytes(saltHex + password);
            return Convert.ToHexString(SHA256.HashData(input));
        }

        [Fact]
        public void Legacy_sha256_hash_still_verifies_and_flags_upgrade()
        {
            const string salt = "A1B2C3D4E5F60718293A4B5C6D7E8F90";
            var legacy = LegacySha256("admin", salt);

            var r = PasswordHasher.Verify("admin", legacy, salt);

            Assert.True(r.Success);       // existing users are not locked out
            Assert.True(r.NeedsUpgrade);  // and get migrated silently
        }

        [Fact]
        public void Legacy_wrong_password_still_fails()
        {
            const string salt = "A1B2C3D4E5F60718293A4B5C6D7E8F90";
            var legacy = LegacySha256("admin", salt);

            var r = PasswordHasher.Verify("wrong", legacy, salt);

            Assert.False(r.Success);
            Assert.False(r.NeedsUpgrade);
        }

        /// <summary>
        /// The original comparison used OrdinalIgnoreCase, so a hash
        /// stored in lowercase hex by an older build must still verify.
        /// </summary>
        [Fact]
        public void Legacy_lowercase_hex_still_verifies()
        {
            const string salt = "DEADBEEF00112233445566778899AABB";
            var legacy = LegacySha256("secret", salt).ToLowerInvariant();

            Assert.True(PasswordHasher.Verify("secret", legacy, salt).Success);
        }

        /// <summary>
        /// After migration the value must be PBKDF2 and must no longer
        /// report NeedsUpgrade — otherwise it would re-hash on every login.
        /// </summary>
        [Fact]
        public void Upgraded_hash_stops_requesting_upgrade()
        {
            const string salt = "0F1E2D3C4B5A69788796A5B4C3D2E1F0";
            var legacy = LegacySha256("pw", salt);

            Assert.True(PasswordHasher.Verify("pw", legacy, salt).NeedsUpgrade);

            var upgraded = PasswordHasher.Hash("pw");
            var r = PasswordHasher.Verify("pw", upgraded, salt);

            Assert.True(r.Success);
            Assert.False(r.NeedsUpgrade);
        }

        // ── Malformed input must read as "wrong password" ─────────────

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("PBKDF2$notanumber$AAAA$BBBB")]
        [InlineData("PBKDF2$210000$not-base64!$BBBB")]
        [InlineData("PBKDF2$210000")]
        [InlineData("PBKDF2$-5$AAAA$BBBB")]
        [InlineData("zzz-not-hex-at-all")]
        public void Malformed_stored_value_never_throws(string stored)
        {
            var r = PasswordHasher.Verify("anything", stored, "SALT");
            Assert.False(r.Success);
        }

        /// <summary>
        /// A hostile iteration count must be rejected rather than freezing
        /// the UI thread for minutes inside PBKDF2.
        /// </summary>
        [Fact]
        public void Absurd_iteration_count_is_rejected_quickly()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = PasswordHasher.Verify(
                "x", "PBKDF2$2000000000$QUFBQQ==$QkJCQg==", "");
            sw.Stop();

            Assert.False(r.Success);
            Assert.True(sw.ElapsedMilliseconds < 1000,
                "Guard must reject before attempting the derivation.");
        }

        [Fact]
        public void Null_password_is_handled()
        {
            var stored = PasswordHasher.Hash(null!);
            Assert.True(PasswordHasher.Verify("", stored, "").Success);
        }

        [Fact]
        public void Unicode_passwords_round_trip()
        {
            // Arabic + emoji + accents: the app is used in the UAE and
            // must not mangle a non-ASCII password.
            const string pw = "كلمةالسر-Ünïcode-🔐";
            var stored = PasswordHasher.Hash(pw);

            Assert.True(PasswordHasher.Verify(pw, stored, "").Success);
            Assert.False(PasswordHasher.Verify(pw + "x", stored, "").Success);
        }
    }
}
