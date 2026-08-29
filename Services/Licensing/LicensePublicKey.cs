using System.Collections.Generic;
using System.ComponentModel;

namespace ProGlassAutomation.Services.Licensing;

/// <summary>
/// Embedded public verification material for ProGlass licensing.
///
/// Security boundary:
/// - production verification keys are embedded here
/// - no production private signing key may exist here
/// - client-side trial signing was removed from the secure offline redesign
/// </summary>
public static class LicensePublicKey
{
    public static string PublicKeyXml { get; private set; } =
        "<RSAKeyValue>" +
        "<Modulus>2rJQoH/RW4j+V5IPCsgmEfOH2yi8Ow09LE6Cgoe6h+ernjT52/SXFCEUCHAjYJppXWIvtGhR/F2d9HpcDWFlCZgIGCtFmO3TAW9/0b/Q4d4i9eYCyAfSgyuRevk8jFzkPnR17uNnkqmpYnQE9Y5aYoEGJq64xjmRJNy/I9w1otHmxLVRA6My+Yeab6Vye5PMwWBQioxW+wz05oDfxWTsgSENdGavqdMOD3/HoaC2D3IsIifWle45c9EBmoPUGjaGYJHT+qrt190lOr7nIqFQYI5LgdIm5ZrDUKSgSxi6ZoFFsTdXPZle5rdoxOf3bMYvYDqGNm9xv2IsUuBJy0AlHw==</Modulus>" +
        "<Exponent>AQAB</Exponent>" +
        "</RSAKeyValue>";

    public static bool IsPlaceholder =>
        PublicKeyXml.StartsWith("PLACEHOLDER_", System.StringComparison.Ordinal);

    /// <summary>
    /// Public half of the generator's embedded production signing key.
    /// MUST stay byte-identical to
    /// Tools/LicenseGenerator/ProductionSigningKey.PublicKeyXml — a
    /// source test pins this equality so the app always accepts exactly
    /// what the generator signs (and nothing else).
    /// </summary>
    public static string GeneratorCompatiblePublicKeyXml { get; } =
        "<RSAKeyValue><Modulus>w565u0lCLzyH43JlW9LT+ukiazU63OiL1Q4hA46mja9i2eGGNn/EyAR0h1ntUaj89IYYMPfao4etT5aZTuED9MeTD5HYS8LSXLflf/FmQ2CLzKj+cA51JXFRg+A//mOT7EDyaq7GRoQqY810NT6gySTNEkAzeIc5E4jm0jzl4WE162N5EPUZV3WiVFyN/lXvhCsGlmr+DXftGV2hG6PL8JLa8axdIL0RVL3X56TKwSmQuWIOVBbINGlhPiY5wjKl2IhDHmtuPVSntMLnkdfElmG36HU2aGoOqHuiwzuppvvqz6Zt7V9QlM97m3K42sO6GcxO18iluMAFIv6seri1NQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    public static IReadOnlyList<string> ProductionPublicKeys => new[]
    {
        PublicKeyXml,
        GeneratorCompatiblePublicKeyXml
    };

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void SetForTest(string xml) => PublicKeyXml = xml;

    /// <summary>
    /// Legacy trial public-key property kept only for source compatibility.
    /// Trial responses are now expected to be vendor-issued offline artifacts,
    /// not client-self-signed payloads.
    /// </summary>
    public static string TrialPublicKeyXml { get; private set; } =
        "PLACEHOLDER_TRIAL_PUBLIC_KEY_REMOVED";

    private static string? _trialPrivateKeyXmlOverride;

    /// <summary>
    /// Secure offline redesign: no embedded trial private key remains in the
    /// client repository. Tests may still inject a synthetic value through the
    /// internal override below.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string TrialPrivateKeyXml =>
        _trialPrivateKeyXmlOverride ?? TrialKeyVault.GetPrivateKeyXml();

    public static bool IsTrialKeyPlaceholder =>
        TrialPublicKeyXml.StartsWith("PLACEHOLDER_TRIAL_", System.StringComparison.Ordinal)
        || TrialPrivateKeyXml.StartsWith("PLACEHOLDER_TRIAL_", System.StringComparison.Ordinal);

    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static void SetTrialKeysForTest(string publicXml, string privateXml)
    {
        TrialPublicKeyXml = publicXml;
        _trialPrivateKeyXmlOverride = privateXml;
        TrialKeyVault.ResetCacheForTests();
    }
}
