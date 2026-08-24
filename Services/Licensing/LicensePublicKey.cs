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
        // ─────────────────────────────────────────────────────────
        //  EMBEDDED PRODUCTION PUBLIC KEY (Aug 2026 senior audit fix)
        //
        //  Real RSA-2048 public key generated so the verifier no
        //  longer short-circuits to `NotActivated` on every launch
        //  because the placeholder string was still shipped. The
        //  matching PRIVATE key was NOT written to the source tree;
        //  it lives on the vendor's build machine and is used by
        //  Tools/LicenseGenerator to sign customer .lic files. A
        //  determined adversary who forges a payload with their
        //  own key pair cannot sign a valid production license
        //  against this modulus without the private half.
        //
        //  The trial subsystem below carries its OWN pair (both
        //  halves embedded) so the app can self-sign a 30-day
        //  trial on first launch without vendor involvement.
        // ─────────────────────────────────────────────────────────
        public static string PublicKeyXml { get; private set; } =
            "<RSAKeyValue>" +
            "<Modulus>2rJQoH/RW4j+V5IPCsgmEfOH2yi8Ow09LE6Cgoe6h+ernjT52/SXFCEUCHAjYJppXWIvtGhR/F2d9HpcDWFlCZgIGCtFmO3TAW9/0b/Q4d4i9eYCyAfSgyuRevk8jFzkPnR17uNnkqmpYnQE9Y5aYoEGJq64xjmRJNy/I9w1otHmxLVRA6My+Yeab6Vye5PMwWBQioxW+wz05oDfxWTsgSENdGavqdMOD3/HoaC2D3IsIifWle45c9EBmoPUGjaGYJHT+qrt190lOr7nIqFQYI5LgdIm5ZrDUKSgSxi6ZoFFsTdXPZle5rdoxOf3bMYvYDqGNm9xv2IsUuBJy0AlHw==</Modulus>" +
            "<Exponent>AQAB</Exponent>" +
            "</RSAKeyValue>";

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
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static void SetForTest(string xml) => PublicKeyXml = xml;

        // ═════════════════════════════════════════════════════════
        //  TRIAL KEY PAIR — embedded for the auto-issued 7-day trial
        //  (commit 2 of the licensing refactor)
        //
        //  A self-issued 7-day trial needs a key pair, because the
        //  existing verifier rejects unsigned or wrong-key payloads
        //  with Invalid. The production key pair is held only by
        //  the LicenseGenerator tool, so the shipped app cannot
        //  use it to sign a trial. We solve this by embedding a
        //  SECOND key pair, dedicated to trials:
        //
        //    • TrialPublicKeyXml — the public half, used by
        //      LicenseVerifier when payload.TrialSource is non-empty.
        //      Stored as a const-ish property so the verifier can
        //      reach it without a file-system read on cold start.
        //
        //    • TrialPrivateKeyXml — the private half, used by
        //      LicenseService.IssueAutoTrialIfEligible to sign a
        //      fresh trial on first launch. Marked internal + non-
        //      browsable so the IDE does not surface it.
        //
        //  THREAT MODEL — this is NOT cryptographically strong. A
        //  determined user can extract the private key from the
        //  shipped binary with ildasm / ILSpy and write a
        //  100-year trial of their own. The trial is a
        //  business-logic enforcement, not a cryptographic one.
        //  The actual 7-day limit is enforced by
        //  LicenseService.EnforceTrialExpiry deleting the file at
        //  day 8, regardless of what the payload says. The
        //  signature is here so the file is tamper-evident against
        //  ACCIDENTAL edits, not to be uncrackable. Real customer
        //  licenses use the production key pair and are unbreakable
        //  without the private key the customer does not have.
        //
        //  THE PLACEHOLDER below is a real-looking but invalid
        //  RSA XML. The verifier detects the placeholder and
        //  refuses to accept any trial signed against it. The
        //  developer (or LicenseGenerator) must generate a real
        //  pair and replace both halves. Until that is done, the
        //  auto-trial path is disabled and the app behaves as
        //  before Commit 2 (NotActivated on first launch).
        // ═════════════════════════════════════════════════════════

        /// <summary>
        /// RSA-2048 public key for the auto-issued trial
        /// subsystem. Used by <see cref="LicenseVerifier"/>
        /// when the payload's <c>TrialSource</c> field is
        /// non-empty (see <see cref="LicensePayload.TrialSource"/>).
        /// </summary>
        // ─────────────────────────────────────────────────────────
        //  EMBEDDED TRIAL PUBLIC KEY (Aug 2026 senior audit fix)
        //
        //  Real RSA-2048 public key generated so the verifier
        //  accepts payloads self-signed by the trial subsystem
        //  on first launch. Paired with TrialPrivateKeyXml
        //  below — both halves ship in the binary. This is a
        //  soft cryptographic constraint: a determined user
        //  who unpacks the DLL with ILSpy can extract the
        //  private key and forge trials, but the 30-day window
        //  enforcement lives in LicenseService.EnforceTrialExpiry
        //  and the AutoTrialWasIssued flag in AppConfig, not
        //  in the signature.
        // ─────────────────────────────────────────────────────────
        public static string TrialPublicKeyXml { get; private set; } =
            "<RSAKeyValue>" +
            "<Modulus>8lEKV8E6fNZabc0DG02/WwCHEEUnOUKWQF0OpHuxjDsInJIT4EVMTPu9CMPAJnJIV94cNTsJXQJ0x59x1B19k2m2vsP/5sx7i0Vkj7xyksIQu9N/0jfXvjZ5XraWSl4U1XQb7w8Z+dEdKxSJC4+8Sh1cyRmtE1F0x8gks9zoXznS0671I+vb6VSQrkMjSha+HVDAdEWYYkO7PEa8LTrSkWygxkXKIw0sFI9FVhiO//KULh0g0XN6ILjQgXsx0FdV1kNiU03x/Yr4XGAF9ZfKQWIK/OraMto6nbVhNHrbhqU6rk0bxNZ02WgLxoqU6Pjn0zoP2q82vS9z8vRdVQnqMQ==</Modulus>" +
            "<Exponent>AQAB</Exponent>" +
            "</RSAKeyValue>";

        /// <summary>
        /// RSA-2048 private key for the auto-issued trial
        /// subsystem. Held only in this assembly; never
        /// written to disk. Used by
        /// <see cref="LicenseService"/> to sign a 7-day
        /// trial on first launch.
        /// </summary>
        // Backing field for test overrides. In production this stays
        // null and the property below returns the XOR-shard-reassembled
        // key from TrialKeyVault. Tests call SetTrialKeysForTest to
        // swap in a synthetic pair; the swap wins over the vault.
        private static string? _trialPrivateKeyXmlOverride;

        /// <summary>
        /// RSA-2048 private key for the auto-issued trial subsystem.
        ///
        /// SENIOR-AUDIT HARDENING (Aug 2026): the plaintext XML is no
        /// longer embedded as a public string literal. It is stored in
        /// <see cref="TrialKeyVault"/> as four XOR-masked byte-array
        /// shards and reassembled on first access. `strings.exe` on
        /// the shipped DLL returns random bytes instead of a copy-
        /// paste-friendly `&lt;RSAKeyValue&gt;`. ILSpy still recovers
        /// the value after a small amount of work — the industrial
        /// norm for shipped desktop apps (Adobe / Autodesk / SAP GUI
        /// all use this same pattern). The 30-day window enforcement
        /// lives in <see cref="TrialLedger"/>, not in the signature.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        public static string TrialPrivateKeyXml
        {
            get => _trialPrivateKeyXmlOverride ?? TrialKeyVault.GetPrivateKeyXml();
        }

        /// <summary>
        /// True when the trial key pair is still the
        /// placeholder. While true, the auto-trial path is
        /// disabled and the verifier treats any
        /// TrialSource-tagged payload as Invalid. The
        /// developer must run the LicenseGenerator tool and
        /// replace both TrialPublicKeyXml and
        /// TrialPrivateKeyXml with a real pair before
        /// shipping to a customer.
        /// </summary>
        public static bool IsTrialKeyPlaceholder =>
            TrialPublicKeyXml.StartsWith("PLACEHOLDER_TRIAL_",
                System.StringComparison.Ordinal)
            || TrialPrivateKeyXml.StartsWith("PLACEHOLDER_TRIAL_",
                System.StringComparison.Ordinal);

        /// <summary>
        /// INTERNAL TEST HOOK. Swaps the trial key pair
        /// for the duration of a test that exercises the
        /// auto-trial path. Mirrors <see cref="SetForTest"/>
        /// for the production key.
        /// </summary>
        [EditorBrowsable(EditorBrowsableState.Never)]
        internal static void SetTrialKeysForTest(
            string publicXml, string privateXml)
        {
            TrialPublicKeyXml = publicXml;
            _trialPrivateKeyXmlOverride = privateXml;
            // Bust the vault cache so a future test that clears the
            // override immediately re-materialises the real embedded
            // key on the next read.
            TrialKeyVault.ResetCacheForTests();
        }
    }
}
