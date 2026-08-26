// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/LicenseVerifier.cs
// PROJECT: ProGlassAutomation
// MODULE: Commercial licensing — verification
//
// PURPOSE
// ───────
// Read a .lic file, verify its RSA-2048-PSS signature against the
// embedded public key, and return a <see cref="LicenseStatus"/>
// + the parsed <see cref="LicensePayload"/>.
//
// The verifier is the ONLY piece of licensing code the app's
// runtime calls on every cold start. It must be:
//   • fast (sub-millisecond on a modern CPU);
//   • constant-time relative to the signature (RSA-PSS verify
//     is constant-time in .NET 8);
//   • never throw — every failure becomes a <see cref="LicenseStatus"/>
//     so the UI can show a precise reason ("expired", "machine
//     mismatch", "activation limit", etc.).
//
// WHAT THE VERIFIER DOES NOT DO
// ─────────────────────────────
//   • Network calls (offline-first).
//   • Activation counting (handled by LicenseService).
//   • Mutating the file system.
//   • Calling OperationGuard. Feature gating is the caller's
//     job, after the verifier returns a Valid status.
//
// OUTPUT
// ──────
//   var result = LicenseVerifier.Verify(path);
//   switch (result.Status) {
//       case LicenseStatus.Valid: ...
//       case LicenseStatus.GracePeriod: ...  // read-only mode
//       case LicenseStatus.Expired: ...     // show "renew" UI
//       case LicenseStatus.Invalid: ...     // show "license corrupt"
//       case LicenseStatus.NotActivated: ... // show first-run UI
//   }
//   if (result.Payload != null) { ... }
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// Output of <see cref="LicenseVerifier.Verify"/>. Always
    /// returns a non-null <see cref="Status"/>. The
    /// <see cref="Payload"/> is non-null only when the file
    /// parsed cleanly; even on <see cref="LicenseStatus.Invalid"/>
    /// we set <see cref="ErrorDetail"/> to the human-readable
    /// reason so the UI does not have to invent one.
    /// </summary>
    public sealed class LicenseVerificationResult
    {
        public LicenseStatus Status { get; init; }
        public LicensePayload? Payload { get; init; }
        public string ErrorDetail { get; init; } = "";

        /// <summary>True when the app may run in this state
        /// (read-only counts as "may run"). The first-run UI
        /// uses the inverse: !CanRun ⇒ show welcome view.</summary>
        public bool CanRun => Status switch
        {
            LicenseStatus.Valid => true,
            LicenseStatus.ExpiringSoon => true,
            LicenseStatus.Trial => true,
            LicenseStatus.GracePeriod => true,   // read-only
            // Auto-issued 30-day trial that has expired drops the
            // app into read-only mode — the user can still open
            // every screen and browse every record, but new
            // writes / exports / prints are blocked until a real
            // license is installed. Customer data is preserved on
            // disk (the licensing subsystem never deletes it) so
            // switching from Trial to a paid Business / Factory
            // license mid-workflow is lossless.
            LicenseStatus.TrialExpired => true,
            // Anything else (NotActivated, Invalid,
            // MachineMismatch, ActivationLimit, Expired) is
            // also a non-runnable state. Explicit list here
            // keeps the switch exhaustive so a future enum
            // value triggers CS8509 at the next compile.
            LicenseStatus.NotActivated => false,
            LicenseStatus.Invalid => false,
            LicenseStatus.Expired => false,
            LicenseStatus.MachineMismatch => false,
            LicenseStatus.ActivationLimit => false,
            _ => false
        };

        /// <summary>True when the app must operate in read-only
        /// mode (no writes, no exports, no prints). Trial expiry
        /// and post-expiry grace period both land here — the
        /// customer keeps their data but cannot make new changes
        /// until they activate a license.</summary>
        public bool ReadOnly =>
            Status == LicenseStatus.GracePeriod
            || Status == LicenseStatus.TrialExpired;
    }

    /// <summary>
    /// Verifies a .lic file's RSA-2048-PSS signature and returns the
    /// parsed payload + a coarse-grained status. See the file
    /// header for the contract.
    /// </summary>
    public static class LicenseVerifier
    {
        /// <summary>
        /// How many days before <c>ExpiresAt</c> the status flips
        /// from <see cref="LicenseStatus.Valid"/> to
        /// <see cref="LicenseStatus.ExpiringSoon"/>. The UI
        /// shows a renewal banner above this threshold.
        /// </summary>
        public const int ExpiryWarningDays = 14;

        /// <summary>
        /// How many days after <c>ExpiresAt</c> the app keeps
        /// running in read-only mode. After this the app refuses
        /// to start (the welcome view re-appears with a
        /// "license expired" message).
        /// </summary>
        public static readonly TimeSpan GracePeriod = TimeSpan.FromDays(7);

        /// <summary>
        /// Short hex fingerprint (first 8 bytes of SHA-256 of the
        /// Modulus) of the embedded production public key. This lets the
        /// error message tell the vendor WHICH key this build expects, so
        /// a "signature does not match" is actionable instead of a dead
        /// end. Never throws; returns a marker for the placeholder.
        /// </summary>
        public static string EmbeddedPublicKeyFingerprint
        {
            get
            {
                try
                {
                    string keyXml = LicensePublicKey.PublicKeyXml;
                    if (LicensePublicKey.IsPlaceholder) return "PLACEHOLDER";
                    using var rsa = RSA.Create();
                    rsa.FromXmlString(keyXml);
                    var p = rsa.ExportParameters(false);
                    var hash = SHA256.HashData(p.Modulus);
                    var hex = new StringBuilder(16);
                    for (int i = 0; i < 8; i++) hex.Append(hash[i].ToString("x2"));
                    return hex.ToString();
                }
                catch
                {
                    return "UNKNOWN";
                }
            }
        }

        /// <summary>
        /// Read the file at <paramref name="licensePath"/>,
        /// verify the signature against the embedded public key,
        /// and return the result. Never throws — every failure
        /// becomes a <see cref="LicenseStatus"/>.
        /// </summary>
        public static LicenseVerificationResult Verify(string licensePath)
        {
            // No file → "not activated". This is the common
            // first-run state, not an error.
            if (string.IsNullOrWhiteSpace(licensePath) ||
                !File.Exists(licensePath))
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.NotActivated,
                    ErrorDetail = "No license file found at " + licensePath + "."
                };
            }

            // Placeholder public key in source → refuse. This is
            // a developer-machine / fresh-checkout state, not a
            // customer state; we surface it as "not configured"
            // so the operator can see why every license is
            // being rejected.
            if (LicensePublicKey.IsPlaceholder)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.NotActivated,
                    ErrorDetail =
                        "Embedded license public key is the placeholder. " +
                        "Replace Services/Licensing/LicensePublicKey.cs " +
                        "with a real RSA-2048 public key (see the " +
                        "LicenseGenerator tool)."
                };
            }

            // Parse the envelope. JsonException becomes Invalid.
            Envelope? envelope;
            try
            {
                string json = File.ReadAllText(licensePath, Encoding.UTF8);
                envelope = JsonSerializer.Deserialize<Envelope>(json,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
            }
            catch (Exception ex) when (
                ex is JsonException or IOException or UnauthorizedAccessException)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail = "Could not read/parse license file: " + ex.Message
                };
            }

            if (envelope is null || envelope.Payload is null ||
                string.IsNullOrEmpty(envelope.Signature))
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail = "License file is missing payload or signature."
                };
            }

            if (envelope.EnvelopeVersion != LicenseSigner.EnvelopeSchemaVersion)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail =
                        "Unsupported envelope version " + envelope.EnvelopeVersion +
                        "; this build expects " + LicenseSigner.EnvelopeSchemaVersion + "."
                };
            }

            // Decode the signature.
            byte[] signature;
            try
            {
                signature = Convert.FromBase64String(envelope.Signature);
            }
            catch (FormatException ex)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail = "Signature is not valid base64: " + ex.Message
                };
            }
            byte[] message = Encoding.UTF8.GetBytes(envelope.Payload.ToCanonicalJson());

            // Pick the right public key. A payload marked as a
            // trial (TrialSource != "") is verified against the
            // trial public key, not the production public key.
            // This separation is what lets the shipped app
            // issue its own 7-day trial without having the
            // production private key on disk. The trial key
            // pair is embedded in LicensePublicKey for this
            // purpose (see the threat-model comment there for
            // why this is a business-logic gate, not a
            // cryptographic one).
            string publicKeyXml = !string.IsNullOrEmpty(envelope.Payload.TrialSource)
                ? LicensePublicKey.TrialPublicKeyXml
                : LicensePublicKey.PublicKeyXml;

            // If the trial key is still the placeholder, refuse
            // any trial payload — the developer has not finished
            // configuring the licensing subsystem. A real
            // customer license (TrialSource == "") falls through
            // to the production-key path, which is its own
            // placeholder check.
            if (!string.IsNullOrEmpty(envelope.Payload.TrialSource)
                && LicensePublicKey.IsTrialKeyPlaceholder)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail =
                        "Trial public key is the placeholder. The developer " +
                        "must generate a real trial key pair via the " +
                        "LicenseGenerator and paste it into " +
                        "Services/Licensing/LicensePublicKey.cs before " +
                        "shipping to a customer."
                };
            }

            // Verify the signature. PSS first (the modern
            // padding); fall back to PKCS#1 v1.5 for backward
            // compatibility with old hand-rolled license tools.
            try
            {
                using var rsa = RSA.Create();
                rsa.FromXmlString(publicKeyXml);

                bool ok =
                    rsa.VerifyData(message, signature,
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
                    || rsa.VerifyData(message, signature,
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

                if (!ok)
                {
                    // Actionable diagnosis. "Signature does not match" is
                    // almost always a KEY MISMATCH: the license was signed
                    // with a private key whose public half differs from the
                    // one embedded in this app build. Tell the developer
                    // exactly what to do instead of leaving a dead end.
                    string hint =
                        "Signature does not match the payload. " +
                        "The license was signed with a DIFFERENT key than " +
                        "the one this app build expects. " +
                        "A copied / renamed / stale license, or an app " +
                        "rebuilt with a different public key, also causes " +
                        "this.\n" +
                        "\n" +
                        "Send this to your vendor so they can re-issue " +
                        "with the matching key: this app expects public-key " +
                        "fingerprint " + EmbeddedPublicKeyFingerprint + ".";

                    return new LicenseVerificationResult
                    {
                        Status = LicenseStatus.Invalid,
                        ErrorDetail = hint
                    };
                }
            }
            catch (Exception ex)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Invalid,
                    ErrorDetail = "Signature verification failed: " + ex.Message
                };
            }

            // ── Signature is valid. Now check time, machine,
            //    activation count. ──
            LicensePayload payload = envelope.Payload;

            // Machine fingerprint check first — it is independent
            // of time, and a mismatch is more specific than
            // "expired".
            if (!string.IsNullOrEmpty(payload.MachineFingerprint))
            {
                string current = MachineFingerprint.ComputeCurrent();
                if (!string.Equals(
                        current, payload.MachineFingerprint,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return new LicenseVerificationResult
                    {
                        Status = LicenseStatus.MachineMismatch,
                        Payload = payload,
                        ErrorDetail =
                            "License is bound to a different machine " +
                            "(expected " + payload.MachineFingerprint +
                            ", this machine is " + current + ")."
                    };
                }
            }

            // Time checks. Compare in UTC.
            DateTime now = DateTime.UtcNow;

            // For an auto-issued trial (TrialSource != ""), the
            // app sets TrialExpiresAt to the strict 7-day mark
            // but leaves ExpiresAt at the same value (or, in
            // older builds, at a long safety-net value). The
            // canonical expiry to enforce is TrialExpiresAt; if
            // it is unset (DateTime.MinValue, the default) we
            // fall back to ExpiresAt. For real (non-trial)
            // licenses, TrialSource is "" and we always use
            // ExpiresAt.
            DateTime expiresUtc;
            if (!string.IsNullOrEmpty(payload.TrialSource)
                && payload.TrialExpiresAt != default)
            {
                expiresUtc = payload.TrialExpiresAt.ToUniversalTime();
            }
            else
            {
                expiresUtc = payload.ExpiresAt.ToUniversalTime();
            }

            if (now > expiresUtc + GracePeriod)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Expired,
                    Payload = payload,
                    ErrorDetail =
                        "License expired on " + expiresUtc.ToString("u") +
                        " (grace period of " + GracePeriod.TotalDays +
                        " days has elapsed)."
                };
            }

            if (now > expiresUtc)
            {
                // Past expiry but inside the grace period.
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.GracePeriod,
                    Payload = payload,
                    ErrorDetail =
                        "License expired " +
                        (now - expiresUtc).TotalDays.ToString("0.0") +
                        " day(s) ago; running in read-only mode."
                };
            }

            // Valid. Is it expiring soon? Is it a trial?
            TimeSpan remaining = expiresUtc - now;
            if (remaining.TotalDays <= ExpiryWarningDays)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.ExpiringSoon,
                    Payload = payload,
                    ErrorDetail =
                        "License expires in " +
                        ((int)Math.Ceiling(remaining.TotalDays)) +
                        " day(s)."
                };
            }

            if (payload.Edition == LicenseEdition.Trial)
            {
                return new LicenseVerificationResult
                {
                    Status = LicenseStatus.Trial,
                    Payload = payload
                };
            }

            return new LicenseVerificationResult
            {
                Status = LicenseStatus.Valid,
                Payload = payload
            };
        }

        /// <summary>
        /// Internal envelope shape. Public so the JSON
        /// deserialiser can bind; not part of the public API
        /// of the licensing module.
        /// </summary>
        public sealed class Envelope
        {
            public int EnvelopeVersion { get; set; } = 1;
            public LicensePayload? Payload { get; set; }
            public string Signature { get; set; } = "";
        }
    }
}
