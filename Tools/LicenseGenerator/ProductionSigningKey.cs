// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/ProductionSigningKey.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Public-key compatibility during production key rotation
//
// The former production private key was exposed in tracked source and is
// treated as compromised. Its private material has been removed from this
// file, and the generator refuses to issue licenses until a replacement is
// provisioned through an owner-approved secure signing process.
//
// The public key below is retained temporarily so existing app trust can be
// reviewed and migrated deliberately. It is public material, not a signing
// secret. The customer app still accepts the corresponding old signatures;
// revoking that trust requires a replacement key and an approved customer
// license migration plan. Never place private signing material in source.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// Public-key compatibility value retained during the controlled rotation.
/// It is not a source for private signing material. License issuance is
/// blocked by <see cref="KeyProtection"/> until a replacement signer is
/// provisioned and its public key is rolled out to the customer app.
/// </summary>
public static class ProductionSigningKey
{
    public const string KeyId = "production-key-2026";

    /// <summary>Public half — also embedded in the customer app.</summary>
    public const string PublicKeyXml =
        "<RSAKeyValue><Modulus>w565u0lCLzyH43JlW9LT+ukiazU63OiL1Q4hA46mja9i2eGGNn/EyAR0h1ntUaj89IYYMPfao4etT5aZTuED9MeTD5HYS8LSXLflf/FmQ2CLzKj+cA51JXFRg+A//mOT7EDyaq7GRoQqY810NT6gySTNEkAzeIc5E4jm0jzl4WE162N5EPUZV3WiVFyN/lXvhCsGlmr+DXftGV2hG6PL8JLa8axdIL0RVL3X56TKwSmQuWIOVBbINGlhPiY5wjKl2IhDHmtuPVSntMLnkdfElmG36HU2aGoOqHuiwzuppvvqz6Zt7V9QlM97m3K42sO6GcxO18iluMAFIv6seri1NQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    // No private key is stored in this source file.

}
