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
        /// WHY static readonly AND NOT const
        /// ─────────────────────────────────
        /// A `const string` is compiled into the assembly's
        /// metadata as a literal, and every reference is
        /// inlined at the call site at compile time. The CLR
        /// therefore has no field to write to at runtime —
        /// `FieldInfo.SetValue` throws FieldAccessException
        /// ("Cannot set a constant field") on every version
        /// of .NET. Tests that try to swap the public key
        /// (LicenseSignerTests, LicenseGateTests,
        /// LicenseServiceTests, LicenseStatusViewModelTests,
        /// CrashReportServiceTests) need the field to be
        /// writable.
        ///
        /// `static readonly` keeps the field read-only from
        /// production code (no public setter, no property
        /// mutator) while allowing reflection-based mutation
        /// from test code. The micro-optimisation we lose is
        /// the compile-time inlining of the string at the
        /// call site, which is irrelevant: this field is
        /// read once per license verification, not in a hot
        /// loop. We do, however, also gain the ability to
        /// rotate the public key at runtime in the future
        /// (e.g. an admin "switch key pair" command) without
        /// a rebuild, which is a small but real feature.
        /// </summary>
        public static readonly string PublicKeyXml =
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
    }
}
