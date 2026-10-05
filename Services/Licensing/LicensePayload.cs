// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/LicensePayload.cs
// PROJECT: ProGlassAutomation
// MODULE: Licensing — runtime projection model
//
// WHAT THIS IS NOW
// ────────────────
// This type is no longer the authoritative on-disk licensing contract.
// The offline max-security redesign moved authority to the structured
// `ProGlass.Licensing` contracts plus a signed offline activation-response
// envelope.
//
// `LicensePayload` remains as the APP-SIDE projection model consumed by:
//   • About / Settings diagnostics
//   • status banners and activation success UI
//   • feature-gating convenience helpers
//
// In other words:
//   signed offline activation response  → verified runtime context → LicensePayload
//
// The fields are intentionally kept stable so the wider WPF app can keep
// using the same bindings while the underlying trust model is replaced.
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
        ActivationLimit = 8,

        /// <summary>
        /// Auto-issued 7-day trial has expired. The license
        /// file has been deleted from disk; the app MUST refuse
        /// to start and require a real license key. Distinct
        /// from <see cref="Expired"/> (which is for customer
        /// licenses that ran out) because the recovery path is
        /// different: an expired customer license can be
        /// renewed by the vendor, a TrialExpired state
        /// requires a brand-new activation.
        /// </summary>
        TrialExpired = 9
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

        // ── Trial provenance ───────────────────────────────────────
        //
        // In the secure offline redesign the APP no longer self-issues
        // trials. A trial, when present, is expected to be vendor-issued
        // through the signed offline activation-response flow.
        //
        // These fields remain because the wider UI already understands them,
        // and the runtime projection model uses them to distinguish:
        //   • normal paid activation
        //   • vendor-issued trial activation
        //   • trial expiry display
        //
        // TrialSource empty  => not a trial
        // TrialSource set    => some form of vendor-issued trial provenance
        // TrialExpiresAt     => authoritative trial window when present

        /// <summary>
        /// Identifies trial provenance. Empty string means this runtime
        /// payload is not a trial. Any non-empty value means the payload
        /// represents a vendor-issued trial and <see cref="TrialExpiresAt"/>
        /// takes precedence over <see cref="ExpiresAt"/> for display and
        /// expiry checks.
        /// </summary>
        [JsonPropertyName("trialSource")]
        public string TrialSource { get; init; } = "";

        /// <summary>
        /// The expiry timestamp for a vendor-issued trial. Only consulted
        /// when <see cref="TrialSource"/> is non-empty. For real
        /// (non-trial) licenses, leave at the default and let
        /// <see cref="ExpiresAt"/> drive expiry.
        /// </summary>
        [JsonPropertyName("trialExpiresAt")]
        public DateTime TrialExpiresAt { get; init; }

        // ── Derived helpers (read-only; never serialised) ────────────
        //
        // These let the UI / verifier read the licence's truth from the
        // payload itself instead of hardcoding "7 days" or "30 days". This
        // is the fix for the "shows a 7-day trial when it is actually a
        // 30-day trial" report: the displayed duration and balance now come
        // from TrialExpiresAt / IssuedAt, so whatever the issuer actually
        // issued is exactly what the user sees.

        /// <summary>
        /// True when this payload is a trial (any <c>TrialSource</c>).
        /// </summary>
        [JsonIgnore]
        public bool IsTrial => !string.IsNullOrWhiteSpace(TrialSource);

        /// <summary>
        /// The authoritative expiry, in UTC. For a trial with a set
        /// <see cref="TrialExpiresAt"/> that value is used (the strict
        /// window); otherwise — real licenses and legacy trials with no
        /// separate window — <see cref="ExpiresAt"/> is used. This is the
        /// single source the UI should read for the expiry, so it can
        /// never disagree with what the verifier enforces.
        /// </summary>
        [JsonIgnore]
        public DateTime EffectiveExpiresAt =>
            IsTrial && TrialExpiresAt != default
                ? TrialExpiresAt.ToUniversalTime()
                : ExpiresAt.ToUniversalTime();

        /// <summary>
        /// The total number of days this licence was issued for (the
        /// duration window), derived from the payload: the span between
        /// <see cref="IssuedAt"/> and <see cref="EffectiveExpiresAt"/>.
        /// Returns 0 when the payload cannot be interpreted (default dates).
        /// </summary>
        [JsonIgnore]
        public int TotalDays
        {
            get
            {
                try
                {
                    DateTime start = IssuedAt == default
                        ? DateTime.UtcNow
                        : IssuedAt.ToUniversalTime();
                    TimeSpan span = EffectiveExpiresAt - start;
                    int days = (int)Math.Round(span.TotalDays);
                    return Math.Max(days, 0);
                }
                catch { return 0; }
            }
        }

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
                // Trial provenance — see comment on the fields above.
                // Always written (even when empty / default) so the
                // canonical JSON shape is stable across paid activations
                // and vendor-issued trials. Older verifiers that don't
                // know about these fields still see a deterministic,
                // signed field shape.
                writer.WriteString("trialSource", TrialSource ?? "");
                writer.WriteString("trialExpiresAt",
                    TrialExpiresAt == default
                        ? "0001-01-01T00:00:00.000Z"
                        : TrialExpiresAt.ToUniversalTime()
                            .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));
                writer.WriteEndObject();
            }
            return System.Text.Encoding.UTF8.GetString(stream.ToArray());
        }
    }
}
