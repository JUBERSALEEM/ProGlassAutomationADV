using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProGlass.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// External key-file model for the vendor-side generator.
///
/// The private signing key must not be embedded in source or shipped inside
/// distributed customer binaries. The generator therefore loads it from an
/// operator-held file outside the repository.
/// </summary>
public sealed class IssuerKeyMaterial
{
    [JsonPropertyName("formatVersion")]
    public int FormatVersion { get; set; } = 1;

    [JsonPropertyName("purpose")]
    public string Purpose { get; set; } = "Production";

    [JsonPropertyName("algorithm")]
    public string Algorithm { get; set; } = LicenseCrypto.Algorithm;

    [JsonPropertyName("keyId")]
    public string KeyId { get; set; } = "production-key-2026";

    [JsonPropertyName("publicKeyXml")]
    public string PublicKeyXml { get; set; } = "";

    [JsonPropertyName("privateKeyXml")]
    public string PrivateKeyXml { get; set; } = "";

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; set; }

    public static IssuerKeyMaterial Generate(string keyId, string purpose)
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        return new IssuerKeyMaterial
        {
            Purpose = string.IsNullOrWhiteSpace(purpose) ? "Production" : purpose.Trim(),
            KeyId = string.IsNullOrWhiteSpace(keyId) ? "production-key-2026" : keyId.Trim(),
            PublicKeyXml = pub,
            PrivateKeyXml = priv,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static IssuerKeyMaterial LoadFromFile(string path)
    {
        string json = File.ReadAllText(path);
        var value = JsonSerializer.Deserialize<IssuerKeyMaterial>(json,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true,
                ReadCommentHandling = JsonCommentHandling.Skip
            });

        if (value == null)
            throw new InvalidOperationException("Issuer key file could not be parsed.");

        value.Validate();
        return value;
    }

    public void SaveToFile(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path,
            JsonSerializer.Serialize(this, new JsonSerializerOptions
            {
                WriteIndented = true
            }));
    }

    public void Validate()
    {
        if (FormatVersion != 1)
            throw new InvalidOperationException("Unsupported issuer key-file version: " + FormatVersion);
        if (!string.Equals(Algorithm, LicenseCrypto.Algorithm, StringComparison.Ordinal))
            throw new InvalidOperationException("Unsupported issuer-key algorithm: " + Algorithm);
        if (string.IsNullOrWhiteSpace(KeyId))
            throw new InvalidOperationException("Issuer key file is missing keyId.");
        if (string.IsNullOrWhiteSpace(PublicKeyXml) || string.IsNullOrWhiteSpace(PrivateKeyXml))
            throw new InvalidOperationException("Issuer key file is missing public/private key material.");

        using var rsa = RSA.Create();
        rsa.FromXmlString(PrivateKeyXml);
        string derived = rsa.ToXmlString(includePrivateParameters: false);
        if (!string.Equals(derived, PublicKeyXml, StringComparison.Ordinal))
            throw new InvalidOperationException("Issuer key file public/private key pair does not match.");
    }

    public string Fingerprint => LicenseCrypto.Fingerprint(PublicKeyXml);
}
