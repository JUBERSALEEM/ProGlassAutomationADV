using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ProGlass.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// Vendor-side offline activation response generator.
///
/// This tool no longer embeds a production private key and no longer emits a
/// whole-license base64 blob. It signs structured offline activation response
/// JSON with an operator-supplied external key file.
/// </summary>
public static class LicenseGeneratorCore
{
    public const string ActivationRequestExtension = ".pgrequest";
    public const string ActivationResponseExtension = ".lic";
    public const string IssuerKeyExtension = ".pgkey";

    public static string DefaultOutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ProGlassAutomation",
        "OfflineLicensing");

    public sealed class IssueRequest
    {
        public OfflineActivationRequestDocument ActivationRequest { get; set; } = new();
        public string CustomerId { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Notes { get; set; } = "";
        public string Edition { get; set; } = "Business";
        public string LicenseType { get; set; } = "Subscription";
        public string LicenseStatus { get; set; } = "Active";
        public int ValidDays { get; set; } = 365;
        public int GraceDays { get; set; } = 7;
        public bool ReadOnlyOnExpiry { get; set; } = true;
        public IReadOnlyList<string> Features { get; set; } = Array.Empty<string>();
    }

    public sealed class IssueResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        public string OutputPath { get; init; } = "";
        public string ResponseJson { get; init; } = "";
        public OfflineActivationResponseDocument? Payload { get; init; }
        public string? SigningKeyId { get; init; }
    }

    public sealed class InspectionResult
    {
        public bool IsValid { get; init; }
        public string Message { get; init; } = "";
        public string ResponseJson { get; init; } = "";
        public OfflineActivationResponseDocument? Payload { get; init; }
        public string? SigningKeyId { get; init; }
    }

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

    public static OfflineActivationRequestReadResult ReadActivationRequest(string text) =>
        OfflineActivationService.ReadRequest(text);

    public static OfflineActivationRequestReadResult ReadActivationRequestFile(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new OfflineActivationRequestReadResult
            {
                Success = false,
                ErrorCode = OfflineActivationErrorCode.EmptyRequest,
                Message = "Activation request file does not exist."
            };
        }

        return OfflineActivationService.ReadRequest(File.ReadAllText(path));
    }

    public static IssueResult GenerateActivationResponse(
        IssueRequest request,
        IssuerKeyMaterial keyMaterial,
        string? outputPath = null)
    {
        try
        {
            if (request?.ActivationRequest == null)
                return Fail("Activation request is required.");

            keyMaterial.Validate();
            if (string.IsNullOrWhiteSpace(request.CustomerId))
                return Fail("Customer code is required.");
            if (string.IsNullOrWhiteSpace(request.CompanyName))
                return Fail("Company name is required.");

            var requestCheck = OfflineActivationService.ReadRequest(
                OfflineActivationService.CreateRequestJson(request.ActivationRequest));
            if (!requestCheck.Success || requestCheck.Request == null)
                return Fail(requestCheck.Message ?? "Activation request is invalid.");

            var payload = BuildResponsePayload(request, requestCheck.Request);
            string json = OfflineActivationService.SignResponseJson(payload, keyMaterial.PrivateKeyXml, keyMaterial.KeyId);

            string finalPath = string.IsNullOrWhiteSpace(outputPath)
                ? BuildDefaultOutputPath(payload)
                : outputPath!;
            Directory.CreateDirectory(Path.GetDirectoryName(finalPath) ?? DefaultOutputFolder);
            File.WriteAllText(finalPath, json, new System.Text.UTF8Encoding(false));

            var verify = OfflineActivationService.VerifyResponse(
                json,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [keyMaterial.KeyId] = keyMaterial.PublicKeyXml
                },
                expectedProductId: payload.ProductId,
                expectedInstallationId: payload.InstallationId,
                expectedMachineFingerprint: payload.MachineFingerprint,
                expectedRequestNonce: payload.RequestNonce,
                utcNow: DateTime.UtcNow);

            if (!verify.IsValid)
                return Fail("Self-verification failed: " + verify.Message, finalPath, json, payload, keyMaterial.KeyId);

            return new IssueResult
            {
                Success = true,
                Message = BuildSuccessMessage(payload, keyMaterial, finalPath),
                OutputPath = finalPath,
                ResponseJson = json,
                Payload = payload,
                SigningKeyId = keyMaterial.KeyId
            };
        }
        catch (Exception ex)
        {
            return Fail("Generation failed: " + ex.Message);
        }
    }

    // ────────────────────────────────────────────────────────────────────
    // SIMPLE LICENSE KEY (2026 workflow simplification)
    //
    // The primary licensing flow: the vendor generates a signed
    // .proglasslicense key with the operator's external issuer key and
    // hands the customer ONE copy-paste text block (PGKEY1.…). No
    // activation request, no license reference, no PC-ID round-trip.
    // An optional PC ID (machine fingerprint) can be supplied to bind
    // the key to a single machine.
    // ────────────────────────────────────────────────────────────────────

    public sealed class LicenseKeyIssueRequest
    {
        public string Edition { get; set; } = "Business";
        public string CustomerId { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Notes { get; set; } = "";
        public int DaysValid { get; set; } = 365;   // <= 0 → no expiry (Lifetime)
        public string? DeviceFingerprint { get; set; }  // empty → portable
        public IReadOnlyList<string> Features { get; set; } = Array.Empty<string>();
    }

    public sealed class LicenseKeyIssueResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        public string ArmoredKey { get; init; } = "";
        public string? OutputPath { get; init; }
        public string? SigningKeyId { get; init; }
        public LicenseDocument? Document { get; init; }
    }

    /// <summary>
    /// Generate a signed license key and armor it into the single-line
    /// PGKEY1 block the customer pastes into the app. The contract
    /// library signs (RSA-2048-PSS) and self-verifies the document
    /// exactly as the app will; the armor is transport only.
    /// </summary>
    public static LicenseKeyIssueResult GenerateLicenseKey(
        LicenseKeyIssueRequest request,
        IssuerKeyMaterial keyMaterial,
        string? outputPath = null)
    {
        try
        {
            if (request == null)
                return new LicenseKeyIssueResult { Success = false, Message = "License key request is required." };

            keyMaterial.Validate();

            string edition = NormalizeEdition(request.Edition);
            bool isTrial = string.Equals(edition, "Trial", StringComparison.OrdinalIgnoreCase);
            bool isLifetime = request.DaysValid <= 0 && !isTrial;

            // License TYPE mapping for the signed document:
            //   Trial        → "Trial"    (fixed 30-day window)
            //   days > 0     → "Subscription" (expires issuedAt + days)
            //   days <= 0    → "Lifetime" (no expiry)
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
                keyMaterial.KeyId,
                outputPath);

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

            string armored = LicenseKeyArmor.Encode(gen.LicenseJson);

            return new LicenseKeyIssueResult
            {
                Success = true,
                Message =
                    $"Signed {edition} license key generated for " +
                    (string.IsNullOrWhiteSpace(request.CompanyName) ? "(unnamed customer)" : request.CompanyName) +
                    (isLifetime ? " — no expiry." : $" — valid {days} day(s).") +
                    (string.IsNullOrWhiteSpace(request.DeviceFingerprint)
                        ? " Portable: the key works on any PC."
                        : " Bound to the supplied PC ID."),
                ArmoredKey = armored,
                OutputPath = gen.OutputPath,
                SigningKeyId = gen.KeyId,
                Document = gen.Document
            };
        }
        catch (Exception ex)
        {
            return new LicenseKeyIssueResult { Success = false, Message = "License key generation failed: " + ex.Message };
        }
    }

    public static InspectionResult InspectResponseFile(
        string path,
        IReadOnlyDictionary<string, string> trustedPublicKeys)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new InspectionResult
            {
                IsValid = false,
                Message = "Response file does not exist."
            };
        }

        return InspectResponseText(File.ReadAllText(path), trustedPublicKeys);
    }

    public static InspectionResult InspectResponseText(
        string text,
        IReadOnlyDictionary<string, string> trustedPublicKeys)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new InspectionResult
                {
                    IsValid = false,
                    Message = "Response text is empty."
                };
            }

            var parsed = JsonSerializer.Deserialize<SignedOfflineActivationResponse>(text,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });

            if (parsed?.Payload == null)
            {
                return new InspectionResult
                {
                    IsValid = false,
                    Message = "Response JSON is missing payload data.",
                    ResponseJson = text
                };
            }

            var verify = OfflineActivationService.VerifyResponse(
                text,
                trustedPublicKeys,
                expectedProductId: parsed.Payload.ProductId,
                expectedInstallationId: parsed.Payload.InstallationId,
                expectedMachineFingerprint: parsed.Payload.MachineFingerprint,
                expectedRequestNonce: parsed.Payload.RequestNonce,
                utcNow: DateTime.UtcNow);

            return new InspectionResult
            {
                IsValid = verify.IsValid,
                Message = verify.IsValid
                    ? BuildInspectionMessage(parsed.Payload, parsed.Signature.KeyId)
                    : (verify.Message ?? "Response verification failed."),
                ResponseJson = text,
                Payload = parsed.Payload,
                SigningKeyId = parsed.Signature.KeyId
            };
        }
        catch (Exception ex)
        {
            return new InspectionResult
            {
                IsValid = false,
                Message = "Inspection failed: " + ex.Message,
                ResponseJson = text
            };
        }
    }

    public static InspectionResult RunSelfTest(IssuerKeyMaterial keyMaterial)
    {
        keyMaterial.Validate();

        string tempPath = Path.Combine(
            Path.GetTempPath(),
            "proglass-offline-selftest-" + Guid.NewGuid().ToString("N") + ActivationResponseExtension);

        try
        {
            var request = new OfflineActivationRequestDocument
            {
                ProductId = ProductIds.ProGlassAutomation,
                LicenseId = "SELFTEST-LIC-001",
                InstallationId = "SELFTEST-INSTALL-001",
                MachineFingerprint = "SELFTEST-MACHINE-ABC",
                RequestNonce = Guid.NewGuid().ToString("N"),
                RequestedAtUtc = DateTime.UtcNow,
                ApplicationVersion = "2.0.0"
            };

            var result = GenerateActivationResponse(
                new IssueRequest
                {
                    ActivationRequest = request,
                    CustomerId = "SELFTEST",
                    CompanyName = "Generator self test",
                    Edition = "Business",
                    LicenseType = "Subscription",
                    LicenseStatus = "Active",
                    ValidDays = 30,
                    Features = DefaultFeatures("Business")
                },
                keyMaterial,
                tempPath);

            return new InspectionResult
            {
                IsValid = result.Success,
                Message = result.Success
                    ? "Self-test passed. The generator created a signed offline activation response with keyId '" + keyMaterial.KeyId + "'."
                    : result.Message,
                ResponseJson = result.ResponseJson,
                Payload = result.Payload,
                SigningKeyId = result.SigningKeyId
            };
        }
        finally
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); } catch { }
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

    private static OfflineActivationResponseDocument BuildResponsePayload(
        IssueRequest request,
        OfflineActivationRequestDocument activationRequest)
    {
        string edition = NormalizeEdition(request.Edition);
        string licenseType = string.IsNullOrWhiteSpace(request.LicenseType)
            ? "Subscription"
            : request.LicenseType.Trim();

        DateTime issued = DateTime.UtcNow;
        DateTime? expires = RequiresExpiry(licenseType)
            ? issued.AddDays(Math.Max(1, Math.Min(3650, request.ValidDays)))
            : null;

        return new OfflineActivationResponseDocument
        {
            ProtocolVersion = OfflineActivationProtocol.Version,
            ProductId = activationRequest.ProductId,
            LicenseId = activationRequest.LicenseId,
            ActivationId = Guid.NewGuid().ToString("N"),
            InstallationId = activationRequest.InstallationId,
            RequestNonce = activationRequest.RequestNonce,
            MachineFingerprint = activationRequest.MachineFingerprint,
            CustomerId = (request.CustomerId ?? string.Empty).Trim(),
            CompanyName = (request.CompanyName ?? string.Empty).Trim(),
            Edition = edition,
            LicenseType = licenseType,
            LicenseStatus = string.IsNullOrWhiteSpace(request.LicenseStatus) ? "Active" : request.LicenseStatus.Trim(),
            IssuedAtUtc = issued,
            NotBeforeUtc = issued,
            ExpiresAtUtc = expires,
            FeatureEntitlements = NormalizeFeatures(request.Features, edition).ToList(),
            Notes = (request.Notes ?? string.Empty).Trim(),
            OfflinePolicy = new OfflineActivationPolicy
            {
                ClockRollbackToleranceMinutes = 1440,
                GraceDays = Math.Max(0, Math.Min(90, request.GraceDays)),
                ReadOnlyOnExpiry = request.ReadOnlyOnExpiry
            }
        };
    }

    private static IReadOnlyList<string> NormalizeFeatures(
        IReadOnlyList<string>? requested,
        string edition)
    {
        var parsed = (requested ?? Array.Empty<string>())
            .SelectMany(x => (x ?? string.Empty).Split(new[] { ',', ';', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return parsed.Count > 0
            ? parsed
            : DefaultFeatures(edition).ToList();
    }

    private static bool RequiresExpiry(string licenseType)
    {
        return !string.Equals(licenseType, "Lifetime", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(licenseType, "PerpetualVersion", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildDefaultOutputPath(OfflineActivationResponseDocument payload)
    {
        string company = Sanitise(payload.CompanyName.Length == 0 ? payload.CustomerId : payload.CompanyName);
        string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return Path.Combine(DefaultOutputFolder, company + "-" + stamp + ActivationResponseExtension);
    }

    private static IssueResult Fail(
        string message,
        string? outputPath = null,
        string? responseJson = null,
        OfflineActivationResponseDocument? payload = null,
        string? keyId = null)
    {
        return new IssueResult
        {
            Success = false,
            Message = message,
            OutputPath = outputPath ?? string.Empty,
            ResponseJson = responseJson ?? string.Empty,
            Payload = payload,
            SigningKeyId = keyId
        };
    }

    private static string BuildSuccessMessage(
        OfflineActivationResponseDocument payload,
        IssuerKeyMaterial keyMaterial,
        string path)
    {
        return payload.Edition + " offline activation response ready · keyId " +
               keyMaterial.KeyId + " · fingerprint " + keyMaterial.Fingerprint +
               " · expires " + (payload.ExpiresAtUtc?.ToString("yyyy-MM-dd") ?? "(no expiry)") +
               " · file " + path;
    }

    private static string BuildInspectionMessage(OfflineActivationResponseDocument payload, string? keyId)
    {
        return "Signed offline activation response verified · keyId " +
               (keyId ?? "(none)") + " · edition " + payload.Edition +
               " · customer " + payload.CustomerId +
               " · install " + payload.InstallationId;
    }

    private static string Sanitise(string? name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        return new string((string.IsNullOrWhiteSpace(name) ? "Activation" : name.Trim())
            .Select(c => invalid.Contains(c) ? '_' : c)
            .ToArray());
    }
}
