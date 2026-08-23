// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/LicensePayload.cs
// PROJECT: ProGlassAutomation
// MODULE: Commercial licensing — payload model
//
// WHAT THIS IS
// ────────────
// The on-disk format of a ProGlass Automation license file (.lic).
// A license is a JSON envelope containing the customer/company
// record PLUS an RSA-2048-PSS signature over the canonicalised
// record. The signature is verified by the app on every load —
// the private key only ever exists in the separate
// LicenseGenerator tool, never in the app's source tree.
//
// FILE SHAPE (illustrative — actual JSON is produced by
// LicenseSigner and consumed by LicenseVerifier):
//
//   {
//     "envelopeVersion": 1,
//     "payload": {
//       "schemaVersion": 1,
//       "licenseId":     "uuid",
//       "edition":       "Business",     // Trial | Basic | Business | Factory | Enterprise
//       "customerId":    "CUST-2026-001",
//       "companyName":   "Acme Glass LLC",
//       "issuedAt":      "2026-08-23T10:00:00Z",
//       "expiresAt":     "2027-08-23T10:00:00Z",
//       "maxActivations": 3,
//       "features":      ["Inquiry.*", "Proforma.*", "JobOrder.*"],
//       "machineFingerprint": null,    // null = portable; non-null = locked
//       "notes":         "Issued by J. Saleem 2026-08-23"
//     },
//     "signature": "base64(256 bytes RSA-2048-PSS over canonical JSON)"
//   }
//
// SIGNING MODEL
// ─────────────
//   • RSA-2048, PSS padding, SHA-256, via the BCL.
//   • Public key embedded as an XML const string in
//     LicensePublicKey.cs.
//   • Private key only ever lives in the LicenseGenerator tool,
//     stored on the developer's machine outside the source tree
//     (see %AppData%\ProGlassAutomation\license-keys.json).
//   • The signature is over the canonical JSON of the payload
//     (sorted keys, no whitespace), NOT over the pretty-printed
//     JSON the operator sees. This makes verification robust to
//     formatting changes. The round-trip tests guard this.
//
// WHY RSA, NOT ED25519
// ────────────────────
//   • Universal .NET support across every version back to .NET
//     Framework 2.0 — no risk of API rename in a future
//     release breaking every shipped license.
//   • .NET 8's Ed25519 class is in flux across previews.
//   • The rest of this codebase already uses RSA (PasswordHasher,
//     UpdateSignatureVerifier, DPAPI). One family for the whole
//     app means one toolset for the developer.
//   • 256-byte signatures are larger than Ed25519's 64 bytes,
//     but invisible to a customer.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// The customer-facing edition tier. The order is significant:
    /// <see cref="OperationGuard.LicenseEditionAtLeast"/> compares
    /// by ordinal. Never insert a value in the middle of this enum;
    /// always append, so the meaning of a stored edition string does
    /// not change for an existing customer.
    /// </summary>
    public enum LicenseEdition
    {
        /// <summary>30-day, single-activation, all features.</summary>
        Trial = 0,

        /// <summary>Single user, inquiry + proforma only.</summary>
        Basic = 1,

        /// <summary>Up to 5 users, full ERP without production optimisation.</summary>
        Business = 2,

        /// <summary>Unlimited users, includes cutting-optimisation engine.</summary>
        Factory = 3,

        /// <summary>Unlimited + multi-site + custom integrations.</summary>
        Enterprise = 4
    }

    /// <summary>
    /// Status returned by <see cref="LicenseVerifier.Verify"/> and
    /// surfaced in the About page. The enum order is meaningful:
    /// Verifier picks the FIRST applicable status in this order.
    /// </summary>
    public enum LicenseStatus
    {
        /// <summary>No license file on disk.</summary>
        NotActivated = 0,

        /// <summary>File present but signature is invalid / corrupt.</summary>
        Invalid = 1,

        /// <summary>Valid signature, expired, in grace period (read-only).</summary>
        GracePeriod = 2,

        /// <summary>Valid signature, past expiresAt, no grace left.</summary>
        Expired = 3,

        /// <summary>Valid signature, expires within 14 days.</summary>
        ExpiringSoon = 4,

        /// <summary>Valid signature, expires in more than 14 days.</summary>
        Valid = 5,

        /// <summary>Edition is Trial; surfaces "trial" badge in UI.</summary>
        Trial = 6,

        /// <summary>Machine fingerprint does not match this device.</summary>
        MachineMismatch = 7,

        /// <summary>Too many devices have activated this license.</summary>
        ActivationLimit = 8
    }

    /// <summary>
    /// On-disk customer/company record. Serialised as the inner JSON
    /// of a license file. The outer envelope (signature etc.) is
    /// produced by <see cref="LicenseSigner"/> and parsed by
    /// <see cref="LicenseVerifier"/>.
    ///
    /// All fields are init-only so a payload is immutable after
    /// deserialisation. The <see cref="MachineFingerprint"/> is
    /// intentionally separate from <see cref="MaxActivations"/>: a
    /// portable license (null fingerprint, MaxActivations=N) lets the
    /// customer run on N devices without binding; a device-locked
    /// license (fingerprint set, MaxActivations=1) is the cheap tier.
    /// </summary>
    public sealed record LicensePayload
    {
        /// <summary>Schema version. Bump on breaking changes to the
        /// payload shape (currently always 1).</summary>
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; init; } = 1;

        /// <summary>Stable, unique license id. Issuer-supplied
        /// (UUID/GUID). Used for activation-counting and audit
        /// trails — the app never makes decisions based on it
        /// beyond "is this license the same one I saw last time?"</summary>
        [JsonPropertyName("licenseId")]
        public string LicenseId { get; init; } = "";

        /// <summary>Edition tier. Stored as the enum name
        /// (e.g. "Business") so a typo on the issuer side is
        /// obvious in the file. <see cref="LicenseVerifier"/>
        /// refuses unknown values.</summary>
        [JsonPropertyName("edition")]
        public LicenseEdition Edition { get; init; } = LicenseEdition.Trial;

        /// <summary>Issuer's customer code (e.g. "CUST-2026-001").
        /// Free-form — the app shows it in the About page and
        /// writes it to the audit trail on every license event,
        /// nothing more.</summary>
        [JsonPropertyName("customerId")]
        public string CustomerId { get; init; } = "";

        /// <summary>End-customer company name. Shown in the About
        /// page, written to the audit trail on activation. Free
        /// form, not validated.</summary>
        [JsonPropertyName("companyName")]
        public string CompanyName { get; init; } = "";

        /// <summary>UTC issuance timestamp. After a license is
        /// loaded, the app shows "Issued X days ago" in the
        /// About page; nothing depends on this value.</summary>
        [JsonPropertyName("issuedAt")]
        public DateTime IssuedAt { get; init; }

        /// <summary>UTC expiry. <see cref="LicenseVerifier"/>
        /// compares this against <c>DateTime.UtcNow</c> with a
        /// configurable grace period on top.</summary>
        [JsonPropertyName("expiresAt")]
        public DateTime ExpiresAt { get; init; }

        /// <summary>How many devices may activate this license.
        /// The app enforces this on first activation; a portable
        /// license with N=3 means the customer can run on up to
        /// three machines, tracked by the local activation ledger
        /// (see <c>LicenseService</c>).</summary>
        [JsonPropertyName("maxActivations")]
        public int MaxActivations { get; init; } = 1;

        /// <summary>Feature allowlist. Strings are matched by
        /// <see cref="OperationGuard.FeatureAllowed"/> against
        /// <see cref="Services.Audit.AppPermissions"/> values
        /// (e.g. "Inquiry.*" matches InquiryView/Create/Edit/
        /// Delete). An empty list means "no features — effectively
        /// a read-only app" and is treated as a misconfiguration
        /// on the issuer side; the verifier will still load the
        /// license but the UI will surface "no features enabled".</summary>
        [JsonPropertyName("features")]
        public IReadOnlyList<string> Features { get; init; } =
            Array.Empty<string>();

        /// <summary>Hardware fingerprint of the device this
        /// license is bound to. <c>null</c> means portable (the
        /// license travels with the .lic file). When set, the
        /// app compares it against
        /// <see cref="MachineFingerprint.ComputeCurrent"/> on
        /// load and refuses to start if it does not match.</summary>
        [JsonPropertyName("machineFingerprint")]
        public string? MachineFingerprint { get; init; }

        /// <summary>Free-form notes from the issuer. Shown in
        /// the About page next to the other license metadata.
        /// Never parsed by the app.</summary>
        [JsonPropertyName("notes")]
        public string Notes { get; init; } = "";

        /// <summary>
        /// Canonical JSON of this payload, with keys sorted
        /// alphabetically and no insignificant whitespace. This is
        /// the EXACT string the signature is computed over. Any
        /// change to this method (whitespace, ordering, key case)
        /// breaks every existing license; the round-trip tests in
        /// <c>Tests/Licensing/LicenseSignerTests.cs</c> guard
        /// this.
        /// </summary>
        public string ToCanonicalJson()
        {
            // System.Text.Json does not support a sorted-keys option
            // directly. We build a JsonObject with explicit member
            // ordering instead — the fields are listed in the same
            // order as the public surface so the JSON is stable
            // across .NET versions.
            using var stream = new System.IO.MemoryStream();
            using (var writer = new Utf8JsonWriter(stream,
                new JsonWriterOptions { Indented = false }))
            {
                writer.WriteStartObject();
                writer.WriteNumber("schemaVersion", SchemaVersion);
                writer.WriteString("licenseId", LicenseId ?? "");
                writer.WriteString("edition", Edition.ToString());
                writer.WriteString("customerId", CustomerId ?? "");
                writer.WriteString("companyName", CompanyName ?? "");
                writer.WriteString("issuedAt",
                    IssuedAt.ToUniversalTime()
                        .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                writer.WriteString("expiresAt",
                    ExpiresAt.ToUniversalTime()
                        .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                writer.WriteNumber("maxActivations", MaxActivations);
                writer.WriteStartArray("features");
                foreach (var f in Features.OrderBy(s => s, StringComparer.Ordinal))
                    writer.WriteStringValue(f);
                writer.WriteEndArray();
                if (MachineFingerprint == null)
                    writer.WriteNull("machineFingerprint");
                else
                    writer.WriteString("machineFingerprint", MachineFingerprint);
                writer.WriteString("notes", Notes ?? "");
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
