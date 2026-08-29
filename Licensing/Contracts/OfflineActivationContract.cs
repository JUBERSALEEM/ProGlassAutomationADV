using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProGlass.Licensing;

/// <summary>
/// Offline manual activation protocol constants. This protocol is separate
/// from the base license file contract so the activation flow can evolve
/// without silently changing the signed license document shape.
/// </summary>
public static class OfflineActivationProtocol
{
    public const int Version = 1;
}

/// <summary>
/// Customer-generated activation request. This is deterministic JSON but is
/// NOT a trusted artifact by itself; it becomes authoritative only when the
/// vendor signs a matching activation response.
/// </summary>
public sealed class OfflineActivationRequestDocument
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = OfflineActivationProtocol.Version;

    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = ProductIds.ProGlassAutomation;

    [JsonPropertyName("licenseId")]
    public string LicenseId { get; set; } = "";

    [JsonPropertyName("installationId")]
    public string InstallationId { get; set; } = "";

    [JsonPropertyName("machineFingerprint")]
    public string MachineFingerprint { get; set; } = "";

    [JsonPropertyName("requestNonce")]
    public string RequestNonce { get; set; } = "";

    [JsonPropertyName("requestedAtUtc")]
    public DateTime RequestedAtUtc { get; set; }

    [JsonPropertyName("applicationVersion")]
    public string ApplicationVersion { get; set; } = "";
}

/// <summary>
/// Offline policy enforced by the customer application after the signed
/// activation response has been verified.
/// </summary>
public sealed class OfflineActivationPolicy
{
    [JsonPropertyName("clockRollbackToleranceMinutes")]
    public int ClockRollbackToleranceMinutes { get; set; } = 1440;

    [JsonPropertyName("graceDays")]
    public int GraceDays { get; set; } = 0;

    [JsonPropertyName("readOnlyOnExpiry")]
    public bool ReadOnlyOnExpiry { get; set; }
}

/// <summary>
/// Vendor-signed activation response payload. This is the security-sensitive
/// object that binds a specific license to a specific install + machine +
/// activation request nonce.
/// </summary>
public sealed class OfflineActivationResponseDocument
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = OfflineActivationProtocol.Version;

    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = ProductIds.ProGlassAutomation;

    [JsonPropertyName("licenseId")]
    public string LicenseId { get; set; } = "";

    [JsonPropertyName("activationId")]
    public string ActivationId { get; set; } = "";

    [JsonPropertyName("installationId")]
    public string InstallationId { get; set; } = "";

    [JsonPropertyName("requestNonce")]
    public string RequestNonce { get; set; } = "";

    [JsonPropertyName("machineFingerprint")]
    public string MachineFingerprint { get; set; } = "";

    [JsonPropertyName("customerId")]
    public string CustomerId { get; set; } = "";

    [JsonPropertyName("companyName")]
    public string CompanyName { get; set; } = "";

    [JsonPropertyName("edition")]
    public string Edition { get; set; } = "Business";

    [JsonPropertyName("licenseType")]
    public string LicenseType { get; set; } = "Subscription";

    [JsonPropertyName("licenseStatus")]
    public string LicenseStatus { get; set; } = "Active";

    [JsonPropertyName("issuedAtUtc")]
    public DateTime IssuedAtUtc { get; set; }

    [JsonPropertyName("notBeforeUtc")]
    public DateTime NotBeforeUtc { get; set; }

    [JsonPropertyName("expiresAtUtc")]
    public DateTime? ExpiresAtUtc { get; set; }

    [JsonPropertyName("featureEntitlements")]
    public List<string> FeatureEntitlements { get; set; } = new();

    [JsonPropertyName("offlinePolicy")]
    public OfflineActivationPolicy OfflinePolicy { get; set; } = new();
}

/// <summary>
/// Signed offline activation envelope.
/// </summary>
public sealed class SignedOfflineActivationResponse
{
    [JsonPropertyName("protocolVersion")]
    public int ProtocolVersion { get; set; } = OfflineActivationProtocol.Version;

    [JsonPropertyName("payload")]
    public OfflineActivationResponseDocument Payload { get; set; } = new();

    [JsonPropertyName("signature")]
    public SignatureEnvelope Signature { get; set; } = new();
}

/// <summary>
/// Canonical serialization rules for offline activation request/response
/// payloads. The signed response payload uses this exact deterministic byte
/// stream for signature generation and verification.
/// </summary>
public static class OfflineActivationCanonical
{
    public static string SerializeRequest(OfflineActivationRequestDocument request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = true
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", request.ProtocolVersion);
            writer.WriteString("productId", request.ProductId ?? "");
            writer.WriteString("licenseId", request.LicenseId ?? "");
            writer.WriteString("installationId", request.InstallationId ?? "");
            writer.WriteString("machineFingerprint", request.MachineFingerprint ?? "");
            writer.WriteString("requestNonce", request.RequestNonce ?? "");
            writer.WriteString("requestedAtUtc", request.RequestedAtUtc.ToUniversalTime().ToString("O"));
            writer.WriteString("applicationVersion", request.ApplicationVersion ?? "");
            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static byte[] SerializeRequestUtf8(OfflineActivationRequestDocument request) =>
        System.Text.Encoding.UTF8.GetBytes(SerializeRequest(request));

    public static string SerializeResponse(OfflineActivationResponseDocument response)
    {
        if (response is null) throw new ArgumentNullException(nameof(response));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = true
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("protocolVersion", response.ProtocolVersion);
            writer.WriteString("productId", response.ProductId ?? "");
            writer.WriteString("licenseId", response.LicenseId ?? "");
            writer.WriteString("activationId", response.ActivationId ?? "");
            writer.WriteString("installationId", response.InstallationId ?? "");
            writer.WriteString("requestNonce", response.RequestNonce ?? "");
            writer.WriteString("machineFingerprint", response.MachineFingerprint ?? "");
            writer.WriteString("customerId", response.CustomerId ?? "");
            writer.WriteString("companyName", response.CompanyName ?? "");
            writer.WriteString("edition", response.Edition ?? "");
            writer.WriteString("licenseType", response.LicenseType ?? "");
            writer.WriteString("licenseStatus", response.LicenseStatus ?? "");
            writer.WriteString("issuedAtUtc", response.IssuedAtUtc.ToUniversalTime().ToString("O"));
            writer.WriteString("notBeforeUtc", response.NotBeforeUtc.ToUniversalTime().ToString("O"));
            if (response.ExpiresAtUtc.HasValue)
                writer.WriteString("expiresAtUtc", response.ExpiresAtUtc.Value.ToUniversalTime().ToString("O"));
            else
                writer.WriteNull("expiresAtUtc");

            writer.WritePropertyName("featureEntitlements");
            writer.WriteStartArray();
            foreach (var feature in (response.FeatureEntitlements ?? new List<string>())
                         .Where(x => !string.IsNullOrWhiteSpace(x))
                         .OrderBy(x => x, StringComparer.Ordinal))
            {
                writer.WriteStringValue(feature);
            }
            writer.WriteEndArray();

            writer.WritePropertyName("offlinePolicy");
            writer.WriteStartObject();
            writer.WriteNumber("clockRollbackToleranceMinutes", response.OfflinePolicy?.ClockRollbackToleranceMinutes ?? 0);
            writer.WriteNumber("graceDays", response.OfflinePolicy?.GraceDays ?? 0);
            writer.WriteBoolean("readOnlyOnExpiry", response.OfflinePolicy?.ReadOnlyOnExpiry ?? false);
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static byte[] SerializeResponseUtf8(OfflineActivationResponseDocument response) =>
        System.Text.Encoding.UTF8.GetBytes(SerializeResponse(response));
}
