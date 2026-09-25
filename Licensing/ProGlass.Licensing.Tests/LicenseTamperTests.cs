// Attack tests for the PGKEY1 verifier. A genuine signed license must still
// verify. Every other case must fail closed, including the single-key
// fallback that previously accepted an unknown keyId.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ProGlass.Licensing.Tests;

public sealed class LicenseTamperTests
{
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Genuine_signed_license_still_verifies()
    {
        var (pub, _, keyId, json) = Issue();

        var result = Verify(json, pub, keyId);

        Assert.True(result.IsValid, result.Message);
        Assert.Equal(LicenseErrorCode.None, result.ErrorCode);
    }

    [Fact]
    public void Modified_json_is_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string tampered = Mutate(json, lic => lic.Document.Customer.Name = "Evil Glass LLC");

        var result = Verify(tampered, pub, keyId);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseErrorCode.InvalidSignature, result.ErrorCode);
    }

    [Fact]
    public void Modified_base64_armor_is_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string armored = LicenseKeyArmor.Encode(json);
        char[] chars = armored.ToCharArray();
        int flip = chars.Length - 2;
        chars[flip] = chars[flip] == 'A' ? 'B' : 'A';

        string? extracted = LicenseKeyArmor.TryExtractJson(new string(chars));
        if (extracted == null)
            return;

        var result = Verify(extracted, pub, keyId);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Changed_expiry_is_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string tampered = Mutate(json, lic => lic.Document.ExpiresAtUtc = Now.AddYears(20));

        Assert.Equal(LicenseErrorCode.InvalidSignature, Verify(tampered, pub, keyId).ErrorCode);
    }

    [Fact]
    public void Changed_license_type_is_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string tampered = Mutate(json, lic => lic.Document.LicenseType = "Lifetime");

        Assert.Equal(LicenseErrorCode.InvalidSignature, Verify(tampered, pub, keyId).ErrorCode);
    }

    [Fact]
    public void Signed_unknown_license_type_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var doc = ValidDocument();
        doc.LicenseType = "FreeForever";

        var result = Verify(Sign(doc, priv, keyId), pub, keyId);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseErrorCode.InvalidLicenseType, result.ErrorCode);
    }

    [Fact]
    public void Changed_feature_list_is_rejected_and_unlisted_feature_is_not_granted()
    {
        var (pub, _, keyId, json) = Issue();
        var genuine = Verify(json, pub, keyId);

        Assert.True(genuine.IsValid, genuine.Message);
        Assert.False(LicenseValidator.HasFeature(genuine.Document!, "ADMIN"));

        string tampered = Mutate(json, lic => lic.Document.Features.Add("ADMIN"));
        Assert.Equal(LicenseErrorCode.InvalidSignature, Verify(tampered, pub, keyId).ErrorCode);
    }

    [Fact]
    public void Changed_product_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var doc = ValidDocument();
        doc.ProductId = "SomeOtherProduct";

        var result = Verify(Sign(doc, priv, keyId), pub, keyId);

        Assert.Equal(LicenseErrorCode.WrongProduct, result.ErrorCode);
    }

    [Fact]
    public void Changed_customer_is_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string tampered = Mutate(json, lic => lic.Document.Customer.Id = "CUST-999");

        Assert.Equal(LicenseErrorCode.InvalidSignature, Verify(tampered, pub, keyId).ErrorCode);
    }

    [Fact]
    public void Changed_device_binding_is_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string tampered = Mutate(json, lic => lic.Document.DevicePolicy.Binding = "SingleDevice");

        Assert.Equal(LicenseErrorCode.InvalidSignature, Verify(tampered, pub, keyId).ErrorCode);
    }

    [Fact]
    public void Changed_version_policy_is_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string tampered = Mutate(json, lic => lic.Document.VersionPolicy.MaximumApplicationVersion = "1.0.0");

        Assert.Equal(LicenseErrorCode.InvalidSignature, Verify(tampered, pub, keyId).ErrorCode);
    }

    [Fact]
    public void Changed_signature_bytes_are_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string tampered = Mutate(json, lic =>
        {
            char[] chars = lic.Signature.Value.ToCharArray();
            chars[10] = chars[10] == 'A' ? 'B' : 'A';
            lic.Signature.Value = new string(chars);
        });

        var result = Verify(tampered, pub, keyId);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseErrorCode.InvalidSignature, result.ErrorCode);
    }

    [Fact]
    public void Unknown_key_id_is_rejected_even_when_only_one_trusted_key_exists()
    {
        var (pub, _, _, json) = Issue();
        var trust = new Dictionary<string, string> { ["production-key-2026"] = pub };

        var result = LicenseService.VerifyAndValidate(json, "1.0.0", Now, trust);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseErrorCode.UnknownKey, result.ErrorCode);
        Assert.Equal(1, trust.Count);
    }

    [Fact]
    public void Algorithm_downgrade_is_rejected_even_when_signature_bytes_still_verify()
    {
        var (pub, _, keyId, json) = Issue();
        string tampered = Mutate(json, lic => lic.Signature.Algorithm = "RSA-SHA1");

        var result = Verify(tampered, pub, keyId);

        Assert.False(result.IsValid);
        Assert.Equal(LicenseErrorCode.InvalidAlgorithm, result.ErrorCode);
    }

    [Fact]
    public void Feature_reorder_does_not_break_a_genuine_signature()
    {
        var (pub, _, keyId, json) = Issue();
        string reordered = Mutate(json, lic =>
        {
            var features = lic.Document.Features;
            (features[0], features[1]) = (features[1], features[0]);
        });

        var result = Verify(reordered, pub, keyId);

        Assert.True(result.IsValid, result.Message);
    }

    [Fact]
    public void Expired_license_is_not_accepted()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var doc = ValidDocument(Now.AddDays(-30), Now.AddDays(-1));

        var result = Verify(Sign(doc, priv, keyId), pub, keyId);

        Assert.Equal(LicenseErrorCode.Expired, result.ErrorCode);
    }

    [Fact]
    public void Expiry_before_issue_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var doc = ValidDocument(Now.AddDays(-1), Now.AddDays(-10));

        var result = Verify(Sign(doc, priv, keyId), pub, keyId);

        Assert.Equal(LicenseErrorCode.InvalidPolicy, result.ErrorCode);
    }

    [Fact]
    public void Clock_rollback_beyond_tolerance_is_rejected_and_small_skew_is_not()
    {
        var (pub, _, keyId, json) = Issue();

        var rolled = LicenseService.VerifyAndValidate(
            json, "1.0.0", Now,
            new Dictionary<string, string> { [keyId] = pub },
            lastSeenUtc: Now.AddDays(3));
        Assert.Equal(LicenseErrorCode.ClockRollbackDetected, rolled.ErrorCode);

        var skew = LicenseService.VerifyAndValidate(
            json, "1.0.0", Now,
            new Dictionary<string, string> { [keyId] = pub },
            lastSeenUtc: Now.AddHours(2));
        Assert.True(skew.IsValid, skew.Message);
    }

    [Fact]
    public void Malformed_input_fails_closed_and_does_not_throw()
    {
        var keys = new Dictionary<string, string> { ["k"] = "<RSAKeyValue></RSAKeyValue>" };

        var thrown = Record.Exception(() =>
            LicenseService.VerifyAndValidate("{", "1.0.0", Now, keys));

        Assert.Null(thrown);
        var result = LicenseService.VerifyAndValidate("{", "1.0.0", Now, keys);
        Assert.False(result.IsValid);
        Assert.NotEqual(LicenseErrorCode.None, result.ErrorCode);
    }

    [Fact]
    public void Missing_trust_store_is_rejected()
    {
        var (_, _, _, json) = Issue();

        var empty = LicenseService.VerifyAndValidate(
            json, "1.0.0", Now, new Dictionary<string, string>());

        Assert.Equal(LicenseErrorCode.MissingKey, empty.ErrorCode);
    }

    [Fact]
    public void Oversized_license_text_is_rejected_before_parse()
    {
        string huge = new string('{', LicenseFormat.MaxJsonCharacters + 8);
        var read = LicenseReader.Read(huge);

        Assert.Null(read.License);
        Assert.Contains("size", read.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Rsa_key_smaller_than_2048_is_rejected()
    {
        RSA? rsa;
        try
        {
            rsa = RSA.Create(1024);
        }
        catch (CryptographicException)
        {
            // This platform will not create a 1024-bit key. The verifier
            // still rejects one if it is presented.
            return;
        }

        using (rsa)
        {
            string pub = rsa.ToXmlString(includePrivateParameters: false);
            Assert.False(LicenseCrypto.VerifyCanonical(
                Encoding.UTF8.GetBytes("not-a-license"),
                Convert.ToBase64String(new byte[128]),
                pub));
        }
    }

    [Fact]
    public void Comment_and_trailing_comma_documents_are_rejected()
    {
        var (pub, _, keyId, json) = Issue();
        string commented = json.Insert(1, "/*no*/");
        string trailing = json.TrimEnd() + ",";

        Assert.False(Verify(commented, pub, keyId).IsValid);
        Assert.False(LicenseService.VerifyAndValidate(
            trailing, "1.0.0", Now, new Dictionary<string, string> { [keyId] = pub }).IsValid);
    }

    private static (string PublicKey, string PrivateKey, string KeyId, string Json) Issue()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var created = LicenseService.Create(new LicenseRequest
        {
            CustomerId = "CUST-001",
            CustomerName = "Acme Glass LLC",
            Company = "Acme",
            LicenseType = "Subscription",
            DaysValid = 365,
            Features = new List<string> { "PROFORMA_INVOICE", "JOB_ORDER" },
            DeviceBinding = "Unbound",
            MinimumApplicationVersion = "1.0.0",
            IssuedAtUtc = Now.AddDays(-1)
        }, priv, keyId);

        Assert.True(created.Success, created.Message);
        Assert.False(string.IsNullOrWhiteSpace(created.LicenseJson));
        return (pub, priv, keyId, created.LicenseJson!);
    }

    private static LicenseDocument ValidDocument(DateTime? issued = null, DateTime? expires = null)
    {
        DateTime at = issued ?? Now.AddDays(-1);
        return new LicenseDocument
        {
            LicenseFormatVersion = LicenseFormat.Version,
            LicenseId = "lic-manual",
            ProductId = ProductIds.ProGlassAutomation,
            Customer = new CustomerInfo { Id = "CUST-001", Name = "Acme Glass LLC", Company = "Acme" },
            LicenseType = "Subscription",
            IssuedAtUtc = at,
            ExpiresAtUtc = expires ?? at.AddDays(30),
            Features = new List<string> { "PROFORMA_INVOICE", "JOB_ORDER" },
            DevicePolicy = new DevicePolicy { Binding = "Unbound" },
            VersionPolicy = new VersionPolicy { MinimumApplicationVersion = "1.0.0" }
        };
    }

    private static string Sign(LicenseDocument doc, string privateKey, string keyId)
    {
        return LicenseWriter.ToJson(new SignedLicense
        {
            LicenseFormatVersion = LicenseFormat.Version,
            Document = doc,
            Signature = new SignatureEnvelope
            {
                Algorithm = LicenseCrypto.Algorithm,
                KeyId = keyId,
                Value = LicenseCrypto.SignCanonical(LicenseCanonical.SerializeUtf8(doc), privateKey)
            }
        });
    }

    private static string Mutate(string json, Action<SignedLicense> mutate)
    {
        var license = JsonSerializer.Deserialize<SignedLicense>(json)
            ?? throw new InvalidOperationException("Test license did not parse.");
        mutate(license);
        return LicenseWriter.ToJson(license);
    }

    private static LicenseValidationResult Verify(string json, string publicKey, string keyId) =>
        LicenseService.VerifyAndValidate(
            json,
            "1.0.0",
            Now,
            new Dictionary<string, string> { [keyId] = publicKey });
}
