// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/LicenseContractTests.cs
// PROJECT: ProGlass.Licensing.Tests
// MODULE: Contract tests — the permanent licensing behavior
//
// These are the tests that future ProGlassAutomation builds must keep
// passing. They pin the stable license contract: canonical serialization,
// sign/verify, tamper detection, version policy (incl. unlimited future
// compatibility), expiry, product binding, machine binding, key rotation and
// error codes. They never touch WPF, a database, or app services.
// ═══════════════════════════════════════════════════════════════════════

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ProGlass.Licensing.Tests;

public sealed class LicenseContractTests
{
    private const string AppVersion = "1.0.0";

    private static LicenseRequest BuildRequest(string type = "Subscription") => new()
    {
        CustomerId = "CUST-001",
        CustomerName = "Acme Glass LLC",
        Company = "Acme",
        LicenseType = type,
        DaysValid = 365,
        Features = new() { "PROFORMA_INVOICE", "JOB_ORDER", "REPORTS" },
        DeviceBinding = "Unbound",
        MinimumApplicationVersion = "1.0.0"
    };

    [Fact]
    public void Canonical_serialization_is_deterministic()
    {
        var doc = new LicenseDocument
        {
            LicenseId = Guid.NewGuid().ToString(),
            ProductId = ProductIds.ProGlassAutomation,
            Customer = new CustomerInfo { Id = "C", Name = "N" },
            LicenseType = "Subscription",
            IssuedAtUtc = new DateTime(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc),
            Features = new() { "REPORTS", "PROFORMA_INVOICE" }
        };

        string a = LicenseCanonical.Serialize(doc);
        string b = LicenseCanonical.Serialize(doc);
        Assert.Equal(a, b); // identical document => identical bytes

        // Feature order must be sorted (deterministic).
        Assert.Contains("\"PROFORMA_INVOICE\",\"REPORTS\"", a);
    }

    [Fact]
    public void Generate_then_verify_is_valid()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var result = LicenseService.Create(BuildRequest(), priv, keyId);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.Document);

        var verify = LicenseService.VerifyAndValidate(
            LicenseWriter.ToJson(new SignedLicense
            {
                LicenseFormatVersion = LicenseFormat.Version,
                Document = result.Document,
                Signature = new SignatureEnvelope
                {
                    Algorithm = LicenseCrypto.Algorithm,
                    KeyId = keyId,
                    Value = ReSign(result.Document, priv)
                }
            }),
            AppVersion, DateTime.UtcNow,
            new Dictionary<string, string> { [keyId] = pub });

        Assert.True(verify.IsValid, verify.Message);
    }

    [Fact]
    public void Tampered_customer_invalidates_signature()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var result = LicenseService.Create(BuildRequest(), priv, keyId);
        Assert.True(result.Success);

        var tampered = JsonSerializer.Deserialize<LicenseDocument>(
            JsonSerializer.Serialize(result.Document));
        tampered!.Customer.Name = "Hacked Company";   // tamper

        var verify = LicenseService.VerifyAndValidate(
            LicenseWriter.ToJson(new SignedLicense
            {
                LicenseFormatVersion = LicenseFormat.Version,
                Document = tampered,
                Signature = new SignatureEnvelope { Algorithm = LicenseCrypto.Algorithm, KeyId = keyId, Value = ReSign(result.Document, priv) }
            }),
            AppVersion, DateTime.UtcNow,
            new Dictionary<string, string> { [keyId] = pub });

        Assert.False(verify.IsValid);
        Assert.Equal(LicenseErrorCode.InvalidSignature, verify.ErrorCode);
    }

    [Fact]
    public void Unlimited_future_version_is_accepted()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        // Default request: min 1.0.0, max null.
        var result = LicenseService.Create(BuildRequest(), priv, keyId);
        Assert.True(result.Success);

        // A 2026 license must be accepted by 1.0, 1.1, 2.0, 3.0 builds.
        foreach (var v in new[] { "1.0", "1.1", "1.2", "2.0", "2.1", "3.0" })
        {
            var check = LicenseService.VerifyAndValidate(
                LicenseWriter.ToJson(new SignedLicense { LicenseFormatVersion = 1, Document = result.Document, Signature = new SignatureEnvelope { Algorithm = LicenseCrypto.Algorithm, KeyId = keyId, Value = ReSign(result.Document, priv) } }),
                v, DateTime.UtcNow,
                new Dictionary<string, string> { [keyId] = pub });
            Assert.True(check.IsValid, "app " + v + " must accept it: " + check.Message);
        }
    }

    [Fact]
    public void Expired_license_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var req = BuildRequest();
        req.DaysValid = 1;
        var result = LicenseService.Create(req, priv, keyId);
        Assert.True(result.Success);

        var verify = LicenseService.VerifyAndValidate(
            LicenseWriter.ToJson(new SignedLicense { LicenseFormatVersion = 1, Document = result.Document, Signature = new SignatureEnvelope { Algorithm = LicenseCrypto.Algorithm, KeyId = keyId, Value = ReSign(result.Document, priv) } }),
            AppVersion, DateTime.UtcNow.AddDays(30),
            new Dictionary<string, string> { [keyId] = pub });

        Assert.False(verify.IsValid);
        Assert.Equal(LicenseErrorCode.Expired, verify.ErrorCode);
    }

    [Fact]
    public void Wrong_product_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var result = LicenseService.Create(BuildRequest(), priv, keyId);
        Assert.True(result.Success);
        result.Document!.ProductId = "SOME_OTHER_PRODUCT";

        var verify = LicenseService.VerifyAndValidate(
            LicenseWriter.ToJson(new SignedLicense { LicenseFormatVersion = 1, Document = result.Document, Signature = new SignatureEnvelope { Algorithm = LicenseCrypto.Algorithm, KeyId = keyId, Value = ReSign(result.Document, priv) } }),
            AppVersion, DateTime.UtcNow,
            new Dictionary<string, string> { [keyId] = pub });

        Assert.False(verify.IsValid);
        Assert.Equal(LicenseErrorCode.WrongProduct, verify.ErrorCode);
    }

    [Fact]
    public void Machine_bound_license_rejects_other_fingerprint()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var req = BuildRequest();
        req.DeviceBinding = "SingleDevice";
        req.DeviceFingerprint = "expected-fp-123";
        var result = LicenseService.Create(req, priv, keyId);
        Assert.True(result.Success);

        var signed = new SignedLicense { LicenseFormatVersion = 1, Document = result.Document, Signature = new SignatureEnvelope { Algorithm = LicenseCrypto.Algorithm, KeyId = keyId, Value = ReSign(result.Document, priv) } };

        var ok = LicenseService.VerifyAndValidate(LicenseWriter.ToJson(signed), AppVersion, DateTime.UtcNow,
            new Dictionary<string, string> { [keyId] = pub }, currentFingerprint: "expected-fp-123");
        Assert.True(ok.IsValid);

        var bad = LicenseService.VerifyAndValidate(LicenseWriter.ToJson(signed), AppVersion, DateTime.UtcNow,
            new Dictionary<string, string> { [keyId] = pub }, currentFingerprint: "other-fp");
        Assert.False(bad.IsValid);
        Assert.Equal(LicenseErrorCode.MachineMismatch, bad.ErrorCode);
    }

    [Fact]
    public void Version_range_restriction_is_enforced()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var req = BuildRequest();
        req.MaximumApplicationVersion = "1.9";
        var result = LicenseService.Create(req, priv, keyId);
        Assert.True(result.Success);
        var signed = new SignedLicense { LicenseFormatVersion = 1, Document = result.Document, Signature = new SignatureEnvelope { Algorithm = LicenseCrypto.Algorithm, KeyId = keyId, Value = ReSign(result.Document, priv) } };
        var ks = new Dictionary<string, string> { [keyId] = pub };

        Assert.True(LicenseService.VerifyAndValidate(LicenseWriter.ToJson(signed), "1.5", DateTime.UtcNow, ks).IsValid);
        Assert.Equal(LicenseErrorCode.VersionNotAllowed,
            LicenseService.VerifyAndValidate(LicenseWriter.ToJson(signed), "2.0", DateTime.UtcNow, ks).ErrorCode);
    }

    [Fact]
    public void Unknown_key_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var result = LicenseService.Create(BuildRequest(), priv, keyId);
        Assert.True(result.Success);
        var signed = new SignedLicense { LicenseFormatVersion = 1, Document = result.Document, Signature = new SignatureEnvelope { Algorithm = LicenseCrypto.Algorithm, KeyId = keyId, Value = ReSign(result.Document, priv) } };

        // A different trusted-key ring that does NOT include the signing key.
        // Use more than one key so the verifier cannot fall back to a single
        // configured key when the keyId is missing from the ring.
        var (otherPub1, _) = LicenseCrypto.GenerateKeyPair();
        var (otherPub2, _) = LicenseCrypto.GenerateKeyPair();
        var verify = LicenseService.VerifyAndValidate(LicenseWriter.ToJson(signed), AppVersion, DateTime.UtcNow,
            new Dictionary<string, string>
            {
                ["other-key-1"] = otherPub1,
                ["other-key-2"] = otherPub2
            });
        Assert.False(verify.IsValid);
        Assert.Equal(LicenseErrorCode.UnknownKey, verify.ErrorCode);
    }

    [Fact]
    public void Unsupported_format_version_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var result = LicenseService.Create(BuildRequest(), priv, keyId);
        var signed = new SignedLicense { LicenseFormatVersion = 999, Document = result.Document, Signature = new SignatureEnvelope { Algorithm = LicenseCrypto.Algorithm, KeyId = keyId, Value = ReSign(result.Document, priv) } };
        var read = LicenseReader.Read(LicenseWriter.ToJson(signed));
        Assert.Equal(LicenseReadStatus.UnsupportedFormat, read.Status);
    }

    [Fact]
    public void Unknown_future_feature_is_tolerated()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var req = BuildRequest();
        req.Features = new() { "PROFORMA_INVOICE", "AI_ANALYTICS", "CLOUD_SYNC" }; // future feature IDs
        var result = LicenseService.Create(req, priv, keyId);
        Assert.True(result.Success);

        // The app only checks known features and does NOT crash on unknown ones.
        Assert.True(LicenseValidator.HasFeature(result.Document!, "PROFORMA_INVOICE"));
        Assert.False(LicenseValidator.HasFeature(result.Document!, "SOME_UNKNOWN"));
    }

    [Fact]
    public void VerifyCanonical_rejects_pkcs1_v15_signatures()
    {
        // 2026-08-29 security cross-check: the contract accepts PSS ONLY.
        // A v1.5 signature over the same canonical bytes must be
        // rejected even with the correct public key — the legacy v1.5
        // acceptance path was attack surface, not compatibility.
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        byte[] canonical = Encoding.UTF8.GetBytes("ProGlass security cross-check");

        using var rsa = RSA.Create(LicenseCrypto.KeySizeBits);
        rsa.FromXmlString(priv);
        byte[] v15 = rsa.SignData(canonical, LicenseCrypto.Hash, RSASignaturePadding.Pkcs1);

        Assert.False(LicenseCrypto.VerifyCanonical(canonical, Convert.ToBase64String(v15), pub));
        Assert.True(LicenseCrypto.VerifyCanonical(canonical, LicenseCrypto.SignCanonical(canonical, priv), pub));
    }

    // ── helper: re-sign a document with a private key ─────────────────
    private static string ReSign(LicenseDocument doc, string priv) =>
        LicenseCrypto.SignCanonical(LicenseCanonical.SerializeUtf8(doc), priv);
}
