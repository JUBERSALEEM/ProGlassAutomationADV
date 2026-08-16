// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Security/Integrity/AgentIntegrity.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE T)
//
// INTEGRITY — verifying that a file is what it claims to be.
//
// HONEST STATUS: THIS IS NOT YET LOAD-BEARING.
//   ProGlass deliberately does not download local models, so today there
//   is nothing whose hash must be checked before use. The master prompt
//   names Integrity as a required component, and the moment model
//   download is added this is where the check belongs — so it exists,
//   works, and is tested, rather than being a stub that lies.
//
//   It IS used today for one real thing: verifying an imported knowledge
//   JSON file has not been truncated mid-write, which is the actual
//   corruption mode this product can suffer.
//
// WHAT THIS DOES NOT CLAIM
//   A SHA-256 match proves a file matches an expected digest. It proves
//   nothing about whether that digest was trustworthy in the first place.
//   Without a signature chain this detects corruption and accidental
//   tampering, not a determined attacker. Do not describe it as more.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Security.Integrity
{
    /// <summary>Result of an integrity check.</summary>
    public sealed class IntegrityResult
    {
        public bool Verified { get; init; }
        public string Reason { get; init; } = "";
        public string ActualHash { get; init; } = "";

        public static IntegrityResult Ok(string hash) =>
            new() { Verified = true, ActualHash = hash };

        public static IntegrityResult Fail(string reason, string hash = "") =>
            new() { Verified = false, Reason = reason, ActualHash = hash };
    }

    /// <summary>File hashing and verification. Never throws.</summary>
    public static class AgentIntegrity
    {
        /// <summary>
        /// SHA-256 of a file, lowercase hex. Returns a failed result
        /// rather than throwing when the file is missing or locked.
        /// </summary>
        public static async Task<IntegrityResult> HashFileAsync(
            string path, CancellationToken cancel = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(path))
                    return IntegrityResult.Fail("No file path was given.");

                if (!File.Exists(path))
                    return IntegrityResult.Fail($"File not found: {path}");

                using var stream = new FileStream(
                    path, FileMode.Open, FileAccess.Read, FileShare.Read,
                    bufferSize: 81920, useAsync: true);

                using var sha = SHA256.Create();

                byte[] hash = await sha.ComputeHashAsync(stream, cancel)
                    .ConfigureAwait(false);

                return IntegrityResult.Ok(Convert.ToHexString(hash).ToLowerInvariant());
            }
            catch (OperationCanceledException)
            {
                return IntegrityResult.Fail("The integrity check was cancelled.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentIntegrity] hash failed: {ex.Message}");
                return IntegrityResult.Fail(
                    "The file could not be read for verification, so I treated " +
                    "it as unverified.");
            }
        }

        /// <summary>
        /// Verifies a file against an expected SHA-256. Fail-closed: a
        /// missing or malformed expectation verifies NOTHING rather than
        /// passing by default.
        /// </summary>
        public static async Task<IntegrityResult> VerifyFileAsync(
            string path, string expectedSha256, CancellationToken cancel = default)
        {
            if (string.IsNullOrWhiteSpace(expectedSha256))
                return IntegrityResult.Fail(
                    "No expected checksum was supplied, so I could not verify " +
                    "this file.");

            var actual = await HashFileAsync(path, cancel).ConfigureAwait(false);
            if (!actual.Verified) return actual;

            bool match = string.Equals(
                actual.ActualHash,
                expectedSha256.Trim(),
                StringComparison.OrdinalIgnoreCase);

            return match
                ? IntegrityResult.Ok(actual.ActualHash)
                : IntegrityResult.Fail(
                    "This file does not match its expected checksum, so I did " +
                    "not use it.",
                    actual.ActualHash);
        }

        /// <summary>
        /// Cheap structural check for a JSON payload: balanced braces and
        /// a sane length. Catches the real-world case — an import file
        /// truncated by a crash or a half-finished copy — without pulling
        /// in a parser.
        /// </summary>
        public static bool LooksStructurallyCompleteJson(string? json, int maxLength)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(json)) return false;
                if (json.Length > maxLength) return false;

                string trimmed = json.TrimStart();
                if (trimmed.Length == 0) return false;

                char first = trimmed[0];
                if (first != '{' && first != '[') return false;

                int depth = 0;
                bool inString = false;
                bool escaped = false;

                foreach (char c in json)
                {
                    if (escaped) { escaped = false; continue; }
                    if (c == '\\' && inString) { escaped = true; continue; }
                    if (c == '"') { inString = !inString; continue; }
                    if (inString) continue;

                    if (c == '{' || c == '[') depth++;
                    else if (c == '}' || c == ']') depth--;

                    if (depth < 0) return false;   // closed more than opened
                }

                return depth == 0 && !inString;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentIntegrity] json check: {ex.Message}");
                return false;   // fail closed
            }
        }
    }
}
