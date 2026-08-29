// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/LicenseGeneratorCore.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Vendor-side license-key signing core (2026 simple flow)
//
// ONE JOB
// ───────
// This tool signs customer LICENSE KEYS. The operator loads an external
// issuer key file (.pgkey — the private key NEVER lives in this repo or in
// the customer app), picks an edition and a validity window, optionally
// binds the key to a customer PC ID, and the tool produces ONE copy-paste
// text block:
//
//     PGKEY1.<base64url(signed .proglasslicense JSON)>
//
// The customer pastes that block into the app (Settings → License) and the
// app verifies the signature against its embedded public-key ring and
// activates instantly. No requests, no responses, no references.
//
// SECURITY MODEL (unchanged from the max-security redesign)
// ─────────────────────────────────────────────────────────
//   • Signing = RSA-2048-PSS over the canonical document bytes
//     (LicenseCanonical). Verification = embedded public keys only.
//   • The keyId is embedded in the signature envelope; the app maps it to
//     a trusted public key. An unknown keyId → rejected.
//   • Product binding (PROGLASS_AUTOMATION), issue/expiry dates, version
//     policy and optional single-device binding are all signed data.
//   • The generator self-verifies every key it emits (Create re-reads and
//     VerifyAndValidate-s the exact JSON it signed), so a misconfigured
//     issuer key can never silently produce a key the app will reject.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProGlass.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// Stateless signing core for the simple license-key flow.
/// </summary>
public static class LicenseGeneratorCore
{
    public const string IssuerKeyExtension = ".pgkey";
    public const string LicenseKeyFileExtension = ".lic";

    /// <summary>Default folder for exported artifacts.</summary>
    public static string DefaultOutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ProGlassAutomation",
        "Licensing");

    /// <summary>
    /// Recommended feature presets per edition. The APP derives the
    /// displayed edition back from these exact presets (see
    /// LicenseService.DeriveKeyEdition), so the two must stay in sync:
    ///   Trial      → "*"
    ///   Basic      → Inquiry.*, Proforma.*
    ///   Business   → + JobOrder/DeliveryNote/TaxInvoice/Statement/Reports/AI
    ///   Factory    → + Cutting.*
    ///   Enterprise → "*"
    /// </summary>
    public static IReadOnlyList<string> DefaultFeatures(string edition)
    {
        return NormalizeEdition(edition) switch
        {
            "Trial" => new[] { "*" },
            "Basic" => new[] { "Inquiry.*", "Proforma.*" },
            "Business" => new[]
            {
                "Inquiry.*",
                "Proforma.*",
                "JobOrder.*",
                "DeliveryNote.*",
                "TaxInvoice.*",
                "Statement.*",
                "Reports.*",
                "AI.*"
            },
            "Factory" => new[]
            {
                "Inquiry.*",
                "Proforma.*",
                "JobOrder.*",
                "DeliveryNote.*",
                "TaxInvoice.*",
                "Statement.*",
                "Reports.*",
                "AI.*",
                "Cutting.*"
            },
            "Enterprise" => new[] { "*" },
            _ => new[] { "Inquiry.*", "Proforma.*" }
        };
    }

    /// <summary>What the operator fills in on the LICENSE KEY card.</summary>
    public sealed class LicenseKeyIssueRequest
    {
        public string Edition { get; set; } = "Business";
        public string CustomerId { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Notes { get; set; } = "";
        /// <summary>Days of validity. &lt;= 0 → never expires (Lifetime).</summary>
        public int DaysValid { get; set; } = 365;
        /// <summary>Customer PC ID (machine fingerprint). Empty → portable.</summary>
        public string? DeviceFingerprint { get; set; }
        public IReadOnlyList<string> Features { get; set; } = Array.Empty<string>();
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

    /// <summary>Result of <see cref="RunSelfTest"/>.</summary>
    public sealed class SelfTestResult
    {
        public bool IsValid { get; init; }
        public string Message { get; init; } = "";
    }

    /// <summary>
    /// Sign a license key and armor it into the single-line PGKEY1 block.
    /// The contract library signs (RSA-2048-PSS over the canonical bytes),
    /// re-reads and self-verifies the exact JSON, and only then is the
    /// block produced — so the returned key is guaranteed to verify
    /// against the same public half the app would use.
    /// </summary>
    public static LicenseKeyIssueResult GenerateLicenseKey(
        LicenseKeyIssueRequest request,
        IssuerKeyMaterial keyMaterial)
    {
        try
        {
            if (request == null)
                return new LicenseKeyIssueResult { Success = false, Message = "License key request is required." };

            keyMaterial.Validate();

            string edition = NormalizeEdition(request.Edition);
            bool isTrial = string.Equals(edition, "Trial", StringComparison.OrdinalIgnoreCase);
            bool isLifetime = request.DaysValid <= 0 && !isTrial;

            // Signed license TYPE mapping:
            //   Trial   → "Trial"        (fixed 30-day window)
            //   >0 days → "Subscription" (expires issuedAt + days)
            //   <=0     → "Lifetime"     (no expiry)
            string licenseType = isTrial ? "Trial" : (isLifetime ? "Lifetime" : "Subscription");
            int days = isTrial ? 30 : request.DaysValid;

            var features = (request.Features ?? Array.Empty<string>())
                .Where(f => !string.IsNullOrWhiteSpace(f))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            var gen = LicenseService.Create(
                new LicenseRequest
                {
                    CustomerId = request.CustomerId ?? "",
                    CustomerName = request.CompanyName ?? "",
                    Company = request.CompanyName ?? "",
                    Notes = request.Notes ?? "",
                    LicenseType = licenseType,
                    DaysValid = days,
                    Features = features,
                    DeviceBinding = string.IsNullOrWhiteSpace(request.DeviceFingerprint)
                        ? "Unbound"
                        : "SingleDevice",
                    DeviceFingerprint = request.DeviceFingerprint
                },
                keyMaterial.PrivateKeyXml,
                keyMaterial.KeyId);

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

            return new LicenseKeyIssueResult
            {
                Success = true,
                Message =
                    $"Signed {edition} license key · keyId {gen.KeyId} · " +
                    (isLifetime ? "no expiry" : $"valid {days} day(s)") +
                    (string.IsNullOrWhiteSpace(request.DeviceFingerprint)
                        ? " · portable (any PC)"
                        : " · bound to the supplied PC ID") +
                    (string.IsNullOrWhiteSpace(request.CompanyName)
                        ? "."
                        : " · for " + request.CompanyName + "."),
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

    /// <summary>
    /// Sign a synthetic 1-day key with the loaded issuer key and verify it
    /// against that key's own public half — proves the loaded .pgkey can
    /// actually sign AND that this build's contract code verifies what it
    /// signs. Nothing is written to disk.
    /// </summary>
    public static SelfTestResult RunSelfTest(IssuerKeyMaterial keyMaterial)
    {
        try
        {
            keyMaterial.Validate();

            var gen = LicenseService.Create(
                new LicenseRequest
                {
                    CustomerId = "SELFTEST",
                    Company = "Generator self test",
                    LicenseType = "Subscription",
                    DaysValid = 1,
                    Features = new() { "Inquiry.*" }
                },
                keyMaterial.PrivateKeyXml,
                keyMaterial.KeyId);

            if (!gen.Success || string.IsNullOrWhiteSpace(gen.LicenseJson))
                return new SelfTestResult { IsValid = false, Message = gen.Message ?? "Self-test failed." };

            var verify = LicenseService.VerifyAndValidate(
                gen.LicenseJson,
                appVersion: "2.0.0",
                utcNow: DateTime.UtcNow,
                trustedPublicKeys: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [keyMaterial.KeyId] = keyMaterial.PublicKeyXml
                });

            return verify.IsValid
                ? new SelfTestResult
                {
                    IsValid = true,
                    Message =
                        "Self-test passed. The loaded key signs licenses that verify correctly " +
                        "(keyId '" + keyMaterial.KeyId + "', fingerprint " + keyMaterial.Fingerprint + ")."
                }
                : new SelfTestResult { IsValid = false, Message = "Self-test failed: " + verify.Message };
        }
        catch (Exception ex)
        {
            return new SelfTestResult { IsValid = false, Message = "Self-test failed: " + ex.Message };
        }
    }

    public static string NormalizeEdition(string? text)
    {
        string value = (text ?? string.Empty).Trim();
        if (value.Equals("Trial", StringComparison.OrdinalIgnoreCase)) return "Trial";
        if (value.Equals("Basic", StringComparison.OrdinalIgnoreCase)) return "Basic";
        if (value.Equals("Business", StringComparison.OrdinalIgnoreCase)) return "Business";
        if (value.Equals("Factory", StringComparison.OrdinalIgnoreCase)) return "Factory";
        if (value.Equals("Enterprise", StringComparison.OrdinalIgnoreCase)) return "Enterprise";
        return "Business";
    }
}
