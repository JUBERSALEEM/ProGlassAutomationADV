using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProGlassAutomation.Services.Licensing;

/// <summary>
/// Local protected storage for the offline activation redesign.
///
/// Stores three machine-scoped artifacts under the app data root:
///   1. installation identity
///   2. last generated activation request context (licenseId + nonce)
///   3. installed signed activation response + anti-rollback timestamp
///
/// Everything is protected with Windows DPAPI LocalMachine scope so the state
/// is no longer ordinary plaintext JSON on disk.
/// </summary>
internal static class OfflineActivationStore
{
    public const string InstallationStateFileName = "offline-installation.bin";
    public const string PendingRequestStateFileName = "offline-request.bin";
    public const string ActivationStateFileName = "offline-activation.bin";

    private const int StateFormatVersion = 1;

    private static readonly byte[] Entropy =
        Encoding.UTF8.GetBytes("ProGlassAutomation.OfflineLicensing.v1");

    internal sealed class ProtectedReadResult<T> where T : class
    {
        public bool Exists { get; init; }
        public bool Success { get; init; }
        public string Error { get; init; } = "";
        public T? Value { get; init; }
    }

    internal sealed class InstallationState
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; } = StateFormatVersion;

        [JsonPropertyName("installationId")]
        public string InstallationId { get; set; } = "";

        [JsonPropertyName("createdAtUtc")]
        public DateTime CreatedAtUtc { get; set; }
    }

    internal sealed class PendingRequestState
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; } = StateFormatVersion;

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

    internal sealed class ActivationState
    {
        [JsonPropertyName("formatVersion")]
        public int FormatVersion { get; set; } = StateFormatVersion;

        [JsonPropertyName("installationId")]
        public string InstallationId { get; set; } = "";

        [JsonPropertyName("licenseId")]
        public string LicenseId { get; set; } = "";

        [JsonPropertyName("activationId")]
        public string ActivationId { get; set; } = "";

        [JsonPropertyName("machineFingerprint")]
        public string MachineFingerprint { get; set; } = "";

        [JsonPropertyName("requestNonce")]
        public string RequestNonce { get; set; } = "";

        [JsonPropertyName("keyId")]
        public string KeyId { get; set; } = "";

        [JsonPropertyName("signedResponseJson")]
        public string SignedResponseJson { get; set; } = "";

        [JsonPropertyName("lastValidatedUtc")]
        public DateTime LastValidatedUtc { get; set; }
    }

    public static string GetInstallationId(string dataRoot)
    {
        var existing = ReadInstallationStateDetailed(dataRoot);
        if (existing.Success && existing.Value != null && !string.IsNullOrWhiteSpace(existing.Value.InstallationId))
            return existing.Value.InstallationId;

        var created = new InstallationState
        {
            InstallationId = Guid.NewGuid().ToString("N"),
            CreatedAtUtc = DateTime.UtcNow
        };
        WriteProtected(Path.Combine(dataRoot, InstallationStateFileName), created);
        return created.InstallationId;
    }

    public static PendingRequestState SavePendingRequest(
        string dataRoot,
        string licenseId,
        string installationId,
        string machineFingerprint,
        string applicationVersion)
    {
        var state = new PendingRequestState
        {
            LicenseId = licenseId,
            InstallationId = installationId,
            MachineFingerprint = machineFingerprint,
            RequestNonce = Guid.NewGuid().ToString("N"),
            RequestedAtUtc = DateTime.UtcNow,
            ApplicationVersion = applicationVersion
        };

        WriteProtected(Path.Combine(dataRoot, PendingRequestStateFileName), state);
        return state;
    }

    public static PendingRequestState? ReadPendingRequest(string dataRoot) =>
        ReadPendingRequestDetailed(dataRoot).Value;

    public static ProtectedReadResult<PendingRequestState> ReadPendingRequestDetailed(string dataRoot) =>
        ReadProtected<PendingRequestState>(Path.Combine(dataRoot, PendingRequestStateFileName));

    public static void ClearPendingRequest(string dataRoot)
    {
        TryDelete(Path.Combine(dataRoot, PendingRequestStateFileName));
    }

    public static ActivationState? ReadActivationState(string dataRoot) =>
        ReadActivationStateDetailed(dataRoot).Value;

    public static ProtectedReadResult<ActivationState> ReadActivationStateDetailed(string dataRoot) =>
        ReadProtected<ActivationState>(Path.Combine(dataRoot, ActivationStateFileName));

    public static void SaveActivationState(
        string dataRoot,
        string installationId,
        string licenseId,
        string activationId,
        string machineFingerprint,
        string requestNonce,
        string keyId,
        string signedResponseJson,
        DateTime lastValidatedUtc)
    {
        var state = new ActivationState
        {
            InstallationId = installationId,
            LicenseId = licenseId,
            ActivationId = activationId,
            MachineFingerprint = machineFingerprint,
            RequestNonce = requestNonce,
            KeyId = keyId,
            SignedResponseJson = signedResponseJson,
            LastValidatedUtc = lastValidatedUtc.ToUniversalTime()
        };

        WriteProtected(Path.Combine(dataRoot, ActivationStateFileName), state);
    }

    public static void UpdateLastValidatedUtc(string dataRoot, DateTime lastValidatedUtc)
    {
        var current = ReadActivationStateDetailed(dataRoot);
        if (!current.Success || current.Value == null)
            return;

        if (current.Value.LastValidatedUtc >= lastValidatedUtc.ToUniversalTime())
            return;

        current.Value.LastValidatedUtc = lastValidatedUtc.ToUniversalTime();
        WriteProtected(Path.Combine(dataRoot, ActivationStateFileName), current.Value);
    }

    public static void ClearActivationState(string dataRoot)
    {
        TryDelete(Path.Combine(dataRoot, ActivationStateFileName));
    }

    public static string ActivationStatePath(string dataRoot) =>
        Path.Combine(dataRoot, ActivationStateFileName);

    private static ProtectedReadResult<InstallationState> ReadInstallationStateDetailed(string dataRoot) =>
        ReadProtected<InstallationState>(Path.Combine(dataRoot, InstallationStateFileName));

    private static ProtectedReadResult<T> ReadProtected<T>(string path) where T : class
    {
        try
        {
            if (!File.Exists(path))
            {
                return new ProtectedReadResult<T>
                {
                    Exists = false,
                    Success = false,
                    Error = "",
                    Value = null
                };
            }

            byte[] cipher = File.ReadAllBytes(path);
            if (cipher.Length == 0)
            {
                return new ProtectedReadResult<T>
                {
                    Exists = true,
                    Success = false,
                    Error = "Protected state file is empty.",
                    Value = null
                };
            }

            byte[] plain = ProtectedData.Unprotect(
                cipher,
                Entropy,
                DataProtectionScope.LocalMachine);

            var value = JsonSerializer.Deserialize<T>(plain);
            if (value == null)
            {
                return new ProtectedReadResult<T>
                {
                    Exists = true,
                    Success = false,
                    Error = "Protected state payload could not be deserialized.",
                    Value = null
                };
            }

            return new ProtectedReadResult<T>
            {
                Exists = true,
                Success = true,
                Error = "",
                Value = value
            };
        }
        catch (Exception ex)
        {
            return new ProtectedReadResult<T>
            {
                Exists = File.Exists(path),
                Success = false,
                Error = ex.Message,
                Value = null
            };
        }
    }

    private static void WriteProtected<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        byte[] plain = JsonSerializer.SerializeToUtf8Bytes(value);
        byte[] cipher = ProtectedData.Protect(
            plain,
            Entropy,
            DataProtectionScope.LocalMachine);
        File.WriteAllBytes(path, cipher);
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch
        {
            // best effort — local cleanup must never crash activation flows
        }
    }
}
