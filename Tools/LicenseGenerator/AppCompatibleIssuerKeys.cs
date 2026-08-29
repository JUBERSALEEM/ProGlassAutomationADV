namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// Public trust hints for the current ProGlass app build.
///
/// This file intentionally contains PUBLIC verification material only.
/// The matching private signing key must be loaded from an operator-held
/// external key file and must never be embedded in this repository again.
/// </summary>
internal static class AppCompatibleIssuerKeys
{
    public const string ProductionKeyId = "production-key-2026";
    public const string LegacyProductionKeyId = "legacy-production-key-2026";

    public static string PublicKeyXml { get; } =
        "<RSAKeyValue>" +
        "<Modulus>tzD0mVm083BrnW8Y535QKNPI32B7bDVWqawsH+5UscgXqod+9AsIOw7mk76E9m25VUbTWr/fO7/wCQQV7CYZusXRNPwGSiVxwaIXQ+yib3uFVRLsiii+XmbMfvT0Aur5oDVfzo/l3vj3gj71xnfZ8xuobeCidpI8CypwZY3utnMDJ5EC37XCvrh0mjiR6ruUB8LHBvrayXDXdPNZ4BQfAXHRUkFj1bPJP4FWMsAfLywod1zyyJV+/8EPUxcyi/s/V2A3Hmtci+Ml3NUFEuc0yEuVEVAkUrIEE97oE+3qsIX+hi3jiybhAMi2Y6LcnqNdfV+ekCLUOio8bgn1ChfVDQ==</Modulus>" +
        "<Exponent>AQAB</Exponent>" +
        "</RSAKeyValue>";

    public static string LegacyPublicKeyXml { get; } =
        "<RSAKeyValue>" +
        "<Modulus>2rJQoH/RW4j+V5IPCsgmEfOH2yi8Ow09LE6Cgoe6h+ernjT52/SXFCEUCHAjYJppXWIvtGhR/F2d9HpcDWFlCZgIGCtFmO3TAW9/0b/Q4d4i9eYCyAfSgyuRevk8jFzkPnR17uNnkqmpYnQE9Y5aYoEGJq64xjmRJNy/I9w1otHmxLVRA6My+Yeab6Vye5PMwWBQioxW+wz05oDfxWTsgSENdGavqdMOD3/HoaC2D3IsIifWle45c9EBmoPUGjaGYJHT+qrt190lOr7nIqFQYI5LgdIm5ZrDUKSgSxi6ZoFFsTdXPZle5rdoxOf3bMYvYDqGNm9xv2IsUuBJy0AlHw==</Modulus>" +
        "<Exponent>AQAB</Exponent>" +
        "</RSAKeyValue>";

    public static IReadOnlyDictionary<string, string> TrustedPublicKeys =>
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ProductionKeyId] = PublicKeyXml,
            [LegacyProductionKeyId] = LegacyPublicKeyXml
        };

    public static bool IsKnownKey(string keyId, string publicKeyXml)
    {
        return TrustedPublicKeys.TryGetValue(keyId, out var expected)
            && string.Equals(expected, publicKeyXml, StringComparison.Ordinal);
    }
}
