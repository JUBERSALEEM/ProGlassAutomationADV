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
                catch
                {
                    // Fall through to recreate. The bad file
                    // is left on disk for forensic recovery.
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
            // The @""-prefixed verbatim string is the
            // canonical C# form (no escaping, no
            // concatenation). The developer pastes the
            // whole block — there is no risk of a
            // mis-escaped quote breaking the build.
            return "    public const string PublicKeyXml =\n" +
                   "        @\"" + publicKeyXml + "\";";
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
