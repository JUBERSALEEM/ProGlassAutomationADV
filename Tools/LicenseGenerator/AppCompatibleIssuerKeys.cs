using System.ComponentModel;

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// Vendor-only signing key pair for app-compatible .lic issuance.
///
/// This tool is an INTERNAL issuer utility, not customer software. The main
/// app trusts the matching public key as a secondary production verifier so
/// freshly generated licenses activate without rebuilding the app.
/// </summary>
internal static class AppCompatibleIssuerKeys
{
    public static string PublicKeyXml { get; } =
        "<RSAKeyValue>" +
        "<Modulus>tzD0mVm083BrnW8Y535QKNPI32B7bDVWqawsH+5UscgXqod+9AsIOw7mk76E9m25VUbTWr/fO7/wCQQV7CYZusXRNPwGSiVxwaIXQ+yib3uFVRLsiii+XmbMfvT0Aur5oDVfzo/l3vj3gj71xnfZ8xuobeCidpI8CypwZY3utnMDJ5EC37XCvrh0mjiR6ruUB8LHBvrayXDXdPNZ4BQfAXHRUkFj1bPJP4FWMsAfLywod1zyyJV+/8EPUxcyi/s/V2A3Hmtci+Ml3NUFEuc0yEuVEVAkUrIEE97oE+3qsIX+hi3jiybhAMi2Y6LcnqNdfV+ekCLUOio8bgn1ChfVDQ==</Modulus>" +
        "<Exponent>AQAB</Exponent>" +
        "</RSAKeyValue>";

    /// <summary>
    /// Legacy production public key kept for inspection compatibility with
    /// licenses that were issued before the generator rewrite.
    /// </summary>
    public static string LegacyPublicKeyXml { get; } =
        "<RSAKeyValue>" +
        "<Modulus>2rJQoH/RW4j+V5IPCsgmEfOH2yi8Ow09LE6Cgoe6h+ernjT52/SXFCEUCHAjYJppXWIvtGhR/F2d9HpcDWFlCZgIGCtFmO3TAW9/0b/Q4d4i9eYCyAfSgyuRevk8jFzkPnR17uNnkqmpYnQE9Y5aYoEGJq64xjmRJNy/I9w1otHmxLVRA6My+Yeab6Vye5PMwWBQioxW+wz05oDfxWTsgSENdGavqdMOD3/HoaC2D3IsIifWle45c9EBmoPUGjaGYJHT+qrt190lOr7nIqFQYI5LgdIm5ZrDUKSgSxi6ZoFFsTdXPZle5rdoxOf3bMYvYDqGNm9xv2IsUuBJy0AlHw==</Modulus>" +
        "<Exponent>AQAB</Exponent>" +
        "</RSAKeyValue>";

    public static IReadOnlyList<string> TrustedPublicKeys => new[]
    {
        PublicKeyXml,
        LegacyPublicKeyXml
    };

    [EditorBrowsable(EditorBrowsableState.Never)]
    public static string PrivateKeyXml =>
        "<RSAKeyValue>" +
        "<Modulus>tzD0mVm083BrnW8Y535QKNPI32B7bDVWqawsH+5UscgXqod+9AsIOw7mk76E9m25VUbTWr/fO7/wCQQV7CYZusXRNPwGSiVxwaIXQ+yib3uFVRLsiii+XmbMfvT0Aur5oDVfzo/l3vj3gj71xnfZ8xuobeCidpI8CypwZY3utnMDJ5EC37XCvrh0mjiR6ruUB8LHBvrayXDXdPNZ4BQfAXHRUkFj1bPJP4FWMsAfLywod1zyyJV+/8EPUxcyi/s/V2A3Hmtci+Ml3NUFEuc0yEuVEVAkUrIEE97oE+3qsIX+hi3jiybhAMi2Y6LcnqNdfV+ekCLUOio8bgn1ChfVDQ==</Modulus>" +
        "<Exponent>AQAB</Exponent>" +
        "<P>+d/qv0ftPBcQWet4Kj5V7EeG2ek9SOSCTk5UKcKxfCOhdDZe7PH6dEwsqgjDWrHKQ7aQLac2rSQe9EZ+BZoyvPmtdE2tD5JdaArPk/5Vkt+B1TsAo8cbx8SIhsGcnpf0y9vXvEpr3Nu4yzwQ7sP7ucW9G1Gubii2tf8uYHDpQAk=</P>" +
        "<Q>u66RaL/Q/+YodyVjjt4xSbmkT9NiVUauhJxh3mc3DQWlYRcWWnAVm18ls9MMeqp0IrYpknmySibxCcsH2uFfAxwmRvROoYTan151uqy7jfqx8uDuGTANZpdL5ZJJZXkHwINWDnXzwSGeUcdX41SHBtKJ5VWE8DDdHf5mYLk+ZeU=</Q>" +
        "<DP>7EJp5T6O8zgjf8ICtn6GI4hl2viLt7ZH9t5c+bdatbPC3uzhyEvrsbasrIDopYJharDdliz6p671jaZxVb68pq208bjYH1cW9l7yck4Sf3LRgdnt+u0yOSp8pF3ARrwK/A37586+Oe+OxKsgn4GzK63p5kDPGt1oFKEZKprYEHE=</DP>" +
        "<DQ>cmyX0pk/vwR/n0R4Ul49BoncF5700LDrLYZ6VLHw6kuK7Iq64iB7y+JShjQh45eQfqk450iy3mqngiT6gQPufWLoI1JHrknlUEyy0kEFiCZT35QrFcgqI2YSq8BoZylHBKQOD3ryRclNUOaMOtkKCarKz7s39YiVBvhXSpkl5dE=</DQ>" +
        "<InverseQ>W/292doQX50Wv7Vp5h6Dx5KSClY3oiInzHqxk2opa6++n94FSZ0+cxLvtNE2nAlo9vtPTYPwnYR4OjqbjPv6/SQaZwfO9ZkD5VSO8cLYMiDljen1VzjdV88YzOJG/+OTOxpigjM36Z8TYa8IVBx7OAx6HNLyiaKNUvcdluEJKu8=</InverseQ>" +
        "<D>CWhX81pzlrnb/kSC4/vXttUIyzc7ogxg2YtNjnaRPmUEbsiRDjbVKB/v2uqtONuHKZg0HpmyZ4bdQeqbYUUj9p8V4JGdwBKJXj80+yhslMA5khYhSXIQtAKK920cbTGZ0AQAWiMH1B93KdGSZ8FmvbDwAXPdsVb5WD3IcmAA1Y6JhIuZNn86xMv0KcfNHCp9IVEJbeoJZFDCJZhDDpF78JM6iIOJOti6XSAGtwOVgvccoH+43aVYUxZHHisjP/5Zn5PiQNPc8KZLWiC+pmP3IjfHhIVz8Z6GvTIP9gYMg6kNu0TVFnAiYqD4665nX8kjzYcUsEOV4IqmLTpI8kk1yQ==</D>" +
        "</RSAKeyValue>";
}
