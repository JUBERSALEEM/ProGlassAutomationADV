// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/LicenseGeneratorCore.cs
// PROJECT: ProGlassAutomation
// MODULE: Developer-only tool — core service (no UI)
//
// The tool's UI (MainWindow) is intentionally thin; every
// meaningful operation goes through this class so the
// service logic is testable without spinning up a WPF
// window. The class is also a documentation surface: the
// "what does each button do" comments live on the methods
// here, not on the click handlers.
//
// FILE LAYOUT
//   • KeyStore + load/save
//   • IssueLicense(...)        — build payload + sign
//   • CurrentFingerprint       — for the "bind to this
//                                 machine" flow
//   • ExportPublicKeyForApp    — pretty-print the public
//                                 key XML the developer
//                                 pastes into the app
//   • WritePublicKeyToSource   — one-click install of the
//                                 public key into the app's
//                                 LicensePublicKey.cs so the
//                                 app and tool always agree
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ProGlassAutomation.Services.Licensing;

namespace ProGlassAutomation.Tools.LicenseGenerator
{
    /// <summary>
    /// UI-free service behind the LicenseGenerator WPF
    /// window. Holds the key store, signs licenses, prints
    /// fingerprints. No thread affinity — safe to call
    /// from any UI dispatcher.
    /// </summary>
    public static class LicenseGeneratorCore
    {
        /// <summary>
        /// Default location of the key store on the
        /// developer's machine.
        /// </summary>
        public static string KeysFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "license-keys.json");

        /// <summary>
        /// Default folder where signed licenses are
        /// written. Created on first use.
        /// </summary>
        public static string DefaultOutputFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ProGlassAutomation", "Licenses");

        // ── Key store ────────────────────────────────────────────

        /// <summary>
        /// Persistent key-pair on the developer's machine.
        /// </summary>
        public sealed class KeyStore
        {
            [JsonPropertyName("publicKeyXml")]
            public string PublicKeyXml { get; set; } = "";

            [JsonPropertyName("privateKeyXml")]
            public string PrivateKeyXml { get; set; } = "";

            [JsonPropertyName("createdUtc")]
            public DateTime CreatedUtc { get; set; }
        }

        /// <summary>
        /// Load the existing key pair, or create a fresh
        /// one if none exists. Never throws — a corrupt
        /// keys file is logged and replaced.
        /// </summary>
        public static KeyStore LoadOrCreateKeyStore(out bool wasCreated)
        {
            wasCreated = false;

            if (File.Exists(KeysFilePath))
            {
                try
                {
                    var ks = JsonSerializer.Deserialize<KeyStore>(
                        File.ReadAllText(KeysFilePath),
                        new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                    if (ks != null
                        && !string.IsNullOrEmpty(ks.PublicKeyXml)
                        && !string.IsNullOrEmpty(ks.PrivateKeyXml))
                    {
                        return ks;
                    }
                }
                catch (System.Exception ex)
                {
                    // Fall through to recreate. The bad file
                    // is left on disk for forensic recovery.
                    ProGlassAutomation.Services.Logging.AppLogService.Debug("[LicenseGenerator] Key-store file unreadable; recreating: " + ex.Message);
                }
            }

            (string publicXml, string privateXml) = LicenseSigner.GenerateKeyPair();
            var store = new KeyStore
            {
                PublicKeyXml = publicXml,
                PrivateKeyXml = privateXml,
                CreatedUtc = DateTime.UtcNow
            };
            SaveKeyStore(store);
            wasCreated = true;
            return store;
        }

        /// <summary>
        /// Save the key store atomically (temp file +
        /// rename) so a crash never leaves a half-written
        /// key store on disk.
        /// </summary>
        public static void SaveKeyStore(KeyStore store)
        {
            string dir = Path.GetDirectoryName(KeysFilePath)!;
            Directory.CreateDirectory(dir);
            string temp = KeysFilePath + ".tmp";
            File.WriteAllText(temp,
                JsonSerializer.Serialize(store,
                    new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(KeysFilePath)) File.Delete(KeysFilePath);
            File.Move(temp, KeysFilePath);
        }

        // ── Issue license ────────────────────────────────────────

        /// <summary>
        /// Parameters for <see cref="IssueLicense"/>. Held
        /// as a record-like class so the WPF code-behind
        /// can build it from form fields without coupling
        /// the WPF controls to the service.
        /// </summary>
        public sealed class IssueRequest
        {
            public LicenseEdition Edition { get; set; } = LicenseEdition.Business;
            public string CustomerId { get; set; } = "";
            public string CompanyName { get; set; } = "";
            public int DaysValid { get; set; } = 365;
            public int MaxActivations { get; set; } = 1;
            public IReadOnlyList<string> Features { get; set; } =
                Array.Empty<string>();
            public string? MachineFingerprint { get; set; }
            public string Notes { get; set; } = "";
        }

        /// <summary>
        /// Result of <see cref="IssueLicense"/>.
        /// </summary>
        public sealed class IssueResult
        {
            public bool Success { get; init; }
            public string Message { get; init; } = "";
            public string OutputPath { get; init; } = "";
            public LicensePayload? Payload { get; init; }
        }

        /// <summary>
        /// Build a payload, sign it with the supplied
        /// private key, and write to the resolved output
        /// path. The output path is computed from the
        /// company name + today's date unless
        /// <paramref name="explicitOutputPath"/> is
        /// supplied (the WPF "Save as…" dialog does that).
        /// </summary>
        public static IssueResult IssueLicense(
            IssueRequest request,
            string privateKeyXml,
            string? explicitOutputPath = null)
        {
            if (request is null) throw new ArgumentNullException(nameof(request));
            if (string.IsNullOrWhiteSpace(privateKeyXml))
                return new IssueResult
                {
                    Success = false,
                    Message = "No signing key is available. " +
                              "Generate a key pair first."
                };
            if (request.DaysValid < 1 || request.DaysValid > 365 * 10)
                return new IssueResult
                {
                    Success = false,
                    Message = "Days-valid must be between 1 and 3650."
                };
            if (request.MaxActivations < 1 || request.MaxActivations > 1000)
                return new IssueResult
                {
                    Success = false,
                    Message = "Max-activations must be between 1 and 1000."
                };

            var payload = new LicensePayload
            {
                LicenseId = Guid.NewGuid().ToString(),
                Edition = request.Edition,
                CustomerId = string.IsNullOrWhiteSpace(request.CustomerId)
                    ? "CUST-" + DateTime.UtcNow.ToString("yyyy-MM") + "-001"
                    : request.CustomerId.Trim(),
                CompanyName = string.IsNullOrWhiteSpace(request.CompanyName)
                    ? "(unnamed customer)"
                    : request.CompanyName.Trim(),
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(request.DaysValid),
                MaxActivations = request.MaxActivations,
                Features = NormaliseFeatures(request.Features),
                MachineFingerprint = string.IsNullOrWhiteSpace(request.MachineFingerprint)
                    ? null
                    : request.MachineFingerprint.Trim(),
                Notes = request.Notes?.Trim() ?? ""
            };

            string outputPath = explicitOutputPath
                ?? ResolveDefaultOutputPath(payload.CompanyName);
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
                LicenseSigner.SignToFile(payload, privateKeyXml, outputPath);
            }
            catch (Exception ex)
            {
                return new IssueResult
                {
                    Success = false,
                    Message = "Sign failed: " + ex.Message
                };
            }

            return new IssueResult
            {
                Success = true,
                Message = "License signed successfully.",
                OutputPath = outputPath,
                Payload = payload
            };
        }

        // ── Misc helpers ─────────────────────────────────────────

        /// <summary>Current machine's fingerprint, hex16.</summary>
        public static string CurrentFingerprint()
            => MachineFingerprint.ComputeCurrent();

        /// <summary>
        /// Pretty-print the public-key XML in the form the
        /// developer pastes into
        /// <c>Services/Licensing/LicensePublicKey.cs</c>.
        /// </summary>
        public static string FormatPublicKeyForApp(string publicKeyXml)
        {
            // Matches the ACTUAL declaration shape used by
            // Services/Licensing/LicensePublicKey.cs — a private-set
            // auto-property with a string-literal initializer (NOT a
            // `const`, which is a different shape and would not paste
            // cleanly). The initializer is a single literal because the
            // RSA XML never contains a double-quote.
            return "    public static string PublicKeyXml { get; private set; } =\n" +
                   "        \"" + publicKeyXml + "\";";
        }

        // ── KEY INSTALL-TO-SOURCE (foolproof public-key setup) ─────
        //
        // THE BUG THIS FIXES (Aug 2026): the generator and the app
        // must agree on the SAME RSA key pair. The app verifies .lic
        // files against the public key embedded in
        // Services/Licensing/LicensePublicKey.cs (PublicKeyXml). The
        // generator signs with the PRIVATE key stored in
        // license-keys.json. If the developer forgets to paste the
        // printed public key into LicensePublicKey.cs (or pastes a
        // stale one), the two keys diverge and every generated
        // license fails with "Signature does not match the payload."
        //
        // WritePublicKeyToSource() removes that manual, error-prone
        // step: it rewrites PublicKeyXml inside the app's actual
        // source file so the embedded public key ALWAYS matches the
        // signing private key on the developer's own machine. One
        // click, then rebuild the app.

        /// <summary>
        /// Locates <c>Services/Licensing/LicensePublicKey.cs</c> by
        /// walking up from the running assembly to the repository
        /// root (the folder that owns ProGlassAutomation.csproj).
        /// Returns null when the tool is not running from a repo
        /// checkout (e.g. a published, source-free build).
        /// </summary>
        public static string? FindAppSourceFile(string fileName)
        {
            try
            {
                var dir = new DirectoryInfo(AppContext.BaseDirectory);
                while (dir != null)
                {
                    if (File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                        return Path.Combine(dir.FullName, "Services", "Licensing", fileName);
                    dir = dir.Parent;
                }
            }
            catch (System.Exception ex)
            {
                // Best-effort locate — if we cannot find the repo we return
                // null and the caller falls back to the manual-paste path.
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    "[LicenseGenerator] FindAppSourceFile failed: " + ex.Message);
                return null;
            }

            return null;
        }

        /// <summary>
        /// Rewrites the <c>PublicKeyXml</c> property inside
        /// <c>Services/Licensing/LicensePublicKey.cs</c> so it
        /// matches <paramref name="publicKeyXml"/> (the public half
        /// of the key pair recorded in
        /// <see cref="KeysFilePath"/>). Returns false with a
        /// message when the source file cannot be located or the
        /// declaration shape changed.
        /// </summary>
        public static bool WritePublicKeyToSource(
            string publicKeyXml, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(publicKeyXml))
            {
                error = "No public key to write. Generate a key pair first.";
                return false;
            }

            string? path = FindAppSourceFile("LicensePublicKey.cs");
            if (path == null)
            {
                error =
                    "Could not locate Services/Licensing/LicensePublicKey.cs.\n" +
                    "Run this tool from a checkout of the repository " +
                    "(the folder containing ProGlassAutomation.csproj). " +
                    "Otherwise copy the public key below into the file manually.";
                return false;
            }

            try
            {
                string src = File.ReadAllText(path);

                // Match the whole declaration, including its multi-line
                // initializer, from the property header up to the closing
                // `</RSAKeyValue>";`. The base64 payload never contains a
                // `;`, so a non-greedy match to the FIRST
                // `</RSAKeyValue>";` is safe for both the placeholder and
                // a real embedded key. The leading whitespace before
                // `public` is outside the match, so it is preserved.
                var rx = new System.Text.RegularExpressions.Regex(
                    @"public\s+static\s+string\s+PublicKeyXml\s*\{\s*get;\s*private\s+set;\s*\}\s*=\s*(.*?)</RSAKeyValue>\s*""\s*;",
                    System.Text.RegularExpressions.RegexOptions.Singleline);
                if (!rx.IsMatch(src))
                {
                    error =
                        "The PublicKeyXml declaration in LicensePublicKey.cs " +
                        "does not match the expected shape. Restore the file " +
                        "from source control and try again.";
                    return false;
                }

                // Build a clean declaration from the public-key XML (no
                // double-quotes inside it, so a single string literal is
                // valid C#). Split Modulus/Exponent for readability.
                string replacement =
                    "public static string PublicKeyXml { get; private set; } =\n" +
                    "            \"<RSAKeyValue>\" +\n" +
                    "            \"<Modulus>" + Extract(publicKeyXml, "Modulus") + "</Modulus>\" +\n" +
                    "            \"<Exponent>" + Extract(publicKeyXml, "Exponent") + "</Exponent>\" +\n" +
                    "            \"</RSAKeyValue>\";";

                string updated = rx.Replace(src, replacement);
                if (updated == src)
                {
                    error = "PublicKeyXml is already up to date.";
                    return false;
                }

                // Write atomically (temp + rename) so a crash mid-write
                // never corrupts the source file.
                string temp = path + ".tmp";
                File.WriteAllText(temp, updated);
                File.Delete(path);
                File.Move(temp, path);

                return true;
            }
            catch (Exception ex)
            {
                error = "Could not write public key to source: " + ex.Message;
                return false;
            }
        }

        /// <summary>
        /// Extracts a base64 element (<c>Modulus</c> / <c>Exponent</c>)
        /// from an RSA XML string.
        /// </summary>
        private static string Extract(string xml, string tag)
        {
            var m = System.Text.RegularExpressions.Regex.Match(
                xml,
                "<" + tag + ">([A-Za-z0-9+/=]+)</" + tag + ">");
            return m.Success ? m.Groups[1].Value : "";
        }

        /// <summary>
        /// True when the <paramref name="publicKeyXml"/> (the generator's
        /// public half) matches the public key embedded in the app's
        /// <c>Services/Licensing/LicensePublicKey.cs</c>. This is the single
        /// check that catches the "signature does not match" bug BEFORE a
        /// license is issued: the app only accepts licenses signed by the
        /// matching private key, so the two must agree. When this returns
        /// false, issuing a license is pointless — it will fail verification
        /// on the app.
        /// </summary>
        public static bool PublicKeyMatchesApp(string publicKeyXml)
        {
            if (string.IsNullOrWhiteSpace(publicKeyXml)) return false;
            try
            {
                string? path = FindAppSourceFile("LicensePublicKey.cs");
                if (path == null) return false;   // not a repo checkout — unknown
                string src = File.ReadAllText(path);
                // Extract the embedded PublicKeyXml modulus from the source.
                var m = System.Text.RegularExpressions.Regex.Match(
                    src,
                    @"<Modulus>([A-Za-z0-9+/=]+)</Modulus>");
                if (!m.Success) return false;     // placeholder / unparseable
                string embeddedMod = m.Groups[1].Value;

                var my = System.Text.RegularExpressions.Regex.Match(
                    publicKeyXml, @"<Modulus>([A-Za-z0-9+/=]+)</Modulus>");
                return my.Success
                    && string.Equals(my.Groups[1].Value, embeddedMod,
                        StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Short fingerprint of an RSA public-key XML (SHA-256 of the
        /// Modulus, first 8 bytes hex). Used to display a compact, human-
        /// comparable identifier for both the generator key and the app key
        /// so a mismatch is obvious.
        /// </summary>
        public static string PublicKeyFingerprint(string publicKeyXml)
        {
            try
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    publicKeyXml, @"<Modulus>([A-Za-z0-9+/=]+)</Modulus>");
                if (!m.Success) return "UNKNOWN";
                byte[] mod = Convert.FromBase64String(m.Groups[1].Value);
                byte[] hash = System.Security.Cryptography.SHA256.HashData(mod);
                var hex = new System.Text.StringBuilder(16);
                for (int i = 0; i < 8; i++) hex.Append(hash[i].ToString("x2"));
                return hex.ToString();
            }
            catch { return "UNKNOWN"; }
        }

        // ── Internals ────────────────────────────────────────────

        private static string ResolveDefaultOutputPath(string companyName)
        {
            string fileName = SanitiseFileName(
                $"{companyName}-{DateTime.UtcNow:yyyy-MM-dd}.lic");
            return Path.Combine(DefaultOutputFolder, fileName);
        }

        private static IReadOnlyList<string> NormaliseFeatures(
            IReadOnlyList<string> input)
        {
            // Trim each entry, drop empties, dedupe.
            // Always honour the global wildcard.
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var raw in input)
            {
                if (string.IsNullOrWhiteSpace(raw)) continue;
                foreach (var piece in raw.Split(
                    ',', StringSplitOptions.RemoveEmptyEntries))
                {
                    string s = piece.Trim();
                    if (s.Length > 0) set.Add(s);
                }
            }
            if (set.Contains("*")) return new[] { "*" };
            return set.ToArray();
        }

        private static string SanitiseFileName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var chars = name.Select(c => invalid.Contains(c) ? '_' : c);
            return new string(chars.ToArray());
        }
    }
}
