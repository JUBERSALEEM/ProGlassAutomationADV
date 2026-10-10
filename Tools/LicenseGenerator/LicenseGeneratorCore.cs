// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/LicenseGeneratorCore.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Single-click plan-based license-key signing core (2026)
//
// CURRENT STATUS — ISSUANCE PAUSED
// ─────────────────────────────────
// The prior production private key was exposed in tracked source. The
// generator refuses to read the old source value or any DPAPI cache, and
// the UI is disabled. Do not issue licenses from old generator binaries.
// A replacement key, app trust-ring release, and existing-license migration
// must be approved before issuance can resume.
//
// PLANS
// ─────
//   7 Days (7) · 30 Days (30) · 6 Months (182) · 1 Year (365) · 2 Years (730)
// Every generated key grants the full feature scope ("*") as a
// Subscription license that expires at issuedAt + plan days.
//
// SECURITY STATUS
// ───────────────
//   • The former production private key was exposed in tracked source
//     and is treated as compromised.
//   • KeyProtection.GetPrivateKey() fails closed; both the embedded
//     fallback and old DPAPI cache are ignored. This tool cannot issue
//     licenses until an owner-approved replacement signer is provisioned.
//   • The public key remains only for compatibility review. The customer
//     app still trusts the old key until a replacement and customer
//     migration plan are approved and released.
//   • The plan set remains the global LicensePlanCatalog.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using ProGlass.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// License-generation core. Issuance currently fails closed in
/// <see cref="KeyProtection.GetPrivateKey"/> until the production key
/// rotation is completed and approved.
/// </summary>
public static class LicenseGeneratorCore
{
    public const string LicenseKeyFileExtension = ".lic";

    /// <summary>Default folder for exported key files.</summary>
    public static string DefaultOutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ProGlassAutomation",
        "Licensing");

    /// <summary>The five sellable plans, in UI order (global catalog).</summary>
    public static IReadOnlyList<LicensePlanCatalog.Plan> Plans => LicensePlanCatalog.All;

    public sealed class LicenseKeyIssueResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        /// <summary>The single-line PGKEY1 block the customer pastes.</summary>
        public string ArmoredKey { get; init; } = "";
        /// <summary>The signed .proglasslicense JSON inside the armor (audit).</summary>
        public string LicenseJson { get; init; } = "";
        public string? SigningKeyId { get; init; }
        public LicenseDocument? Document { get; init; }
    }

    /// <summary>
    /// License issuance is temporarily disabled because the former
    /// production signing key was exposed. The fail-closed key provider
    /// returns a clear failure until a replacement signer is approved.
    /// </summary>
    public static LicenseKeyIssueResult GenerateLicenseKey(LicensePlanCatalog.Plan plan)
    {
        try
        {
            if (plan == null)
                return new LicenseKeyIssueResult { Success = false, Message = "Pick a plan first." };

            // SECURITY: Rate limiter — prevent mass key generation
            if (!KeyProtection.CheckRateLimit(out string? rateReason))
            {
                return new LicenseKeyIssueResult
                {
                    Success = false,
                    Message = rateReason ?? "Rate limit reached."
                };
            }

            // Fail closed: KeyProtection refuses both the old embedded key
            // and any cached DPAPI copy until a replacement signer is approved.
            string privateKey = KeyProtection.GetPrivateKey();

            var gen = LicenseService.Create(
                new LicenseRequest
                {
                    CustomerId = "PLAN-" + plan.Name.Replace(" ", ""),
                    Company = "ProGlass plan: " + plan.Name,
                    Notes = "Single-click plan key · " + plan.Name,
                    LicenseType = "Subscription",
                    DaysValid = plan.Days,
                    Features = new List<string> { "*" },
                    DeviceBinding = "Unbound"
                },
                privateKey,
                ProductionSigningKey.KeyId);

            if (!gen.Success || string.IsNullOrWhiteSpace(gen.LicenseJson))
            {
                return new LicenseKeyIssueResult
                {
                    Success = false,
                    Message = gen.Message ?? "License key generation failed.",
                    SigningKeyId = gen.KeyId,
                    Document = gen.Document
                };
            }

            // Second opinion: verify against the SAME trust map the
            // customer app uses (keyId → embedded public half). This is
            // what makes "generate → paste → instant active" guaranteed.
            var verify = LicenseService.VerifyAndValidate(
                gen.LicenseJson,
                appVersion: "2.0.1",
                utcNow: DateTime.UtcNow,
                trustedPublicKeys: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [ProductionSigningKey.KeyId] = ProductionSigningKey.PublicKeyXml
                });

            if (!verify.IsValid)
            {
                return new LicenseKeyIssueResult
                {
                    Success = false,
                    Message = "Generated key failed the customer-app trust check: " + verify.Message,
                    SigningKeyId = gen.KeyId,
                    Document = gen.Document
                };
            }

            DateTime? expires = gen.Document?.ExpiresAtUtc?.ToUniversalTime();
            string armoredKey = LicenseKeyArmor.Encode(gen.LicenseJson);

            // SECURITY: Audit log every successful key generation
            KeyProtection.RecordGeneration(plan.Name, armoredKey);

            return new LicenseKeyIssueResult
            {
                Success = true,
                Message = string.Format(
                    "Signed {0} license key · full features · expires {1} (UTC). Send the PGKEY1 block to the customer.",
                    plan.Name,
                    expires?.ToString("yyyy-MM-dd") ?? "never"),
                ArmoredKey = armoredKey,
                LicenseJson = gen.LicenseJson,
                SigningKeyId = gen.KeyId,
                Document = gen.Document
            };
        }
        catch (Exception ex)
        {
            return new LicenseKeyIssueResult { Success = false, Message = "License key generation failed: " + ex.Message };
        }
    }
}
