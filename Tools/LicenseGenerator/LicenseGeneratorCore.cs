// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/LicenseGeneratorCore.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Single-click plan-based license-key signing core (2026)
//
// ONE JOB
// ───────
// Pick a plan, press GENERATE, get ONE copy-paste text block:
//
//     PGKEY1.<base64url(signed .proglasslicense JSON)>
//
// The customer pastes that block into the app (Settings → License) and
// the app verifies the vendor signature against its embedded public-key
// ring and activates instantly. No key files, no requests, no responses.
//
// PLANS
// ─────
//   7 Days (7) · 30 Days (30) · 6 Months (182) · 1 Year (365) · 2 Years (730)
// Every generated key grants the full feature scope ("*") as a
// Subscription license that expires at issuedAt + plan days.
//
// SECURITY MODEL
// ──────────────
//   • Signing = RSA-2048-PSS over the canonical document bytes
//     (LicenseCanonical). Verification = embedded public keys only.
//   • The keyId is embedded in the signature envelope; the app maps it
//     to the trusted public key. An unknown keyId → rejected.
//   • The embedded production pair lives in ProductionSigningKey.cs;
//     its public half is ALSO embedded in the customer app, so a key
//     signed here is guaranteed to verify there.
//   • Every key is self-verified before it is shown: the contract
//     Create() re-reads and VerifyAndValidate-s the exact JSON it
//     signed, so a tampered or mismatched key can never be emitted.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProGlass.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// Stateless signing core for the single-click plan-based flow.
/// </summary>
public static class LicenseGeneratorCore
{
    public const string LicenseKeyFileExtension = ".lic";

    /// <summary>Default folder for exported key files.</summary>
    public static string DefaultOutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ProGlassAutomation",
        "Licensing");

    /// <summary>The five sellable plans, in UI order.</summary>
    public static IReadOnlyList<LicensePlan> Plans { get; } = new[]
    {
        new LicensePlan("7 Days", 7),
        new LicensePlan("30 Days", 30),
        new LicensePlan("6 Months", 182),
        new LicensePlan("1 Year", 365),
        new LicensePlan("2 Years", 730)
    };

    /// <summary>A sellable validity window.</summary>
    public sealed class LicensePlan
    {
        public string Name { get; }
        public int Days { get; }

        public LicensePlan(string name, int days)
        {
            Name = name;
            Days = days;
        }
    }

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
    /// Sign a full-feature license key for the selected plan with the
    /// embedded production key and armor it into the single-line PGKEY1
    /// block. The contract library signs, re-reads and self-verifies the
    /// exact JSON, so the returned key is guaranteed to verify against
    /// the public half the customer app embeds.
    /// </summary>
    public static LicenseKeyIssueResult GenerateLicenseKey(LicensePlan plan)
    {
        try
        {
            if (plan == null)
                return new LicenseKeyIssueResult { Success = false, Message = "Pick a plan first." };

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
                ProductionSigningKey.PrivateKeyXml,
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
                appVersion: "2.0.0",
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
            return new LicenseKeyIssueResult
            {
                Success = true,
                Message = string.Format(
                    "Signed {0} license key · full features · expires {1} (UTC). Send the PGKEY1 block to the customer.",
                    plan.Name,
                    expires?.ToString("yyyy-MM-dd") ?? "never"),
                ArmoredKey = LicenseKeyArmor.Encode(gen.LicenseJson),
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
