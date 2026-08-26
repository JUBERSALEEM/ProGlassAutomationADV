// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/Contracts/LicenseContract.cs
// PROJECT: ProGlass.Licensing
// MODULE: Stable license contract — the versioned on-disk model
//
// This is the PERMANENT public contract between the LicenseGenerator
// (which signs) and ProGlassAutomation (which verifies). It is a
// versioned, canonical-serializable, digitally-signed document.
//
// HARD RULES THAT MAKE IT A CONTRACT (not a quick format):
//   • licenseFormatVersion is a SEPARATE concept from any application
//     version. Bump it ONLY when the serialized shape changes in a
//     breaking way; old readers continue to understand older
//     format versions (see LicenseReader).
//   • productId is permanent for ProGlass Automation: `PROGLASS_AUTOMATION`.
//     It NEVER changes on an app version bump / UI change / DB migration.
//   • versionPolicy controls which APP VERSIONS the license is accepted
//     by. The DEFAULT is "unlimited future compatibility" — a 2026 license
//     must keep working on 2027 / 2028 / 1.1 / 2.0 builds unless the
//     issuer deliberately restricted it.
//   • features are DATA, not code. Unknown future feature identifiers in a
//     license must be tolerated (never crash); the app only checks the
//     features it knows about.
//   • The signature is over the CANONICAL serialization of this model
//     (deterministic key order, no whitespace) so verification is exact.
// ═══════════════════════════════════════════════════════════════════════

using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProGlass.Licensing;

/// <summary>
/// Stable machine identity of the software this license is for.
/// Permanent for ProGlass Automation — never changes across releases.
/// </summary>
public static class ProductIds
{
    public const string ProGlassAutomation = "PROGLASS_AUTOMATION";
}

/// <summary>
/// Supported license types. Each has deterministic validation rules
/// (see <see cref="LicenseValidator"/>). Values are stable strings.
/// </summary>
public enum LicenseType
{
    Trial,
    Subscription,
    Lifetime,
    PerpetualVersion,
    Enterprise
}

/// <summary>
/// Device (machine) binding policy. "Unbound" is the default and needs no
/// customer fingerprint; "SingleDevice"/"MultiDevice" require a fingerprint
/// (and, for multi-device, a device limit).
/// </summary>
public sealed class DevicePolicy
{
    [JsonPropertyName("binding")]
    public string Binding { get; set; } = "Unbound";   // Unbound | SingleDevice | MultiDevice

    [JsonPropertyName("deviceLimit")]
    public int? DeviceLimit { get; set; }              // only for MultiDevice

    [JsonPropertyName("fingerprint")]
    public string? Fingerprint { get; set; }           // set by Single/Multi binding

    public bool IsUnbound => string.Equals(Binding, "Unbound", StringComparison.OrdinalIgnoreCase);
    public bool IsBound => !IsUnbound;
}

/// <summary>
/// Application-version acceptance policy. This is intentionally restrictive
/// only when the issuer chose it. The DEFAULT is:
///   minimum = "1.0.0", maximum = null  →  accepted by 1.0, 1.1, 2.0, 3.0 …
/// (unlimited future compatibility). The app NEVER auto-rejects a newer
/// build just because the license was generated for an older one.
/// </summary>
public sealed class VersionPolicy
{
    [JsonPropertyName("minimumApplicationVersion")]
    public string? MinimumApplicationVersion { get; set; } = "1.0.0";

    [JsonPropertyName("maximumApplicationVersion")]
    public string? MaximumApplicationVersion { get; set; }

    [JsonPropertyName("allowedMajorVersions")]
    public List<int>? AllowedMajorVersions { get; set; }
}

/// <summary>
/// Public identity of the customer. Keep to business data — no unnecessary
/// sensitive personal information.
/// </summary>
public sealed class CustomerInfo
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("company")]
    public string? Company { get; set; }

    [JsonPropertyName("contact")]
    public string? Contact { get; set; }

    [JsonPropertyName("email")]
    public string? Email { get; set; }

    [JsonPropertyName("notes")]
    public string? Notes { get; set; }
}

/// <summary>
/// The versioned, signed license document. This is what the generator
/// serializes (canonically), signs, and writes to a .proglasslicense file.
/// </summary>
public sealed class LicenseDocument
{
    [JsonPropertyName("licenseFormatVersion")]
    public int LicenseFormatVersion { get; set; } = LicenseFormat.Version;

    [JsonPropertyName("licenseId")]
    public string LicenseId { get; set; } = "";

    [JsonPropertyName("productId")]
    public string ProductId { get; set; } = ProductIds.ProGlassAutomation;

    [JsonPropertyName("customer")]
    public CustomerInfo Customer { get; set; } = new();

    [JsonPropertyName("licenseType")]
    public string LicenseType { get; set; } = "Subscription";

    [JsonPropertyName("issuedAtUtc")]
    public DateTime IssuedAtUtc { get; set; }

    [JsonPropertyName("expiresAtUtc")]
    public DateTime? ExpiresAtUtc { get; set; }      // null = no expiry (Lifetime / Perpetual)

    [JsonPropertyName("features")]
    public List<string> Features { get; set; } = new();

    [JsonPropertyName("devicePolicy")]
    public DevicePolicy DevicePolicy { get; set; } = new();

    [JsonPropertyName("versionPolicy")]
    public VersionPolicy VersionPolicy { get; set; } = new();
}

/// <summary>
/// The signature envelope appended to a signed license file. Carries a
/// key ID so future key rotation can happen without invalidating licenses
/// signed by an older key.
/// </summary>
public sealed class SignatureEnvelope
{
    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = "RSA-PSS-SHA256";

    [JsonPropertyName("keyId")]
    public string KeyId { get; set; } = "unknown";

    [JsonPropertyName("value")]
    public string Value { get; set; } = "";          // base64 signature
}

/// <summary>
/// The signed license FILE: the canonical document + signature envelope.
/// </summary>
public sealed class SignedLicense
{
    [JsonPropertyName("licenseFormatVersion")]
    public int LicenseFormatVersion { get; set; } = LicenseFormat.Version;

    [JsonPropertyName("document")]
    public LicenseDocument Document { get; set; } = new();

    [JsonPropertyName("signature")]
    public SignatureEnvelope Signature { get; set; } = new();
}

/// <summary>
/// Canonical serialization contract. This is the EXACT byte sequence that is
/// signed and re-verified — deterministic, documented, whitespace-free.
/// </summary>
public static class LicenseCanonical
{
    /// <summary>
    /// Produce the canonical JSON for a <see cref="LicenseDocument"/>. Two
    /// identical documents always serialise to the same bytes. The signature
    /// is computed over UTF-8 of exactly this string.
    /// </summary>
    public static string Serialize(LicenseDocument doc)
    {
        if (doc is null) throw new ArgumentNullException(nameof(doc));

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = false,
            SkipValidation = true
        }))
        {
            WriteDocument(writer, doc);
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    public static byte[] SerializeUtf8(LicenseDocument doc) =>
        System.Text.Encoding.UTF8.GetBytes(Serialize(doc));

    // Build the document in a FIXED, documented member order so the same
    // logical document always yields the same bytes regardless of the
    // runtime's JSON default ordering.
    private static void WriteDocument(Utf8JsonWriter w, LicenseDocument d)
    {
        w.WriteStartObject();
        w.WriteNumber("licenseFormatVersion", d.LicenseFormatVersion);
        w.WriteString("licenseId", d.LicenseId);
        w.WriteString("productId", d.ProductId);

        // customer
        w.WritePropertyName("customer");
        w.WriteStartObject();
        w.WriteString("id", d.Customer.Id);
        w.WriteString("name", d.Customer.Name);
        w.WriteString("company", d.Customer.Company ?? "");
        w.WriteString("contact", d.Customer.Contact ?? "");
        w.WriteString("email", d.Customer.Email ?? "");
        w.WriteString("notes", d.Customer.Notes ?? "");
        w.WriteEndObject();

        w.WriteString("licenseType", d.LicenseType);
        w.WriteString("issuedAtUtc", d.IssuedAtUtc.ToUniversalTime().ToString("O"));

        if (d.ExpiresAtUtc.HasValue)
            w.WriteString("expiresAtUtc", d.ExpiresAtUtc.Value.ToUniversalTime().ToString("O"));
        else
            w.WriteNull("expiresAtUtc");

        // features — sorted for determinism
        w.WritePropertyName("features");
        w.WriteStartArray();
        foreach (var f in (d.Features ?? new List<string>()).OrderBy(x => x, StringComparer.Ordinal))
            w.WriteStringValue(f ?? "");
        w.WriteEndArray();

        // devicePolicy
        w.WritePropertyName("devicePolicy");
        w.WriteStartObject();
        w.WriteString("binding", d.DevicePolicy.Binding);
        if (d.DevicePolicy.DeviceLimit.HasValue)
            w.WriteNumber("deviceLimit", d.DevicePolicy.DeviceLimit.Value);
        else
            w.WriteNull("deviceLimit");
        w.WriteString("fingerprint", d.DevicePolicy.Fingerprint ?? "");
        w.WriteEndObject();

        // versionPolicy
        w.WritePropertyName("versionPolicy");
        w.WriteStartObject();
        w.WriteString("minimumApplicationVersion", d.VersionPolicy.MinimumApplicationVersion ?? "");
        w.WriteString("maximumApplicationVersion", d.VersionPolicy.MaximumApplicationVersion ?? "");
        if (d.VersionPolicy.AllowedMajorVersions is { Count: > 0 })
        {
            w.WritePropertyName("allowedMajorVersions");
            w.WriteStartArray();
            foreach (var v in d.VersionPolicy.AllowedMajorVersions.OrderBy(x => x))
                w.WriteNumberValue(v);
            w.WriteEndArray();
        }
        else
        {
            w.WriteNull("allowedMajorVersions");
        }
        w.WriteEndObject();

        w.WriteEndObject();
    }
}
