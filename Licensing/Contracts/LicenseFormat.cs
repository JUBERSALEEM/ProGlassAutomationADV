// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/Contracts/LicenseFormat.cs
// PROJECT: ProGlass.Licensing
// MODULE: Versioned license-file reader/writer (.proglasslicense)
//
// The license FILE is JSON (UTF-8, pretty for human inspection). It is:
//   • versioned (licenseFormatVersion) — independent of any app version;
//   • deterministic to parse (System.Text.Json is tolerant of key order);
//   • forward-tolerant: unknown extra properties are IGNORED so a future
//     reader can extend the format without breaking older readers;
//   • backward-tolerant: a modern reader understands every older
//     licenseFormatVersion it declares supported (see LicenseReader).
//
// The DIGITAL SIGNATURE is over the CANONICAL rep of the document (not the
// pretty file), so even though the file is pretty-printed for humans, the
// signature is exact. This is why the file can be re-formatted without
// invalidating it, but its *content* cannot be changed without breaking the
// signature (see tamper tests).
// ═══════════════════════════════════════════════════════════════════════

using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProGlass.Licensing;

/// <summary>
/// Format versioning. Bump only on a breaking serialized-shape change; new
/// readers must continue to understand older versions.
/// </summary>
public static class LicenseFormat
{
    public const int Version = 1;
    public const string FileExtension = ".proglasslicense";

    /// <summary>
    /// Hard cap for a signed license JSON document. A real PGKEY1 document
    /// is a few kilobytes; anything larger is rejected before parse.
    /// </summary>
    public const int MaxJsonCharacters = 64 * 1024;

    /// <summary>
    /// Hard cap for a pasted or file-backed PGKEY1 armor block, including
    /// whitespace. Sized to hold a max-length JSON document after base64url
    /// expansion, and nothing larger.
    /// </summary>
    public const int MaxArmorCharacters = 96 * 1024;

    /// <summary>Maximum JSON nesting accepted in a license document.</summary>
    public const int MaxJsonDepth = 16;

    public static readonly int[] SupportedVersions = { 1 };
}

public enum LicenseReadStatus
{
    Parsed,
    UnsupportedFormat
}

public sealed class LicenseReadResult
{
    public LicenseReadStatus Status { get; init; }
    public int FormatVersion { get; init; }
    public SignedLicense? License { get; init; }
    public string? Error { get; init; }
}

public static class LicenseReader
{
    /// <summary>Read a signed license from a .proglasslicense string.</summary>
    public static LicenseReadResult Read(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new LicenseReadResult { Status = LicenseReadStatus.Parsed, Error = "Empty license text." };

        if (json.Length > LicenseFormat.MaxJsonCharacters)
            return new LicenseReadResult { Status = LicenseReadStatus.Parsed, Error = "License text exceeds the safe size limit." };

        SignedLicense? license;
        try
        {
            // Unknown properties are ignored (forward compatibility). Property
            // names are matched exactly, comments and trailing commas are
            // rejected, and depth is capped so a hostile document cannot
            // choose a looser parser than the one that signed it.
            var opts = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = false,
                ReadCommentHandling = JsonCommentHandling.Disallow,
                AllowTrailingCommas = false,
                MaxDepth = LicenseFormat.MaxJsonDepth
            };
            license = JsonSerializer.Deserialize<SignedLicense>(json, opts);
        }
        catch (JsonException ex)
        {
            return new LicenseReadResult { Status = LicenseReadStatus.Parsed, Error = "Malformed JSON: " + ex.Message };
        }
        catch (Exception)
        {
            return new LicenseReadResult { Status = LicenseReadStatus.Parsed, Error = "License text could not be parsed." };
        }

        if (license == null || license.Document == null || string.IsNullOrWhiteSpace(license.Signature.Value))
            return new LicenseReadResult { Status = LicenseReadStatus.Parsed, Error = "License is missing document or signature." };

        if (!LicenseFormat.SupportedVersions.Contains(license.LicenseFormatVersion))
            return new LicenseReadResult
            {
                Status = LicenseReadStatus.UnsupportedFormat,
                FormatVersion = license.LicenseFormatVersion,
                License = license,
                Error = "Unsupported license format version: " + license.LicenseFormatVersion
            };

        return new LicenseReadResult { Status = LicenseReadStatus.Parsed, FormatVersion = license.LicenseFormatVersion, License = license };
    }

    public static LicenseReadResult ReadFile(string path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return new LicenseReadResult { Status = LicenseReadStatus.Parsed, Error = "File does not exist: " + path };

            var info = new FileInfo(path);
            if (info.Length > LicenseFormat.MaxJsonCharacters * 4L)
                return new LicenseReadResult { Status = LicenseReadStatus.Parsed, Error = "License file exceeds the safe size limit." };

            return Read(File.ReadAllText(path));
        }
        catch (Exception)
        {
            return new LicenseReadResult { Status = LicenseReadStatus.Parsed, Error = "License file could not be read." };
        }
    }
}

public static class LicenseWriter
{
    /// <summary>Serialize a signed license to pretty JSON for human inspection.</summary>
    public static string ToJson(SignedLicense license)
    {
        return JsonSerializer.Serialize(license, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>Write to a .proglasslicense path (UTF-8, no BOM).</summary>
    public static void WriteFile(SignedLicense license, string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllText(path, ToJson(license),
            new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }
}
