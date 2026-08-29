using Xunit;

namespace ProGlass.Licensing.Tests;

public sealed class OfflineActivationProtocolTests
{
    private static OfflineActivationRequestDocument BuildRequest() => new()
    {
        ProductId = ProductIds.ProGlassAutomation,
        LicenseId = "LIC-001",
        InstallationId = "INST-001",
        MachineFingerprint = "MACHINE-ABC",
        RequestNonce = "NONCE-123",
        RequestedAtUtc = new DateTime(2026, 8, 29, 10, 30, 0, DateTimeKind.Utc),
        ApplicationVersion = "2.0.0"
    };

    private static OfflineActivationResponseDocument BuildResponse() => new()
    {
        ProductId = ProductIds.ProGlassAutomation,
        LicenseId = "LIC-001",
        ActivationId = "ACT-001",
        InstallationId = "INST-001",
        MachineFingerprint = "MACHINE-ABC",
        RequestNonce = "NONCE-123",
        CustomerId = "CUST-001",
        CompanyName = "Acme Glass LLC",
        Edition = "Business",
        LicenseType = "Subscription",
        LicenseStatus = "Active",
        IssuedAtUtc = new DateTime(2026, 8, 29, 11, 0, 0, DateTimeKind.Utc),
        NotBeforeUtc = new DateTime(2026, 8, 29, 11, 0, 0, DateTimeKind.Utc),
        ExpiresAtUtc = new DateTime(2027, 8, 29, 11, 0, 0, DateTimeKind.Utc),
        FeatureEntitlements = new() { "REPORTS", "PROFORMA_INVOICE", "JOB_ORDER" },
        OfflinePolicy = new OfflineActivationPolicy
        {
            ClockRollbackToleranceMinutes = 1440,
            GraceDays = 7,
            ReadOnlyOnExpiry = true
        }
    };

    [Fact]
    public void Request_canonicalization_is_deterministic()
    {
        var request = BuildRequest();
        string a = OfflineActivationCanonical.SerializeRequest(request);
        string b = OfflineActivationCanonical.SerializeRequest(request);

        Assert.Equal(a, b);
        Assert.Contains("\"requestNonce\":\"NONCE-123\"", a);
    }

    [Fact]
    public void Response_canonicalization_sorts_feature_entitlements()
    {
        var response = BuildResponse();
        response.FeatureEntitlements = new() { "ZED", "ALPHA", "MID" };

        string canonical = OfflineActivationCanonical.SerializeResponse(response);

        Assert.Contains("\"featureEntitlements\":[\"ALPHA\",\"MID\",\"ZED\"]", canonical);
    }

    [Fact]
    public void Signed_response_verifies_for_expected_bindings()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);

        string json = OfflineActivationService.SignResponseJson(BuildResponse(), priv, keyId);
        var verify = OfflineActivationService.VerifyResponse(
            json,
            new Dictionary<string, string> { [keyId] = pub },
            expectedProductId: ProductIds.ProGlassAutomation,
            expectedInstallationId: "INST-001",
            expectedMachineFingerprint: "MACHINE-ABC",
            expectedRequestNonce: "NONCE-123",
            utcNow: new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc));

        Assert.True(verify.IsValid, verify.Message);
        Assert.Equal(keyId, verify.SigningKeyId);
    }

    [Fact]
    public void Tampering_any_signed_field_invalidates_signature()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);

        string json = OfflineActivationService.SignResponseJson(BuildResponse(), priv, keyId);
        var parsed = System.Text.Json.JsonSerializer.Deserialize<SignedOfflineActivationResponse>(json)!;
        parsed.Payload.CompanyName = "Tampered Company";

        string tamperedJson = System.Text.Json.JsonSerializer.Serialize(parsed, new System.Text.Json.JsonSerializerOptions
        {
            WriteIndented = true
        });

        var verify = OfflineActivationService.VerifyResponse(
            tamperedJson,
            new Dictionary<string, string> { [keyId] = pub },
            expectedProductId: ProductIds.ProGlassAutomation,
            expectedInstallationId: "INST-001",
            expectedMachineFingerprint: "MACHINE-ABC",
            expectedRequestNonce: "NONCE-123",
            utcNow: DateTime.UtcNow);

        Assert.False(verify.IsValid);
        Assert.Equal(OfflineActivationErrorCode.InvalidSignature, verify.ErrorCode);
    }

    [Fact]
    public void Nonce_mismatch_is_rejected_after_signature_verification()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);

        string json = OfflineActivationService.SignResponseJson(BuildResponse(), priv, keyId);
        var verify = OfflineActivationService.VerifyResponse(
            json,
            new Dictionary<string, string> { [keyId] = pub },
            expectedProductId: ProductIds.ProGlassAutomation,
            expectedInstallationId: "INST-001",
            expectedMachineFingerprint: "MACHINE-ABC",
            expectedRequestNonce: "WRONG-NONCE",
            utcNow: DateTime.UtcNow);

        Assert.False(verify.IsValid);
        Assert.Equal(OfflineActivationErrorCode.NonceMismatch, verify.ErrorCode);
    }

    [Fact]
    public void Installation_mismatch_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);

        string json = OfflineActivationService.SignResponseJson(BuildResponse(), priv, keyId);
        var verify = OfflineActivationService.VerifyResponse(
            json,
            new Dictionary<string, string> { [keyId] = pub },
            expectedProductId: ProductIds.ProGlassAutomation,
            expectedInstallationId: "INST-999",
            expectedMachineFingerprint: "MACHINE-ABC",
            expectedRequestNonce: "NONCE-123",
            utcNow: DateTime.UtcNow);

        Assert.False(verify.IsValid);
        Assert.Equal(OfflineActivationErrorCode.InstallationMismatch, verify.ErrorCode);
    }

    [Fact]
    public void Machine_mismatch_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);

        string json = OfflineActivationService.SignResponseJson(BuildResponse(), priv, keyId);
        var verify = OfflineActivationService.VerifyResponse(
            json,
            new Dictionary<string, string> { [keyId] = pub },
            expectedProductId: ProductIds.ProGlassAutomation,
            expectedInstallationId: "INST-001",
            expectedMachineFingerprint: "OTHER-MACHINE",
            expectedRequestNonce: "NONCE-123",
            utcNow: DateTime.UtcNow);

        Assert.False(verify.IsValid);
        Assert.Equal(OfflineActivationErrorCode.MachineMismatch, verify.ErrorCode);
    }

    [Fact]
    public void Unknown_key_is_rejected()
    {
        var (_, priv) = LicenseCrypto.GenerateKeyPair();
        var (otherPub, _) = LicenseCrypto.GenerateKeyPair();
        string json = OfflineActivationService.SignResponseJson(BuildResponse(), priv, "signing-key-2026");

        var verify = OfflineActivationService.VerifyResponse(
            json,
            new Dictionary<string, string> { ["different-key"] = otherPub },
            expectedProductId: ProductIds.ProGlassAutomation,
            expectedInstallationId: "INST-001",
            expectedMachineFingerprint: "MACHINE-ABC",
            expectedRequestNonce: "NONCE-123",
            utcNow: DateTime.UtcNow);

        Assert.False(verify.IsValid);
        Assert.Equal(OfflineActivationErrorCode.UnknownKey, verify.ErrorCode);
    }

    [Fact]
    public void Expired_activation_response_is_rejected()
    {
        var (pub, priv) = LicenseCrypto.GenerateKeyPair();
        string keyId = LicenseCrypto.Fingerprint(pub);
        var response = BuildResponse();
        response.ExpiresAtUtc = new DateTime(2026, 8, 30, 0, 0, 0, DateTimeKind.Utc);

        string json = OfflineActivationService.SignResponseJson(response, priv, keyId);
        var verify = OfflineActivationService.VerifyResponse(
            json,
            new Dictionary<string, string> { [keyId] = pub },
            expectedProductId: ProductIds.ProGlassAutomation,
            expectedInstallationId: "INST-001",
            expectedMachineFingerprint: "MACHINE-ABC",
            expectedRequestNonce: "NONCE-123",
            utcNow: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc));

        Assert.False(verify.IsValid);
        Assert.Equal(OfflineActivationErrorCode.Expired, verify.ErrorCode);
    }
}
