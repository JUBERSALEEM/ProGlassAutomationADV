// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Security/PasswordHasher.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — password hashing (audit finding A-2)
//
// WHAT WAS WRONG
// ──────────────
// AuditService hashed passwords as:
//
//     SHA256( saltHex + password )        — ONE round
//
// The salt was correct: 16 cryptographically random bytes, unique per
// user, which defeats rainbow tables. The problem is SPEED. SHA-256 is
// designed to be fast — a commodity GPU computes billions per second.
// An attacker who copies audit.db (it is an ordinary file in %AppData%,
// readable by any process running as that user, and it is included in
// every backup set the app writes) can brute-force realistic passwords
// in minutes. The seeded default password in this app is literally
// "admin".
//
// Password hashing must be DELIBERATELY SLOW. That is the entire point
// of a password-based key derivation function.
//
// WHAT THIS DOES
// ──────────────
// PBKDF2-HMAC-SHA256, 210,000 iterations, 16-byte random salt,
// 32-byte output. The iteration count follows the OWASP Password
// Storage Cheat Sheet recommendation for PBKDF2-HMAC-SHA256.
//
// Chosen over bcrypt/scrypt/Argon2 deliberately: PBKDF2 is in the .NET
// base class library (System.Security.Cryptography.Rfc2898DeriveBytes),
// so this adds NO new NuGet dependency. The brief says not to add
// packages unless genuinely required, and for this threat model —
// a local desktop ERP, not an internet-facing auth server — PBKDF2 at
// OWASP's iteration count is appropriate and defensible.
//
// STORAGE FORMAT (single self-describing string)
// ──────────────────────────────────────────────
//     PBKDF2$<iterations>$<saltBase64>$<hashBase64>
//
// Encoding the algorithm and iteration count INSIDE the stored value is
// what makes future upgrades possible: when hardware improves and the
// count needs raising, old hashes still verify with their own recorded
// count while new ones use the higher one. A scheme that stores only
// the raw hash can never be upgraded without forcing a password reset
// on every user.
//
// BACKWARD COMPATIBILITY — THE CRITICAL PART
// ──────────────────────────────────────────
// Existing installations already have SHA-256 hashes in audit.db. If
// this class only understood the new format, every existing user would
// be locked out of their own ERP on upgrade — an unacceptable
// regression. So Verify() detects the legacy format and validates it
// with the ORIGINAL algorithm, then reports NeedsUpgrade = true. The
// caller re-hashes with PBKDF2 using the password the user just proved
// they know, and saves. Migration is therefore silent, requires no user
// action, and completes on each user's next successful sign-in.
//
// The legacy path is kept for exactly this reason and is documented as
// verification-only: nothing in this class ever CREATES a SHA-256 hash.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Security.Cryptography;
using System.Text;

namespace ProGlassAutomation.Services.Security
{
    /// <summary>
    /// Result of verifying a password: whether it matched, and whether
    /// the stored hash uses an outdated scheme that should be upgraded.
    /// </summary>
    public readonly struct PasswordVerificationResult
    {
        public bool Success { get; }

        /// <summary>
        /// True when the password was correct but the stored hash used a
        /// weaker/older scheme. The caller should re-hash and persist.
        /// </summary>
        public bool NeedsUpgrade { get; }

        public PasswordVerificationResult(bool success, bool needsUpgrade)
        {
            Success = success;
            NeedsUpgrade = needsUpgrade;
        }
    }

    /// <summary>
    /// PBKDF2 password hashing with transparent migration from the
    /// legacy salted-SHA-256 scheme. Never throws.
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>
        /// Minimum length for a NEW or CHANGED password. Existing stored
        /// hashes are unaffected, so this only raises the bar for passwords
        /// set from this version onward (Phase 13 — security hardening).
        /// </summary>
        public const int MinLength = 8;

        /// <summary>Marker for the current scheme.</summary>
        private const string Pbkdf2Prefix = "PBKDF2";

        /// <summary>
        /// OWASP-recommended minimum for PBKDF2-HMAC-SHA256.
        /// Stored inside each hash so it can be raised later without
        /// invalidating existing credentials.
        /// </summary>
        private const int DefaultIterations = 210_000;

        private const int SaltBytes = 16;
        private const int HashBytes = 32;

        /// <summary>
        /// Hashes a password with PBKDF2 and returns a self-describing
        /// string safe to store in the database.
        /// </summary>
        public static string Hash(string password)
        {
            password ??= "";

            byte[] salt = RandomNumberGenerator.GetBytes(SaltBytes);

            byte[] hash = Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.UTF8.GetBytes(password),
                salt: salt,
                iterations: DefaultIterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: HashBytes);

            return string.Join('$',
                Pbkdf2Prefix,
                DefaultIterations.ToString(),
                Convert.ToBase64String(salt),
                Convert.ToBase64String(hash));
        }

        /// <summary>
        /// Verifies a password against a stored value in EITHER format.
        /// </summary>
        /// <param name="password">The password the user typed.</param>
        /// <param name="storedHash">
        /// Either "PBKDF2$iterations$salt$hash" (current) or a bare hex
        /// SHA-256 digest (legacy).
        /// </param>
        /// <param name="legacySaltHex">
        /// The user's PasswordSalt column. Only used for legacy hashes.
        /// </param>
        public static PasswordVerificationResult Verify(
            string password, string storedHash, string legacySaltHex)
        {
            if (string.IsNullOrWhiteSpace(storedHash))
                return new PasswordVerificationResult(false, false);

            password ??= "";

            try
            {
                if (storedHash.StartsWith(Pbkdf2Prefix + "$",
                        StringComparison.Ordinal))
                {
                    return new PasswordVerificationResult(
                        VerifyPbkdf2(password, storedHash), false);
                }

                // ── Legacy salted SHA-256 ─────────────────────────────
                // Verification only. Success reports NeedsUpgrade so the
                // caller can silently re-hash with PBKDF2.
                bool ok = VerifyLegacySha256(
                    password, storedHash, legacySaltHex);

                return new PasswordVerificationResult(ok, ok);
            }
            catch
            {
                // A malformed stored value must read as "wrong password",
                // never as an exception that reaches the login screen.
                return new PasswordVerificationResult(false, false);
            }
        }

        private static bool VerifyPbkdf2(string password, string stored)
        {
            // PBKDF2 $ iterations $ salt $ hash
            var parts = stored.Split('$');
            if (parts.Length != 4) return false;

            if (!int.TryParse(parts[1], out int iterations) ||
                iterations <= 0 || iterations > 10_000_000)
            {
                // Guard against a corrupted or hostile iteration count
                // that would otherwise hang the UI thread for minutes.
                return false;
            }

            byte[] salt = Convert.FromBase64String(parts[2]);
            byte[] expected = Convert.FromBase64String(parts[3]);

            byte[] actual = Rfc2898DeriveBytes.Pbkdf2(
                password: Encoding.UTF8.GetBytes(password),
                salt: salt,
                iterations: iterations,
                hashAlgorithm: HashAlgorithmName.SHA256,
                outputLength: expected.Length);

            // Constant-time comparison: a byte-by-byte early-exit compare
            // leaks how many leading bytes matched via timing.
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        /// <summary>
        /// Reproduces the ORIGINAL scheme exactly — SHA256(saltHex + password)
        /// as uppercase hex — so existing users can still sign in once.
        /// Never used to create a new hash.
        /// </summary>
        private static bool VerifyLegacySha256(
            string password, string storedHashHex, string saltHex)
        {
            var input = Encoding.UTF8.GetBytes((saltHex ?? "") + password);
            var hash = SHA256.HashData(input);
            var hex = Convert.ToHexString(hash);

            // The legacy code compared with OrdinalIgnoreCase, so hex
            // casing differences must still verify. Comparing the decoded
            // BYTES in fixed time preserves that behaviour without the
            // timing leak of a string compare.
            byte[] storedBytes;
            try
            {
                storedBytes = Convert.FromHexString(storedHashHex);
            }
            catch
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(hash, storedBytes)
                || string.Equals(hex, storedHashHex,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// True when a stored value uses the current scheme. Used by the
        /// migration path and by diagnostics.
        /// </summary>
        public static bool IsCurrentScheme(string storedHash) =>
            !string.IsNullOrEmpty(storedHash) &&
            storedHash.StartsWith(Pbkdf2Prefix + "$", StringComparison.Ordinal);
    }
}
