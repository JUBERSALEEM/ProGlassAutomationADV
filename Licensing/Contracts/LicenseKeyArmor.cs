// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/Contracts/LicenseKeyArmor.cs
// PROJECT: ProGlass.Licensing
// MODULE: Compact single-line packaging for signed license documents
//
// PURPOSE
// ───────
// A signed .proglasslicense document is multi-line, pretty-printed JSON.
// Pretty JSON is great for human inspection but fragile to exchange: email
// clients and chat apps re-wrap long lines, and a single lost space breaks
// nothing (the parser is tolerant) but a re-wrapped JSON *looks* damaged to
// the customer. The 2026 workflow simplification therefore exchanges
// licenses as ONE copy-paste text block:
//
//     PGKEY1.<base64url(utf8 license JSON)>
//
// FORMAT RULES
// ────────────
//   • "PGKEY1." is the fixed, versioned prefix (armor format version 1).
//   • The payload is the EXACT pretty JSON produced by LicenseWriter.ToJson,
//     UTF-8, base64url-encoded without padding. Whitespace around the block
//     is tolerated on decode; the decoded bytes must be the signed document.
//   • Decoding NEVER trusts the armor — every decoded document is still
//     signature-verified by the existing VerifyAndValidate path. The armor
//     is transport only, not a security boundary.
//   • TryExtractJson stays tolerant (raw JSON decodes for legacy
//     callers), but the APP boundary (LicenseService.InstallKeyText /
//     InstallKeyFile) accepts armored blocks ONLY via IsArmoredBlock —
//     the customer-facing contract is "paste the key the generator
//     produced".
//
// SECURITY NOTE
// ─────────────
// This format changes NOTHING about signing or verification. A key that did
// not come from the vendor's RSA-2048-PSS private key fails VerifyAndValidate
// exactly as before; the armor only makes the legitimate exchange path easy.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Linq;
using System.Text;

namespace ProGlass.Licensing;

/// <summary>
/// Encode / decode the single-line license-key text block ("PGKEY1.…").
/// Pure .NET. No WPF, no app services.
/// </summary>
public static class LicenseKeyArmor
{
    /// <summary>
    /// Fixed armor prefix. Bump to "PGKEY2." only if the armor format
    /// itself changes in a breaking way; the license format version is a
    /// separate concept tracked inside the signed document.
    /// </summary>
    public const string Prefix = "PGKEY1.";

    /// <summary>
    /// True when the text is an armored key block — whitespace anywhere
    /// tolerated, prefix required, payload non-empty. This is the check
    /// the app uses to enforce "only keys produced by the generator".
    /// </summary>
    public static bool IsArmoredBlock(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        string compact = string.Concat(text.Where(c => !char.IsWhiteSpace(c)));
        return compact.StartsWith(Prefix, StringComparison.Ordinal)
            && compact.Length > Prefix.Length;
    }

    /// <summary>
    /// Wrap a signed license JSON document into a single-line key block.
    /// </summary>
    public static string Encode(string licenseJson)
    {
        if (string.IsNullOrWhiteSpace(licenseJson))
            throw new ArgumentException("A signed license JSON document is required.", nameof(licenseJson));

        byte[] raw = Encoding.UTF8.GetBytes(licenseJson);
        return Prefix + Convert.ToBase64String(raw)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>
    /// Extract the signed license JSON from pasted text. Accepts:
    ///   • an armored "PGKEY1.…" block (whitespace anywhere tolerated),
    ///   • a raw JSON document (starts with '{') — kept at this level so
    ///     legacy callers can still decode old .proglasslicense files.
    /// The APP boundary is stricter: LicenseService.InstallKeyText
    /// accepts armored blocks ONLY (see IsArmoredBlock), because the
    /// customer-facing contract is "paste the key the generator
    /// produced". Returns null when the text is neither. Never throws.
    /// </summary>
    public static string? TryExtractJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        string trimmed = text.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
            return trimmed;

        // Armored: drop all whitespace so paste-wrapping never matters.
        string compact = string.Concat(trimmed.Where(c => !char.IsWhiteSpace(c)));
        if (!compact.StartsWith(Prefix, StringComparison.Ordinal))
            return null;

        string payload = compact.Substring(Prefix.Length);
        if (payload.Length == 0)
            return null;

        string b64 = payload.Replace('-', '+').Replace('_', '/');
        b64 = b64.PadRight(b64.Length + ((4 - b64.Length % 4) % 4), '=');

        try
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(b64));
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
