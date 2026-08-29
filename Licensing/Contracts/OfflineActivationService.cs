using System.Text.Json;

namespace ProGlass.Licensing;

/// <summary>
/// Structured outcomes for reading, signing and verifying offline activation
/// requests / responses.
/// </summary>
public enum OfflineActivationErrorCode
{
    None = 0,
    EmptyRequest = 1,
    MalformedRequest = 2,
    UnsupportedProtocol = 3,
    MissingRequestFields = 4,
    EmptyResponse = 5,
    MalformedResponse = 6,
    MissingResponseFields = 7,
    UnknownKey = 8,
    InvalidSignature = 9,
    WrongProduct = 10,
    NotYetValid = 11,
    Expired = 12,
    InstallationMismatch = 13,
    MachineMismatch = 14,
    NonceMismatch = 15,
    UnsupportedStatus = 16,
    OversizedRequest = 17,
    OversizedResponse = 18,
    InactiveStatus = 19
}

public sealed class OfflineActivationRequestReadResult
{
    public bool Success { get; init; }
    public OfflineActivationErrorCode ErrorCode { get; init; } = OfflineActivationErrorCode.None;
    public string? Message { get; init; }
    public OfflineActivationRequestDocument? Request { get; init; }
}

public sealed class OfflineActivationVerificationResult
{
    public bool IsValid { get; init; }
    public OfflineActivationErrorCode ErrorCode { get; init; } = OfflineActivationErrorCode.None;
    public string? Message { get; init; }
    public SignedOfflineActivationResponse? Response { get; init; }
    public OfflineActivationResponseDocument? Document => Response?.Payload;
    public string? SigningKeyId { get; init; }
}

/// <summary>
/// Pure offline activation protocol helpers. No WPF, no logging, no machine
/// APIs, no file system. Callers supply the request text, trusted key ring,
/// expected binding values and the current UTC time.
/// </summary>
public static class OfflineActivationService
{
    public const int MaxRequestCharacters = 64 * 1024;
    public const int MaxResponseCharacters = 512 * 1024;

    public static string CreateRequestJson(OfflineActivationRequestDocument request)
    {
        if (request is null) throw new ArgumentNullException(nameof(request));

        return JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static OfflineActivationRequestReadResult ReadRequest(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new OfflineActivationRequestReadResult
            {
                Success = false,
                ErrorCode = OfflineActivationErrorCode.EmptyRequest,
                Message = "Activation request is empty."
            };
        }

        if (json.Length > MaxRequestCharacters)
        {
            return new OfflineActivationRequestReadResult
            {
                Success = false,
                ErrorCode = OfflineActivationErrorCode.OversizedRequest,
                Message = "Activation request exceeds the safe size limit."
            };
        }

        OfflineActivationRequestDocument? request;
        try
        {
            request = JsonSerializer.Deserialize<OfflineActivationRequestDocument>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
        }
        catch (JsonException ex)
        {
            return new OfflineActivationRequestReadResult
            {
                Success = false,
                ErrorCode = OfflineActivationErrorCode.MalformedRequest,
                Message = "Malformed activation request: " + ex.Message
            };
        }

        if (request is null)
        {
            return new OfflineActivationRequestReadResult
            {
                Success = false,
                ErrorCode = OfflineActivationErrorCode.MalformedRequest,
                Message = "Activation request could not be parsed."
            };
        }

        if (request.ProtocolVersion != OfflineActivationProtocol.Version)
        {
            return new OfflineActivationRequestReadResult
            {
                Success = false,
                ErrorCode = OfflineActivationErrorCode.UnsupportedProtocol,
                Message = "Unsupported activation request protocol version: " + request.ProtocolVersion
            };
        }

        if (string.IsNullOrWhiteSpace(request.ProductId)
            || string.IsNullOrWhiteSpace(request.LicenseId)
            || string.IsNullOrWhiteSpace(request.InstallationId)
            || string.IsNullOrWhiteSpace(request.MachineFingerprint)
            || string.IsNullOrWhiteSpace(request.RequestNonce))
        {
            return new OfflineActivationRequestReadResult
            {
                Success = false,
                ErrorCode = OfflineActivationErrorCode.MissingRequestFields,
                Message = "Activation request is missing product, license, installation, machine or nonce fields."
            };
        }

        return new OfflineActivationRequestReadResult
        {
            Success = true,
            Request = request
        };
    }

    public static string SignResponseJson(
        OfflineActivationResponseDocument response,
        string privateKeyXml,
        string keyId)
    {
        if (response is null) throw new ArgumentNullException(nameof(response));
        if (string.IsNullOrWhiteSpace(privateKeyXml))
            throw new ArgumentException("Private key is required.", nameof(privateKeyXml));

        byte[] canonical = OfflineActivationCanonical.SerializeResponseUtf8(response);
        string signature = LicenseCrypto.SignCanonical(canonical, privateKeyXml);

        var signed = new SignedOfflineActivationResponse
        {
            ProtocolVersion = response.ProtocolVersion,
            Payload = response,
            Signature = new SignatureEnvelope
            {
                Algorithm = LicenseCrypto.Algorithm,
                KeyId = string.IsNullOrWhiteSpace(keyId)
                    ? "unknown"
                    : keyId,
                Value = signature
            }
        };

        return JsonSerializer.Serialize(signed, new JsonSerializerOptions
        {
            WriteIndented = true
        });
    }

    public static OfflineActivationVerificationResult VerifyResponse(
        string responseJson,
        IReadOnlyDictionary<string, string> trustedPublicKeys,
        string expectedProductId,
        string expectedInstallationId,
        string expectedMachineFingerprint,
        string expectedRequestNonce,
        DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.EmptyResponse,
                Message = "Activation response is empty."
            };
        }

        if (responseJson.Length > MaxResponseCharacters)
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.OversizedResponse,
                Message = "Activation response exceeds the safe size limit."
            };
        }

        SignedOfflineActivationResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<SignedOfflineActivationResponse>(
                responseJson,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip
                });
        }
        catch (JsonException ex)
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.MalformedResponse,
                Message = "Malformed activation response: " + ex.Message
            };
        }

        if (response?.Payload == null || string.IsNullOrWhiteSpace(response.Signature?.Value))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.MissingResponseFields,
                Message = "Activation response is missing payload or signature."
            };
        }

        if (response.ProtocolVersion != OfflineActivationProtocol.Version
            || response.Payload.ProtocolVersion != OfflineActivationProtocol.Version)
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.UnsupportedProtocol,
                Message = "Unsupported activation response protocol version."
            };
        }

        if (string.IsNullOrWhiteSpace(response.Payload.ProductId)
            || string.IsNullOrWhiteSpace(response.Payload.LicenseId)
            || string.IsNullOrWhiteSpace(response.Payload.ActivationId)
            || string.IsNullOrWhiteSpace(response.Payload.InstallationId)
            || string.IsNullOrWhiteSpace(response.Payload.MachineFingerprint)
            || string.IsNullOrWhiteSpace(response.Payload.RequestNonce)
            || string.IsNullOrWhiteSpace(response.Payload.LicenseStatus)
            || string.IsNullOrWhiteSpace(response.Signature.KeyId))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.MissingResponseFields,
                Message = "Activation response is missing required security fields."
            };
        }

        if (!string.Equals(response.Signature.Algorithm, LicenseCrypto.Algorithm, StringComparison.Ordinal))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.UnsupportedProtocol,
                Message = "Unsupported activation signature algorithm: " + response.Signature.Algorithm
            };
        }

        if (!trustedPublicKeys.TryGetValue(response.Signature.KeyId, out string? publicKeyXml)
            || string.IsNullOrWhiteSpace(publicKeyXml))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.UnknownKey,
                Message = "No trusted activation signing key for keyId '" + response.Signature.KeyId + "'.",
                Response = response
            };
        }

        byte[] canonical = OfflineActivationCanonical.SerializeResponseUtf8(response.Payload);
        bool signatureOk = LicenseCrypto.VerifyCanonical(canonical, response.Signature.Value, publicKeyXml);
        if (!signatureOk)
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.InvalidSignature,
                Message = "Activation response signature does not match the payload.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        if (!string.Equals(response.Payload.ProductId, expectedProductId, StringComparison.Ordinal))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.WrongProduct,
                Message = "Activation response is for a different product.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        if (!string.Equals(response.Payload.InstallationId, expectedInstallationId, StringComparison.Ordinal))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.InstallationMismatch,
                Message = "Activation response does not match this installation ID.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        if (!string.Equals(response.Payload.MachineFingerprint, expectedMachineFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.MachineMismatch,
                Message = "Activation response is bound to a different machine fingerprint.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        if (!string.Equals(response.Payload.RequestNonce, expectedRequestNonce, StringComparison.Ordinal))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.NonceMismatch,
                Message = "Activation response does not match the original request nonce.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        if (response.Payload.NotBeforeUtc.ToUniversalTime() > utcNow)
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.NotYetValid,
                Message = "Activation response is not valid yet.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        if (response.Payload.ExpiresAtUtc.HasValue
            && response.Payload.ExpiresAtUtc.Value.ToUniversalTime() < utcNow)
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.Expired,
                Message = "Activation response has expired.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        if (!IsSupportedStatus(response.Payload.LicenseStatus))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.UnsupportedStatus,
                Message = "Unsupported activation license status: '" + response.Payload.LicenseStatus + "'.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        if (!string.Equals(response.Payload.LicenseStatus, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return new OfflineActivationVerificationResult
            {
                IsValid = false,
                ErrorCode = OfflineActivationErrorCode.InactiveStatus,
                Message = "Activation response status is '" + response.Payload.LicenseStatus + "', not Active.",
                Response = response,
                SigningKeyId = response.Signature.KeyId
            };
        }

        return new OfflineActivationVerificationResult
        {
            IsValid = true,
            Response = response,
            SigningKeyId = response.Signature.KeyId
        };
    }

    private static bool IsSupportedStatus(string status)
    {
        return string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Revoked", StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, "Expired", StringComparison.OrdinalIgnoreCase);
    }
}
