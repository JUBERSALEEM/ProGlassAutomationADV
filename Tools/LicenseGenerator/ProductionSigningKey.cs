// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/ProductionSigningKey.cs
// PROJECT: ProGlassAutomation.LicenseGenerator
// MODULE: Embedded production signing key for single-click license keys
//
// PURPOSE (2026 plan-based activation)
// ────────────────────────────────────
// The generator is now a SINGLE-CLICK signer: pick a plan (7 days,
// 30 days, 6 months, 1 year, 2 years) and press GENERATE. The signing
// key pair is embedded here so the tool needs no "load key file" step.
// The public half is ALSO embedded in the customer app
// (Services/Licensing/LicensePublicKey.GeneratorCompatiblePublicKeyXml),
// which is why every key this tool signs verifies instantly in the app.
//
// SECURITY MODEL (what "secured" means here)
// ──────────────────────────────────────────
//   • The app ONLY accepts keys whose signature verifies against the
//     public half below — a modified, refactored or cracked key fails
//     RSA-2048-PSS verification and is rejected before anything is
//     stored (LicenseService.InstallKeyText verifies BEFORE saving).
//   • The private half below is the vendor's production signing key.
//     Keep this repository / this tool out of customer hands: anyone
//     who obtains the generator binary can produce app-accepted keys.
//     The generator is a vendor-office tool, never shipped to
//     customers and never embedded in the customer app.
//   • To rotate the key later: generate a fresh RSA-2048 pair, put the
//     private half here and the public half in
//     LicensePublicKey.GeneratorCompatiblePublicKeyXml, and rebuild
//     BOTH projects in the same release. Old keys stay valid only if
//     the old public half remains in TrustedLicenseKeys.All.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlassAutomation.Tools.LicenseGenerator;

/// <summary>
/// The single embedded production signing key the generator uses.
/// <see cref="PublicKeyXml"/> MUST stay byte-identical to the app's
/// <c>LicensePublicKey.GeneratorCompatiblePublicKeyXml</c> — a source
/// test pins this equality so the two can never drift apart.
/// </summary>
public static class ProductionSigningKey
{
    public const string KeyId = "production-key-2026";

    /// <summary>Public half — also embedded in the customer app.</summary>
    public const string PublicKeyXml =
        "<RSAKeyValue><Modulus>w565u0lCLzyH43JlW9LT+ukiazU63OiL1Q4hA46mja9i2eGGNn/EyAR0h1ntUaj89IYYMPfao4etT5aZTuED9MeTD5HYS8LSXLflf/FmQ2CLzKj+cA51JXFRg+A//mOT7EDyaq7GRoQqY810NT6gySTNEkAzeIc5E4jm0jzl4WE162N5EPUZV3WiVFyN/lXvhCsGlmr+DXftGV2hG6PL8JLa8axdIL0RVL3X56TKwSmQuWIOVBbINGlhPiY5wjKl2IhDHmtuPVSntMLnkdfElmG36HU2aGoOqHuiwzuppvvqz6Zt7V9QlM97m3K42sO6GcxO18iluMAFIv6seri1NQ==</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>";

    /// <summary>
    /// Private half — generator-only. Never reference this from the
    /// customer app project and never commit it anywhere else.
    /// </summary>
    public const string PrivateKeyXml =
        "<RSAKeyValue><Modulus>w565u0lCLzyH43JlW9LT+ukiazU63OiL1Q4hA46mja9i2eGGNn/EyAR0h1ntUaj89IYYMPfao4etT5aZTuED9MeTD5HYS8LSXLflf/FmQ2CLzKj+cA51JXFRg+A//mOT7EDyaq7GRoQqY810NT6gySTNEkAzeIc5E4jm0jzl4WE162N5EPUZV3WiVFyN/lXvhCsGlmr+DXftGV2hG6PL8JLa8axdIL0RVL3X56TKwSmQuWIOVBbINGlhPiY5wjKl2IhDHmtuPVSntMLnkdfElmG36HU2aGoOqHuiwzuppvvqz6Zt7V9QlM97m3K42sO6GcxO18iluMAFIv6seri1NQ==</Modulus><Exponent>AQAB</Exponent><P>7GWFAcEWhJXGLXWDa+hP0/aWQYjlP8f3lvrPzlqeOH+8aXgzURrN9J7uE3c98Ze6VT+hL0+7m2ynG0OnuLuHvq4VkoTyNqpaNLqgliaDBqYpMlW867mJcqAwfj6IB+4wvSTWQAAD9nRD47ykurI3SIo49iDrE5RK02C0GUTbIIE=</P><Q>09eOxxFEm7QE4tBVGY1t/H+xKszhNmhCwqSPiUAilhrT3r+lhjeZGwhhNGoExKzvEisDfirt1l9wvP43isSuALtk1lDNaDsKTnotuemEdEOfSfenvjBtrI3NR/37FEPsg5/zy0d/flU3ml3ytAlItfjpTOvcTrJoi4i6Qx+turU=</Q><DP>VmSXold9skDGZ3Ct8e651d4ldZfhKFkdTAknjuOhNUbBhc4O+xIPuNmoambGtZdc2rDhaFf8WUXxIE6uah3m8zHN4nlBx+akBuc5rwYuFmz9W07oN0HcUI7oigqyTiunlkhzIHqg+B3vfbhAf5aIC7OwM4s+Pi6Q1gVebGm0p4E=</DP><DQ>hleH8xQvVzK1eoU1woufOWZdG1Bsbk12MOudHyQuCtUekX8l4FT34l3RDyVAut0Ui4f9ohBlXfKOkzwASx9pXYt5ajQ53yS554Sx5NmDJxwPMyA8H2+kza3rhgE6LeLZZauVNs+zuZEfPuTfWm8czlH42dAlmT6o7/wYayL20JU=</DQ><InverseQ>NpSn6A3pIAmmmcsWr4Xs60V3isDy2ndeFXJ0IQOrCasR7pa8JRPlLGxU5rgkmnM9sITTCmt41sVZDsvj78chcpB/PKAV767/Aj7QuvDEwB23XzehKKKZGUCq03PFwmBt3m1ZyoXGnRIdwB8ja3ZwiiouiKycelgZ0us5wNsRVOo=</InverseQ><D>KbwNaBWjFyzU2QOo+EQskZV3kGPU4RiD7LqM9JNUmVnXvyBMYAg2VW4N5mqbJRbmkMyjEJ9wd/8UTKyWnKuJCoohp+q3uMIh20Iopnk8Fhfc+5b6YbHT8F8Sww3u2Tkpb/gTQAt1pcnAuXY12r4303IUhlZIJOjElpRYmnX/nWueUcgSk+iJqA7CBnLI10uNJItwd2YizmK6ugY0bFkBL35xC1BDddgHl5bFB3eZY058gZ4JsZmQG8V5ckk5dK6eAwdzkJmHLP3A68OlJz7m2PoAK38GWlvvF7UOfXeWRl7S7GpEaZpUdDbBGOKwF6d9uBEjXvYwC38DX7iYPYOMAQ==</D></RSAKeyValue>";
}
