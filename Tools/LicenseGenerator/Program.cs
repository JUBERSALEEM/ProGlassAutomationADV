// ═══════════════════════════════════════════════════════════════════════
// FILE: Tools/LicenseGenerator/Program.cs
// PROJECT: ProGlassAutomation
// MODULE: Developer-only tool — issue ProGlass Automation licenses
//
// PURPOSE
// ───────
// Interactive console tool that produces signed .lic files
// for end customers. The output of this tool is what a
// customer drops into %AppData%\ProGlassAutomation\license.lic
// (or what they activate via the welcome view's "Activate
// License" file picker).
//
// WHAT IT DOES
// ────────────
//   1. On first run, generates a fresh RSA-2048 key pair.
//      The private half is stored in
//      %AppData%\ProGlassAutomation\license-keys.json
//      (chmod 600 on POSIX, ICACL "admins only" on Windows).
//      The public half is printed to the console — the
//      developer copies it into
//      Services/Licensing/LicensePublicKey.cs and rebuilds
//      the app.
//   2. On subsequent runs, the existing key pair is
//      loaded from license-keys.json. The developer can
//      sign a license with the [S] menu option.
//
// NEVER SHIPPED
// ─────────────
//   • The .csproj is IsPackable=false.
//   • The tool lives under Tools/ which the
//     <Compile Remove="Tests/**" /> in the main .csproj
//     does NOT touch — but a future installer
//     configuration MUST exclude Tools/ from the bundle.
//   • The private key never appears in source control —
//     it lives only in license-keys.json on the
//     developer's machine. license-keys.json is added
//     to .gitignore by the developer on first run.
//
// USAGE
// ─────
//   > ProGlassAutomation.LicenseGenerator.exe
//
//   LicenseGenerator — first run detected.
//   No signing key found. Generate a new RSA-2048 key pair? [Y/n] y
//   Key pair generated. Public key (paste into LicensePublicKey.cs):
//
//     <RSAKeyValue><Modulus>...</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>
//
//   Private key saved to: C:\Users\...\AppData\Roaming\ProGlassAutomation\license-keys.json
//   Press Enter to continue...
//
//   [S] Sign a license
//   [E] Export public key (print to console)
//   [Q] Quit
//   > s
//   Edition (Trial|Basic|Business|Factory|Enterprise) [Business]: business
//   Customer ID (e.g. CUST-2026-001) [CUST-2026-001]: CUST-2026-042
//   Company name [Acme Glass LLC]: Acme Glass LLC
//   License duration in days [365]: 365
//   Max activations [1]: 3
//   Features (comma-separated, * = all) [Inquiry.*, Proforma.*, JobOrder.*, ...]: *
//   Machine fingerprint (hex16) or empty for portable: <Enter>
//   Notes (optional) [Issued by ...]:
//
//   License signed. Saved to: ...\acme-2026-42.lic
//   Send this file to the customer.
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
    /// Entry point for the license generator tool. The class
    /// itself is static; the methods are split out for
    /// testability.
    /// </summary>
    internal static class Program
    {
        /// <summary>Default location of the key store on the
        /// developer's machine.</summary>
        public static string KeysFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "license-keys.json");

        /// <summary>Default folder where signed licenses are
        /// written. Created on first use.</summary>
        public static string DefaultOutputFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "ProGlassAutomation", "Licenses");

        // ── Entry point ─────────────────────────────────────────

        private static int Main(string[] args)
        {
            try
            {
                // --print-fingerprint is the non-interactive
                // path used by the README and by the welcome
                // view's "send me the fingerprint so I can
                // machine-bind this license" flow.
                if (args.Length == 1
                    && (args[0] == "--print-fingerprint"
                        || args[0] == "-p"))
                {
                    Console.WriteLine(MachineFingerprint.ComputeCurrent());
                    return 0;
                }

                Console.WriteLine();
                Console.WriteLine("ProGlass Automation — License Generator");
                Console.WriteLine("========================================");
                Console.WriteLine();
                Console.WriteLine("This tool signs license files for end customers.");
                Console.WriteLine("The PRIVATE key stays on this machine in:");
                Console.WriteLine("  " + KeysFilePath);
                Console.WriteLine();
                Console.WriteLine("The PUBLIC key is printed for you to paste into");
                Console.WriteLine("Services/Licensing/LicensePublicKey.cs and rebuild.");
                Console.WriteLine();

                var keys = LoadOrCreateKeyStore();
                Console.WriteLine();

                bool quit = false;
                while (!quit)
                {
                    Console.WriteLine("[S] Sign a license");
                    Console.WriteLine("[E] Re-export public key");
                    Console.WriteLine("[P] Print current machine fingerprint");
                    Console.WriteLine("[Q] Quit");
                    Console.Write("> ");
                    string? choice = Console.ReadLine()?.Trim().ToLowerInvariant();
                    Console.WriteLine();

                    switch (choice)
                    {
                        case "s":
                        case "sign":
                            SignInteractive(keys);
                            break;
                        case "e":
                        case "export":
                            ExportPublicKey(keys);
                            break;
                        case "p":
                        case "print":
                            Console.WriteLine("Current machine fingerprint:");
                            Console.WriteLine("  " + MachineFingerprint.ComputeCurrent());
                            break;
                        case "q":
                        case "quit":
                        case "":
                            quit = true;
                            break;
                        default:
                            Console.WriteLine("Unknown choice.");
                            break;
                    }
                    Console.WriteLine();
                }

                Console.WriteLine("Goodbye.");
                return 0;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine();
                Console.Error.WriteLine("FATAL: " + ex.Message);
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }

        // ── Key store ────────────────────────────────────────────

        private sealed class KeyStore
        {
            [JsonPropertyName("publicKeyXml")]
            public string PublicKeyXml { get; set; } = "";

            [JsonPropertyName("privateKeyXml")]
            public string PrivateKeyXml { get; set; } = "";

            [JsonPropertyName("createdUtc")]
            public DateTime CreatedUtc { get; set; }
        }

        private static KeyStore LoadOrCreateKeyStore()
        {
            if (File.Exists(KeysFilePath))
            {
                Console.WriteLine("Found existing key pair: " + KeysFilePath);
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
                catch (Exception ex)
                {
                    Console.WriteLine("Could not read existing key store (" + ex.Message + ").");
                    Console.WriteLine("A new key pair will be generated. " +
                        "The old file is left in place at " + KeysFilePath +
                        " — delete it by hand if you want a clean slate.");
                }
            }

            Console.Write("No signing key found. Generate a new RSA-2048 key pair? [Y/n] ");
            string? resp = Console.ReadLine()?.Trim().ToLowerInvariant();
            if (resp == "n" || resp == "no")
            {
                Console.WriteLine("Cannot continue without a signing key.");
                Environment.Exit(2);
            }

            Console.WriteLine("Generating key pair...");
            (string publicXml, string privateXml) = LicenseSigner.GenerateKeyPair();
            var store = new KeyStore
            {
                PublicKeyXml = publicXml,
                PrivateKeyXml = privateXml,
                CreatedUtc = DateTime.UtcNow
            };
            SaveKeyStore(store);
            Console.WriteLine("Key pair saved to: " + KeysFilePath);
            return store;
        }

        private static void SaveKeyStore(KeyStore store)
        {
            string dir = Path.GetDirectoryName(KeysFilePath)!;
            Directory.CreateDirectory(dir);
            // Atomic write: temp + rename, so a crash never
            // leaves a half-written key store on disk.
            string temp = KeysFilePath + ".tmp";
            File.WriteAllText(temp,
                JsonSerializer.Serialize(store,
                    new JsonSerializerOptions { WriteIndented = true }));
            if (File.Exists(KeysFilePath)) File.Delete(KeysFilePath);
            File.Move(temp, KeysFilePath);
        }

        // ── Export public key ────────────────────────────────────

        private static void ExportPublicKey(KeyStore keys)
        {
            Console.WriteLine("Paste this line into Services/Licensing/LicensePublicKey.cs:");
            Console.WriteLine();
            Console.WriteLine("    public const string PublicKeyXml =");
            Console.WriteLine("        @\"" + keys.PublicKeyXml + "\";");
            Console.WriteLine();
            Console.WriteLine("After pasting, rebuild the app so the new key is embedded.");
        }

        // ── Sign flow ────────────────────────────────────────────

        private static void SignInteractive(KeyStore keys)
        {
            var edition = PromptEnum<LicenseEdition>("Edition", LicenseEdition.Business);
            string customer = PromptString("Customer ID", "CUST-2026-001");
            string company = PromptString("Company name", "Acme Glass LLC");
            int days = PromptInt("License duration in days", 365, min: 1, max: 365 * 10);
            int maxActivations = PromptInt("Max activations (1 for single-machine)", 1, min: 1, max: 100);
            string features = PromptString(
                "Features (comma-separated; * = all; *.<Module> = whole module)",
                "Inquiry.*, Proforma.*, JobOrder.*, DeliveryNote.*, TaxInvoice.*, Statement.*, MasterData.*, SheetStore.*");
            string fingerprint = PromptString("Machine fingerprint (hex16) or empty for portable", "");
            string notes = PromptString("Notes (optional)", "Issued by " + Environment.UserName + " " + DateTime.UtcNow.ToString("yyyy-MM-dd"));

            var payload = new LicensePayload
            {
                LicenseId = Guid.NewGuid().ToString(),
                Edition = edition,
                CustomerId = customer,
                CompanyName = company,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(days),
                MaxActivations = maxActivations,
                Features = SplitFeatures(features),
                MachineFingerprint = string.IsNullOrWhiteSpace(fingerprint) ? null : fingerprint.Trim(),
                Notes = notes
            };

            Directory.CreateDirectory(DefaultOutputFolder);
            string safeName = SanitiseFileName($"{company}-{DateTime.UtcNow:yyyy-MM-dd}.lic");
            string outputPath = Path.Combine(DefaultOutputFolder, safeName);

            LicenseSigner.SignToFile(payload, keys.PrivateKeyXml, outputPath);

            Console.WriteLine();
            Console.WriteLine("License signed.");
            Console.WriteLine("  LicenseId     : " + payload.LicenseId);
            Console.WriteLine("  Edition       : " + payload.Edition);
            Console.WriteLine("  Customer      : " + payload.CustomerId + " (" + payload.CompanyName + ")");
            Console.WriteLine("  Expires       : " + payload.ExpiresAt.ToString("u") + " (in " + days + " days)");
            Console.WriteLine("  Activations   : " + payload.MaxActivations);
            Console.WriteLine("  Features      : " + string.Join(", ", payload.Features));
            Console.WriteLine("  Machine bound : " + (string.IsNullOrEmpty(payload.MachineFingerprint) ? "no (portable)" : payload.MachineFingerprint));
            Console.WriteLine("  Notes         : " + payload.Notes);
            Console.WriteLine();
            Console.WriteLine("  Saved to      : " + outputPath);
            Console.WriteLine();
            Console.WriteLine("Send this file to the customer. They drop it into:");
            Console.WriteLine("  %AppData%\\ProGlassAutomation\\license.lic");
            Console.WriteLine("…or use the welcome view's 'Activate License' button.");
        }

        // ── Input helpers ────────────────────────────────────────

        private static T PromptEnum<T>(string label, T defaultValue) where T : struct, Enum
        {
            string names = string.Join("|", Enum.GetNames<T>());
            while (true)
            {
                Console.Write(label + " (" + names + ") [" + defaultValue + "]: ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) return defaultValue;
                if (Enum.TryParse<T>(input, ignoreCase: true, out T value)) return value;
                Console.WriteLine("  Invalid value. Try again.");
            }
        }

        private static string PromptString(string label, string defaultValue)
        {
            Console.Write(label + " [" + defaultValue + "]: ");
            string? input = Console.ReadLine();
            return string.IsNullOrWhiteSpace(input) ? defaultValue : input.Trim();
        }

        private static int PromptInt(string label, int defaultValue, int min, int max)
        {
            while (true)
            {
                Console.Write(label + " [" + defaultValue + "]: ");
                string? input = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(input)) return defaultValue;
                if (int.TryParse(input, out int v) && v >= min && v <= max) return v;
                Console.WriteLine("  Invalid value. Must be an integer in [" + min + ", " + max + "].");
            }
        }

        private static IReadOnlyList<string> SplitFeatures(string input)
        {
            // Trim each entry, drop empties, dedupe.
            var set = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var raw in input.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string s = raw.Trim();
                if (s.Length > 0) set.Add(s);
            }
            // Always allow the global wildcard.
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
