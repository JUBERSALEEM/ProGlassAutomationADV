using ProGlass.Licensing;
using ContractLicenseService = ProGlass.Licensing.LicenseService;
using ContractLicenseReader = ProGlass.Licensing.LicenseReader;
using ContractLicenseValidator = ProGlass.Licensing.LicenseValidator;

namespace ProGlassAutomation.Services.Licensing;

/// <summary>
/// The app's trusted signing-key ring for offline activation and signed license
/// verification. Public keys only.
/// </summary>
public static class TrustedLicenseKeys
{
    public const string ProductionKeyId = "production-key-2026";

    /// <summary>
    /// The app's trusted signing-key ring. Contains EXACTLY ONE key: the
    /// public half of the generator's embedded production pair. The
    /// legacy key id ("legacy-production-key-2026") was removed in the
    /// 2026-08-29 security cross-check — its private half was never
    /// recoverable, so it could never legitimately sign anything, and
    /// dead trust in the ring is nothing but attack surface. Only keys
    /// produced by the License Key Generator verify here.
    /// </summary>
    public static IReadOnlyDictionary<string, string> All =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProductionKeyId] = LicensePublicKey.GeneratorCompatiblePublicKeyXml,
        };
}

/// <summary>
/// Thin app-side wrapper around the permanent ProGlass.Licensing contract.
/// </summary>
public static class ProGlassLicense
{
    public const string ProductId = ProductIds.ProGlassAutomation;

    public static string AppVersion =>
        Services.Updates.AppInfo.Version ?? Services.Updates.AppInfo.FallbackVersion;

    public static LicenseValidationResult VerifyFile(string path) =>
        ContractLicenseService.VerifyAndValidate(
            ContractLicenseReader.ReadFile(path),
            AppVersion,
            DateTime.UtcNow,
            TrustedLicenseKeys.All,
            currentFingerprint: MachineFingerprint.ComputeCurrent());

    public static LicenseValidationResult VerifyText(string licenseJson, DateTime? lastSeenUtc = null) =>
        ContractLicenseService.VerifyAndValidate(
            ContractLicenseReader.Read(licenseJson),
            AppVersion,
            DateTime.UtcNow,
            TrustedLicenseKeys.All,
            currentFingerprint: MachineFingerprint.ComputeCurrent(),
            lastSeenUtc: lastSeenUtc);

    public static bool HasFeature(LicenseDocument? doc, string featureId) =>
        ContractLicenseValidator.HasFeature(doc, featureId);
}
