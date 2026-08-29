using System.Text.Json;

namespace ProGlassAutomation.Services.Licensing;

/// <summary>
/// Safe, migration-only reader for legacy `.lic` artifacts.
///
/// This inspector NEVER treats the legacy artifact as trusted runtime
/// activation state. It only extracts user-facing metadata that helps the
/// operator/vendor re-issue the license under the secure offline activation
/// flow.
/// </summary>
internal static class LegacyLicenseMigrationInspector
{
    internal sealed class Result
    {
        public bool Exists { get; init; }
        public bool CanMigrate { get; init; }
        public string Message { get; init; } = "";
        public string LicenseId { get; init; } = "";
        public string CustomerId { get; init; } = "";
        public string CompanyName { get; init; } = "";
        public string Edition { get; init; } = "";
        public string MachineFingerprint { get; init; } = "";
        public bool HasSignatureField { get; init; }
        public int EnvelopeVersion { get; init; }
    }

    private const int MaxLegacyArtifactChars = 512_000;

    public static Result Inspect(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new Result
            {
                Exists = false,
                CanMigrate = false,
                Message = "No legacy license artifact was found."
            };
        }

        string json;
        try
        {
            json = File.ReadAllText(path);
        }
        catch (Exception ex)
        {
            return new Result
            {
                Exists = true,
                CanMigrate = false,
                Message = "Legacy license artifact exists but could not be read: " + ex.Message
            };
        }

        if (json.Length == 0)
        {
            return new Result
            {
                Exists = true,
                CanMigrate = false,
                Message = "Legacy license artifact is empty."
            };
        }

        if (json.Length > MaxLegacyArtifactChars)
        {
            return new Result
            {
                Exists = true,
                CanMigrate = false,
                Message = "Legacy license artifact exceeds the safe migration size limit."
            };
        }

        LegacyEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<LegacyEnvelope>(json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
        }
        catch (JsonException ex)
        {
            return new Result
            {
                Exists = true,
                CanMigrate = false,
                Message = "Legacy license artifact is malformed JSON: " + ex.Message
            };
        }

        if (envelope?.Payload == null)
        {
            return new Result
            {
                Exists = true,
                CanMigrate = false,
                Message = "Legacy license artifact is missing its payload block."
            };
        }

        string edition = envelope.Payload.Edition.ToString();
        string machine = envelope.Payload.MachineFingerprint ?? "";
        string scope = string.IsNullOrWhiteSpace(machine) ? "portable" : "machine-bound";
        return new Result
        {
            Exists = true,
            CanMigrate = true,
            Message =
                "Legacy license artifact detected. It is no longer trusted directly by the app, " +
                "but it can be re-issued under the secure offline activation flow.",
            LicenseId = envelope.Payload.LicenseId ?? "",
            CustomerId = envelope.Payload.CustomerId ?? "",
            CompanyName = envelope.Payload.CompanyName ?? "",
            Edition = edition,
            MachineFingerprint = machine,
            HasSignatureField = !string.IsNullOrWhiteSpace(envelope.Signature),
            EnvelopeVersion = envelope.EnvelopeVersion
        };
    }

    public static string BuildMigrationPrompt(string path)
    {
        var result = Inspect(path);
        if (!result.Exists)
            return "No legacy license artifact was found.";

        if (!result.CanMigrate)
            return result.Message;

        string id = string.IsNullOrWhiteSpace(result.LicenseId) ? "unknown" : result.LicenseId;
        string company = string.IsNullOrWhiteSpace(result.CompanyName) ? "unknown company" : result.CompanyName;
        string customer = string.IsNullOrWhiteSpace(result.CustomerId) ? "unknown customer" : result.CustomerId;
        string scope = string.IsNullOrWhiteSpace(result.MachineFingerprint)
            ? "portable"
            : "machine-bound";

        return result.Message + " " +
               "Detected legacy artifact: " +
               "licenseId=" + id + ", " +
               "customer=" + customer + ", " +
               "company=" + company + ", " +
               "edition=" + result.Edition + ", " +
               "scope=" + scope + ", " +
               "envelopeVersion=" + result.EnvelopeVersion + ". " +
               "Generate a fresh activation request from this machine and ask the vendor to re-issue it as a signed offline activation response.";
    }

    private sealed class LegacyEnvelope
    {
        public int EnvelopeVersion { get; set; }
        public LicensePayload? Payload { get; set; }
        public string Signature { get; set; } = "";
    }
}
