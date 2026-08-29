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
    public const string LegacyProductionKeyId = "legacy-production-key-2026";

    public static IReadOnlyDictionary<string, string> All =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProductionKeyId] = LicensePublicKey.GeneratorCompatiblePublicKeyXml,
            [LegacyProductionKeyId] = LicensePublicKey.PublicKeyXml,
        };
}

/// <summary>
/// Thin app-side wrapper around the permanent ProGlass.Licensing contract.
/// </summary>
public static class ProGlassLicense
{
    public const string ProductId = ProductIds.ProGlassAutomation;

    public static string AppVersion =>
        Services.Updates.AppInfo.Version ?? "2.0.0";

    public static LicenseValidationResult VerifyFile(string path) =>
        ContractLicenseService.VerifyAndValidate(
            ContractLicenseReader.ReadFile(path),
            AppVersion,
            DateTime.UtcNow,
            TrustedLicenseKeys.All,
            currentFingerprint: MachineFingerprint.ComputeCurrent());

    public static LicenseValidationResult VerifyText(string licenseJson) =>
        ContractLicenseService.VerifyAndValidate(
            ContractLicenseReader.Read(licenseJson),
            AppVersion,
            DateTime.UtcNow,
            TrustedLicenseKeys.All,
            currentFingerprint: MachineFingerprint.ComputeCurrent());

    public static bool HasFeature(LicenseDocument? doc, string featureId) =>
        ContractLicenseValidator.HasFeature(doc, featureId);
}
