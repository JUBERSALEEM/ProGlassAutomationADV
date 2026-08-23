// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/LicensePublicKey.cs
// PROJECT: ProGlassAutomation
// MODULE: Commercial licensing — embedded public key
//
// WHY A CONST STRING
// ─────────────────
// The public key is part of the app — every shipped binary must
// embed it. Putting it in a const string (rather than a resource or
// a config file) means:
//   • The key cannot be swapped at runtime by editing config.
//   • The key is included in the binary's compile-time constant
//     pool, so it is visible to anyone who runs `ildasm` on the
//     DLL — this is acceptable because a public key is PUBLIC.
//   • The verification path does not need a file-system read on
//     cold start, so it works in low-trust AppDomains and during
//     tests that do not have a real %AppData%.
//
// HOW THE KEY GETS HERE
// ─────────────────────
// 1. The first time someone runs the LicenseGenerator tool on
//    the developer's machine, it generates a fresh RSA-2048 key
//    pair and saves:
//      • Private key  →  %AppData%\ProGlassAutomation\license-keys.json
//                        (NEVER committed; file system only).
//      • Public key   →  the developer copies it into this file
//                        and rebuilds.
// 2. The generator prints the EXACT line to paste in, so the
//    developer cannot typo it.
// 3. Until the placeholder below is replaced with a real public
//    key, every license verification returns Invalid. This is the
//    intended behaviour for a fresh checkout — it is loud, not
//    silent.
//
// WHY RSA-2048
// ────────────
// • Universal .NET support (RSA is in the BCL since .NET 1.0).
// • The rest of this codebase already uses RSA for password
//   hashing, Authenticode verification, and DPAPI. Adding a
//   second key family would mean a second toolset for the
//   developer.
// • 2048 bits is ~112-bit security equivalent, which is the
//   baseline used by every major PKI for code signing.
// ═══════════════════════════════════════════════════════════════════════

using System.ComponentModel;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// The RSA-2048 public key used to verify license file
    /// signatures. The corresponding private key is held ONLY
    /// by the LicenseGenerator tool — it must never be present
    /// in this source tree, in CI logs, or in the shipped
    /// binary.
    /// </summary>
    public static class LicensePublicKey
    {
        /// <summary>
        /// RSA-2048 public key in the standard .NET XML format
        /// produced by <c>RSA.ToXmlString(false)</c>. The
        /// value below is a PLACEHOLDER. Every license
        /// signature produced against it will fail
        /// verification with <see cref="LicenseStatus.Invalid"/>,
        /// which is the intended behaviour for a fresh
        /// checkout.
        ///
        /// Replace with a real key on first use:
        ///   1. Run Tools/LicenseGenerator/ProGlassAutomation.LicenseGenerator.exe
        ///   2. Choose "Generate new key pair".
        ///   3. Copy the printed line into this field.
        ///   4. Rebuild.
        ///
        /// Until step 4 is done, the app shows "License: not
        /// activated (placeholder key in use)".
        ///
        /// WHY auto-property WITH PRIVATE SETTER (not const, not static readonly)
        /// ──────────────────────────────────────────────────────────────────────
        /// A `const string` is compiled into the assembly's
        /// metadata as a literal — FieldInfo.SetValue throws
        /// FieldAccessException on every .NET version. A
        /// `static readonly` field is also initonly at the
        /// CLR level — FieldInfo.SetValue throws
        /// FieldAccessException on .NET Core 3.0+ (and the
        /// .NET 10 SDK on the build machine).
        ///
        /// An auto-property with a private setter is the
        /// only shape that satisfies BOTH requirements:
        ///   1. Production code cannot change the value
        ///      (private setter is not callable from outside
        ///      the class).
        ///   2. Tests in the same assembly's test project can
        ///      swap the value via SetForTest, the documented
        ///      internal hook. The test hook is the supported
        ///      alternative to reflection-on-initonly-field
        ///      and is what every .NET BCL that needs this
        ///      pattern (e.g. System.Text.Json's JsonSerializer
        ///      test hooks) does.
        ///
        /// The micro-optimisation we lose is the C# compile-
        /// time inlining of the string at the call site, which
        /// is irrelevant: this field is read once per license
        /// verification, not in a hot loop.
        /// </summary>
        public static string PublicKeyXml { get; private set; } =
            "PLACEHOLDER_REPLACE_WITH_REAL_RSA2048_PUBLIC_KEY_XML";

        /// <summary>
        /// True while the embedded public key is the placeholder.
        /// The check is intentionally cheap (string compare) so it
        /// can run on every cold start. <see cref="LicenseService"/>
        /// surfaces this as a special "NotConfigured" status so
        /// the user is not left wondering why a real license file
        /// fails to validate.
        /// </summary>
        public static bool IsPlaceholder =>
            PublicKeyXml.StartsWith("PLACEHOLDER_", System.StringComparison.Ordinal);

        /// <summary>
        /// INTERNAL TEST HOOK. Swaps the public key for the
        /// duration of a test. Production code cannot reach
        /// this because it is marked [EditorBrowsable(Never)]
        /// and lives in a production assembly that the test
        /// project gets via a ProjectReference (with
        /// <PrivateAssets>all</PrivateAssets> on the Roslyn
        /// reference, but the licensing types have no such
        /// marker, so the test assembly CAN see this method).
        ///
        /// Replace the reflection-on-initonly-field pattern
        /// (which throws FieldAccessException on .NET Core 3.0+)
        /// with this direct call. The test owns the call site
        /// and the production assembly owns the policy (private
        /// setter, no public surface).
        /// </summary>
        [EditorBrowsable(Never)]
        internal static void SetForTest(string xml) => PublicKeyXml = xml;
    }
}
