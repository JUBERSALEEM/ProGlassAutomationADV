using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ProGlassAutomation.Services.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// Vendor-side app-compatible .lic issuer.
///
/// This tool intentionally emits the EXACT legacy .lic envelope the current
/// ProGlassAutomation app already accepts in Settings/About activation. That
/// means:
///   • payload shape = <see cref="LicensePayload"/>
///   • canonicalization/signature = <see cref="LicenseSigner"/>
///   • file extension = .lic
///   • pasted activation text = base64 of that same JSON envelope
///
/// The app verifier trusts the matching public key as a secondary production
/// key, so licenses generated here activate without rebuilding the app.
/// </summary>
public static class LicenseGeneratorCore
{
    public const string LicenseFileExtension = ".lic";
    public const int UnlimitedPortableActivations = 9999;

    public static string DefaultOutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ProGlassAutomation",
        "Licenses");

    public sealed class IssueRequest
    {
        public string CustomerId { get; set; } = "";
        public string CompanyName { get; set; } = "";
        public string Notes { get; set; } = "";
        public string LicenseIdOverride { get; set; } = "";
        public LicenseEdition Edition { get; set; } = LicenseEdition.Business;
        public int ValidDays { get; set; } = 365;
        public int MaxActivations { get; set; } = 5;
        public string? MachineFingerprint { get; set; }
        public IReadOnlyList<string> Features { get; set; } = Array.Empty<string>();
        public DateTime? IssuedAtUtc { get; set; }
    }

    public sealed class IssueResult
    {
        public bool Success { get; init; }
        public string Message { get; init; } = "";
        public string OutputPath { get; init; } = "";
        public string LicenseJson { get; init; } = "";
        public string PasteKey { get; init; } = "";
        public LicensePayload? Payload { get; init; }
    }

    public sealed class InspectionResult
    {
        public bool IsValid { get; init; }
        public string Message { get; init; } = "";
        public string LicenseJson { get; init; } = "";
        public string PasteKey { get; init; } = "";
        public LicensePayload? Payload { get; init; }
    }

    private sealed class Envelope
    {
        public int EnvelopeVersion { get; set; }
        public LicensePayload? Payload { get; set; }
        public string Signature { get; set; } = "";
    }

    public static IReadOnlyList<string> DefaultFeatures(LicenseEdition edition)
    {
        return edition switch
        {
            LicenseEdition.Trial => new[] { "*" },
            LicenseEdition.Basic => new[]
            {
                "Inquiry.*",
                "Proforma.*"
            },
            LicenseEdition.Business => new[]
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
            LicenseEdition.Factory => new[]
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
            LicenseEdition.Enterprise => new[] { "*" },
            _ => new[] { "*" }
        };
    }

    public static int SuggestedActivationLimit(LicenseEdition edition)
    {
        return edition switch
        {
            LicenseEdition.Trial => 1,
            LicenseEdition.Basic => 1,
            LicenseEdition.Business => 5,
            LicenseEdition.Factory => UnlimitedPortableActivations,
            LicenseEdition.Enterprise => UnlimitedPortableActivations,
            _ => 1
        };
    }

    public static string PublicKeyFingerprint => ComputeFingerprint(AppCompatibleIssuerKeys.PublicKeyXml);

    public static IssueResult GenerateLicense(IssueRequest request, string? outputPath = null)
    {
        try
        {
            if (request == null)
                return Fail("No request supplied.");

            string customerId = (request.CustomerId ?? "").Trim();
            string company = (request.CompanyName ?? "").Trim();
            if (customerId.Length == 0)
                return Fail("Customer ID is required.");
            if (company.Length == 0)
                return Fail("Company name is required.");

            var payload = BuildPayload(request);
            string finalPath = string.IsNullOrWhiteSpace(outputPath)
                ? BuildDefaultOutputPath(payload)
                : outputPath!;

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath) ?? DefaultOutputFolder);
            LicenseSigner.SignToFile(payload, AppCompatibleIssuerKeys.PrivateKeyXml, finalPath);

            string json = File.ReadAllText(finalPath, Encoding.UTF8);
            var verify = InspectText(json);
            if (!verify.IsValid)
            {
                return Fail(
                    "Generator self-check failed: " + verify.Message,
                    finalPath,
                    json,
                    Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
                    payload);
            }

            return new IssueResult
            {
                Success = true,
                Message = BuildSuccessMessage(payload, finalPath),
                OutputPath = finalPath,
                LicenseJson = json,
                PasteKey = verify.PasteKey,
                Payload = payload
            };
        }
        catch (Exception ex)
        {
            return Fail("Generation failed: " + ex.Message);
        }
    }

    public static InspectionResult InspectFile(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                return new InspectionResult
                {
                    IsValid = false,
                    Message = "Selected file does not exist."
                };
            }

            string text = File.ReadAllText(path, Encoding.UTF8);
            return InspectText(text);
        }
        catch (Exception ex)
        {
            return new InspectionResult
            {
                IsValid = false,
                Message = "Inspection failed: " + ex.Message
            };
        }
    }

    public static InspectionResult InspectText(string text)
    {
        try
        {
            if (!TryDecodeLicenseJson(text, out string json, out string decodeError))
            {
                return new InspectionResult
                {
                    IsValid = false,
                    Message = decodeError
                };
            }

            var envelope = JsonSerializer.Deserialize<Envelope>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (envelope?.Payload == null)
            {
                return new InspectionResult
                {
                    IsValid = false,
                    LicenseJson = json,
                    Message = "License JSON is missing payload data."
                };
            }

            if (envelope.EnvelopeVersion != LicenseSigner.EnvelopeSchemaVersion)
            {
                return new InspectionResult
                {
                    IsValid = false,
                    LicenseJson = json,
                    Payload = envelope.Payload,
                    PasteKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
                    Message = "Unsupported envelope version " + envelope.EnvelopeVersion + "."
                };
            }

            if (string.IsNullOrWhiteSpace(envelope.Signature))
            {
                return new InspectionResult
                {
                    IsValid = false,
                    LicenseJson = json,
                    Payload = envelope.Payload,
                    PasteKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
                    Message = "License JSON is missing the signature."
                };
            }

            byte[] signature = Convert.FromBase64String(envelope.Signature);
            byte[] message = Encoding.UTF8.GetBytes(envelope.Payload.ToCanonicalJson());

            using var rsa = RSA.Create();
            rsa.FromXmlString(AppCompatibleIssuerKeys.PublicKeyXml);
            bool ok = rsa.VerifyData(message, signature,
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
                || rsa.VerifyData(message, signature,
                        HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

            return new InspectionResult
            {
                IsValid = ok,
                LicenseJson = json,
                PasteKey = Convert.ToBase64String(Encoding.UTF8.GetBytes(json)),
                Payload = envelope.Payload,
                Message = ok
                    ? BuildInspectionMessage(envelope.Payload)
                    : "Signature does not match the app-compatible signing key."
            };
        }
        catch (Exception ex)
        {
            return new InspectionResult
            {
                IsValid = false,
                Message = "Inspection failed: " + ex.Message
            };
        }
    }

    public static InspectionResult RunSelfTest()
    {
        string temp = Path.Combine(
            Path.GetTempPath(),
            "proglass-license-selftest-" + Guid.NewGuid().ToString("N") + LicenseFileExtension);

        try
        {
            var result = GenerateLicense(new IssueRequest
            {
                CustomerId = "SELFTEST",
                CompanyName = "Generator self test",
                Edition = LicenseEdition.Business,
                ValidDays = 30,
                MaxActivations = 2,
                Features = DefaultFeatures(LicenseEdition.Business),
                Notes = "Self-test probe generated by the vendor tool."
            }, temp);

            return new InspectionResult
            {
                IsValid = result.Success,
                Message = result.Success
                    ? "Self-test passed. The generator produced an app-compatible .lic signed with fingerprint " + PublicKeyFingerprint + "."
                    : result.Message,
                LicenseJson = result.LicenseJson,
                PasteKey = result.PasteKey,
                Payload = result.Payload
            };
        }
        finally
        {
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }

    public static LicenseEdition ParseEdition(string? text)
    {
        return Enum.TryParse<LicenseEdition>((text ?? "").Trim(), true, out var edition)
            ? edition
            : LicenseEdition.Business;
    }

    private static IssueResult Fail(
        string message,
        string? outputPath = null,
        string? json = null,
        string? pasteKey = null,
        LicensePayload? payload = null)
    {
        return new IssueResult
        {
            Success = false,
            Message = message,
            OutputPath = outputPath ?? "",
            LicenseJson = json ?? "",
            PasteKey = pasteKey ?? "",
            Payload = payload
        };
    }

    private static LicensePayload BuildPayload(IssueRequest request)
    {
        DateTime issued = (request.IssuedAtUtc ?? DateTime.UtcNow).ToUniversalTime();
        int days = request.ValidDays <= 0 ? 365 : request.ValidDays;
        if (days > 3650) days = 3650;

        string? fingerprint = string.IsNullOrWhiteSpace(request.MachineFingerprint)
            ? null
            : request.MachineFingerprint.Trim();

        int maxActivations = fingerprint != null
            ? 1
            : Math.Max(1, request.MaxActivations);

        var features = NormalizeFeatures(request.Features, request.Edition);

        return new LicensePayload
        {
            LicenseId = string.IsNullOrWhiteSpace(request.LicenseIdOverride)
                ? GenerateLicenseId(request.CustomerId)
                : request.LicenseIdOverride.Trim(),
            Edition = request.Edition,
            CustomerId = request.CustomerId.Trim(),
            CompanyName = request.CompanyName.Trim(),
            IssuedAt = issued,
            ExpiresAt = issued.AddDays(days),
            MaxActivations = maxActivations,
            Features = features,
            MachineFingerprint = fingerprint,
            Notes = (request.Notes ?? "").Trim(),
            TrialSource = string.Empty,
            TrialExpiresAt = default
        };
    }

    private static IReadOnlyList<string> NormalizeFeatures(
        IReadOnlyList<string> requested,
        LicenseEdition edition)
    {
        var parsed = (requested ?? Array.Empty<string>())
            .SelectMany(x => (x ?? "").Split(new[] { ',', ';', '\n', '\r' },
                StringSplitOptions.RemoveEmptyEntries))
            .Select(x => x.Trim())
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return parsed.Count > 0 ? parsed : DefaultFeatures(edition).ToList();
    }

    private static string BuildDefaultOutputPath(LicensePayload payload)
    {
        string company = SanitizeFileName(string.IsNullOrWhiteSpace(payload.CompanyName)
            ? payload.CustomerId
            : payload.CompanyName);
        string stamp = DateTime.UtcNow.ToString("yyyy-MM-dd");
        return Path.Combine(DefaultOutputFolder, company + "-" + stamp + LicenseFileExtension);
    }

    private static string GenerateLicenseId(string customerId)
    {
        string prefix = SanitizeFileName(customerId).ToUpperInvariant();
        if (prefix.Length == 0) prefix = "CUST";
        return prefix + "-" + DateTime.UtcNow.ToString("yyyyMMdd") + "-" +
               Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
    }

    private static bool TryDecodeLicenseJson(string input, out string json, out string error)
    {
        json = "";
        error = "";

        string text = (input ?? "").Trim();
        text = text.TrimStart('\uFEFF', '\u200B', ' ', '\t', '\r', '\n');
        if (text.Length == 0)
        {
            error = "No license text supplied.";
            return false;
        }

        if (text.StartsWith("{", StringComparison.Ordinal))
        {
            json = text;
            return true;
        }

        try
        {
            string b64 = text
                .Replace("-----BEGIN LICENSE-----", "", StringComparison.OrdinalIgnoreCase)
                .Replace("-----END LICENSE-----", "", StringComparison.OrdinalIgnoreCase)
                .Replace("-----BEGIN PROGLASS LICENSE-----", "", StringComparison.OrdinalIgnoreCase)
                .Replace("-----END PROGLASS LICENSE-----", "", StringComparison.OrdinalIgnoreCase);
            b64 = string.Concat(b64.Where(c => !char.IsWhiteSpace(c)));
            byte[] bytes = Convert.FromBase64String(b64);
            string decoded = Encoding.UTF8.GetString(bytes);
            if (!decoded.TrimStart().StartsWith("{", StringComparison.Ordinal))
                throw new FormatException("Base64 decoded value is not JSON.");
            json = decoded;
            return true;
        }
        catch (Exception ex)
        {
            error = "The text is neither raw .lic JSON nor a base64 activation key: " + ex.Message;
            return false;
        }
    }

    private static string BuildSuccessMessage(LicensePayload payload, string path)
    {
        string mode = string.IsNullOrWhiteSpace(payload.MachineFingerprint)
            ? $"portable · {payload.MaxActivations} activation(s)"
            : "machine-bound · 1 activation";
        return payload.Edition + " license ready · " + mode +
               " · expires " + payload.ExpiresAt.ToString("yyyy-MM-dd") +
               " · file " + path;
    }

    private static string BuildInspectionMessage(LicensePayload payload)
    {
        string mode = string.IsNullOrWhiteSpace(payload.MachineFingerprint)
            ? $"portable · {payload.MaxActivations} activation(s)"
            : "machine-bound";
        return "App-compatible .lic verified · " + payload.Edition +
               " · customer " + (payload.CustomerId.Length == 0 ? "(blank)" : payload.CustomerId) +
               " · " + mode;
    }

    private static bool TryVerifyWithAnyKey(
        IEnumerable<string> keys,
        byte[] message,
        byte[] signature)
    {
        foreach (var key in keys ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(key))
                continue;

            using var rsa = RSA.Create();
            rsa.FromXmlString(key);
            bool ok = rsa.VerifyData(message, signature,
                         HashAlgorithmName.SHA256, RSASignaturePadding.Pss)
                || rsa.VerifyData(message, signature,
                         HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            if (ok) return true;
        }

        return false;
    }

    private static string ComputeFingerprint(string publicKeyXml)
    {
        using var rsa = RSA.Create();
        rsa.FromXmlString(publicKeyXml);
        var p = rsa.ExportParameters(false);
        byte[] hash = SHA256.HashData(p.Modulus ?? Array.Empty<byte>());
        var sb = new StringBuilder(16);
        for (int i = 0; i < Math.Min(8, hash.Length); i++)
            sb.Append(hash[i].ToString("x2"));
        return sb.ToString();
    }

    private static string SanitizeFileName(string? text)
    {
        string source = string.IsNullOrWhiteSpace(text) ? "LICENSE" : text.Trim();
        var invalid = Path.GetInvalidFileNameChars();
        return new string(source.Select(c => invalid.Contains(c) ? '_' : c).ToArray());
    }
}
