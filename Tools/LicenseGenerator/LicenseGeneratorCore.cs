// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/LicenseGeneratorCore.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Standalone generator service
//
// Thin, UI-free service behind the generator window. Every operation is a
// thin call into ProGlass.Licensing — the generator never re-implements
// signing/verification. It is fully standalone:
//   • NO reference to ProGlassAutomation.
//   • NO app services (AppLogService / Audit / CorrelationContext).
//   • NO database, EF Core, or WPF app build.
//   • NO AppLogServiceStub / AuditStub / fake service.
//
// The ONLY local, generator-owned state is:
//   • the signing key (stored DPAPI-protected for the current Windows user),
//   • a generator-side license history (JSON; never the licensing authority).
// ═══════════════════════════════════════════════════════════════════════

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProGlass.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// UI-free service behind the LicenseGenerator window. Holds the key store,
/// generates licenses (with mandatory self-verification), inspects existing
/// licenses, and keeps generator-side history. Safe from any thread.
/// </summary>
public static class LicenseGeneratorCore
{
    // ── Generator-owned storage (NOT the licensing authority) ────────

    /// <summary>Keys file (DPAPI-protected XML payload in JSON).</summary>
    public static string KeysFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProGlassAutomation", "license-keys.json");

    /// <summary>Generator-side history (never read by the app).</summary>
    public static string HistoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ProGlassAutomation", "license-history.json");

    /// <summary>Default output folder for generated .proglasslicense files.</summary>
    public static string DefaultOutputFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "ProGlassAutomation", "Licenses");

    // ── Key store (DPAPI-protected private key) ───────────────────────

    /// <summary>Shape of the persisted key store.</summary>
    public sealed class KeyStore
    {
        [JsonPropertyName("publicKeyXml")]
        public string PublicKeyXml { get; set; } = "";

        // The public key is written in the clear (it is public). The PRIVATE
        // key is stored as a JSON payload that is itself a DPAPI-protected
        // base64 blob — never plaintext private-key XML on disk.
        [JsonPropertyName("privateKeyProtected")]
        public string? PrivateKeyProtected { get; set; }

        [JsonPropertyName("keyId")]
        public string KeyId { get; set; } = "";

        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; }
    }

    /// <summary>Load existing key store, or generate a fresh one.</summary>
    public static KeyStore LoadOrCreateKeyStore(out bool wasCreated)
    {
        wasCreated = false;
        if (File.Exists(KeysFilePath))
        {
            try
            {
                var ks = JsonSerializer.Deserialize<KeyStore>(File.ReadAllText(KeysFilePath),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (ks != null && !string.IsNullOrWhiteSpace(ks.PublicKeyXml)
                    && !string.IsNullOrWhiteSpace(ks.PrivateKeyProtected))
                {
                    return ks;
                }
            }
            catch { /* recreate below */ }
        }

        (string pub, string priv) = LicenseCrypto.GenerateKeyPair();
        var store = new KeyStore
        {
            PublicKeyXml = pub,
            PrivateKeyProtected = Protect(priv),
            KeyId = LicenseCrypto.Fingerprint(pub),
            CreatedUtc = DateTime.UtcNow
        };
        SaveKeyStore(store);
        wasCreated = true;
        return store;
    }

    public static void SaveKeyStore(KeyStore store)
    {
        var dir = Path.GetDirectoryName(KeysFilePath)!;
        Directory.CreateDirectory(dir);
        string temp = KeysFilePath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(store, new JsonSerializerOptions { WriteIndented = true }),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        if (File.Exists(KeysFilePath)) File.Delete(KeysFilePath);
        File.Move(temp, KeysFilePath);
    }

    /// <summary>Explicitly generate a NEW key pair (replaces any existing key).</summary>
    public static KeyStore GenerateNewKeyPair()
    {
        (string pub, string priv) = LicenseCrypto.GenerateKeyPair();
        var store = new KeyStore
        {
            PublicKeyXml = pub,
            PrivateKeyProtected = Protect(priv),
            KeyId = LicenseCrypto.Fingerprint(pub),
            CreatedUtc = DateTime.UtcNow
        };
        SaveKeyStore(store);
        return store;
    }

    /// <summary>Recover the PLAINTEXT private key (only in memory) for signing.</summary>
    public static string GetPrivateKey(KeyStore store)
    {
        if (store is null || string.IsNullOrWhiteSpace(store.PrivateKeyProtected))
            throw new InvalidOperationException("No private key in store.");
        return Unprotect(store.PrivateKeyProtected);
    }

    // ── Generate a license (mandatory self-verification) ──────────────

    public static LicenseGenerationResult GenerateLicense(
        LicenseRequest request, KeyStore store, string? outputPath = null)
    {
        if (store is null) throw new ArgumentNullException(nameof(store));
        string priv = GetPrivateKey(store);
        return LicenseService.Create(request, priv, store.KeyId, outputPath);
    }

    /// <summary>Inspect an existing license file / string and report a verdict.</summary>
    public static LicenseValidationResult Inspect(string licenseJson, string appVersion,
        IReadOnlyDictionary<string, string> trustedKeys, string? fingerprint = null)
    {
        return LicenseService.VerifyAndValidate(licenseJson, appVersion, DateTime.UtcNow,
            trustedKeys, fingerprint: fingerprint, currentDeviceCount: 1);
    }

    // ── Generator history (authority is the license, not this file) ───

    public sealed class HistoryEntry
    {
        [JsonPropertyName("licenseId")] public string LicenseId { get; set; } = "";
        [JsonPropertyName("customer")] public string Customer { get; set; } = "";
        [JsonPropertyName("licenseType")] public string LicenseType { get; set; } = "";
        [JsonPropertyName("createdUtc")] public DateTime CreatedUtc { get; set; }
        [JsonPropertyName("expiresUtc")] public DateTime? ExpiresUtc { get; set; }
        [JsonPropertyName("keyId")] public string KeyId { get; set; } = "";
        [JsonPropertyName("status")] public string Status { get; set; } = "";
    }

    public static void RecordHistory(LicenseGenerationResult result)
    {
        if (result?.Document is null) return;
        var list = LoadHistory();
        list.Insert(0, new HistoryEntry
        {
            LicenseId = result.Document.LicenseId,
            Customer = result.Document.Customer.Name ?? result.Document.Customer.Id,
            LicenseType = result.Document.LicenseType,
            CreatedUtc = result.Document.IssuedAtUtc,
            ExpiresUtc = result.Document.ExpiresAtUtc,
            KeyId = result.KeyId ?? "",
            Status = result.Success ? "Generated" : "Failed"
        });
        try
        {
            var dir = Path.GetDirectoryName(HistoryPath)!;
            Directory.CreateDirectory(dir);
            File.WriteAllText(HistoryPath,
                JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { /* history is best-effort; never fails generation */ }
    }

    public static List<HistoryEntry> LoadHistory()
    {
        if (!File.Exists(HistoryPath)) return new List<HistoryEntry>();
        try
        {
            return JsonSerializer.Deserialize<List<HistoryEntry>>(File.ReadAllText(HistoryPath))
                ?? new List<HistoryEntry>();
        }
        catch { return new List<HistoryEntry>(); }
    }

    // ── DPAPI helpers (Windows-only; this is the generator, so fine) ──

    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ProGlass.LicenseGenerator.v1");

    private static string Protect(string plaintext)
    {
        byte[] data = Encoding.UTF8.GetBytes(plaintext);
        byte[] enc = ProtectedData.Protect(data, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(enc);
    }

    private static string Unprotect(string protectedBase64)
    {
        byte[] enc = Convert.FromBase64String(protectedBase64);
        byte[] dec = ProtectedData.Unprotect(enc, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(dec);
    }

    // ── Internal use (machine fingerprint string, forwarded, not computed) ──
    // The generator never computes a machine fingerprint. If the admin wants
    // a machine-bound license they paste the customer's fingerprint. Kept for
    // optional convenience to read THIS machine (e.g. when generating a dev
    // self-key), but that is the only place it touches the OS.
    public static string ThisMachineFingerprint()
    {
        try
        {
            // Minimal, deterministic, no app services — reuse the OS identity
            // anchor (registry MachineGuid) to give a stable dev-machine id.
            using var key = Microsoft.Win32.Registry.LocalMachine
                .OpenSubKey(@"SOFTWARE\Microsoft\Cryptography");
            var guid = key?.GetValue("MachineGuid") as string;
            if (!string.IsNullOrWhiteSpace(guid))
            {
                using var sha = SHA256.Create();
                byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(guid.Trim()));
                var sb = new StringBuilder(32);
                foreach (byte b in h.Take(16)) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }
        catch { /* fall through */ }
        return "";
    }
}
