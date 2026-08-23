// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Licensing/LicenseSigner.cs
// PROJECT: ProGlassAutomation
// MODULE: Commercial licensing — signing primitives
//
// PURPOSE
// ───────
// Sign a <see cref="LicensePayload"/> with an RSA-2048 private key
// (PSS padding, SHA-256) and write the resulting .lic file. The
// corresponding verifier (LicenseVerifier.cs) uses the embedded
// public key in LicensePublicKey.cs.
//
// WHY RSA, NOT ED25519
// ────────────────────
// • RSA-2048-PSS is universally supported across every .NET
//   version back to .NET Framework 2.0 — no risk of an API
//   rename in a future .NET release breaking every shipped
//   license.
// • The .NET 8 Ed25519 class is `System.Security.Cryptography
//   .Ed25519` and its surface has been in flux across
//   previews — production code that needs a long-lived signing
//   format should not depend on it.
// • RSA-2048 is the algorithm used by the project's own
//   `Services.Security.PasswordHasher` and the Authenticode
//   path in `UpdateSignatureVerifier`. One family for the
//   whole app means the developer only needs one set of tools
//   to issue, audit and re-key.
// • 256-byte signatures are larger than Ed25519's 64 bytes,
//   but a license file is kilobytes anyway and the difference
//   is invisible to a customer.
//
// PSS, NOT PKCS#1 v1.5
// ────────────────────
// RSA-PSS is the modern padding scheme (RFC 8017 §8.1) and
// the verifier in this codebase checks the same scheme. PKCS#1
// v1.5 is also accepted by the verifier for backward
// compatibility with old hand-rolled license tools.
//
// WHERE THIS IS CALLED FROM
// ─────────────────────────
//   • The separate LicenseGenerator tool (Tools/LicenseGenerator).
//   • Tests in Tests/Licensing/LicenseSignerTests.cs.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ProGlassAutomation.Services.Licensing
{
    /// <summary>
    /// Sign license payloads with an RSA-2048 private key and
    /// serialise the signed envelope to a .lic file.
    /// </summary>
    public static class LicenseSigner
    {
        /// <summary>
        /// Top-level envelope version. Bumped on breaking changes
        /// to the file shape (currently always 1).
        /// </summary>
        public const int EnvelopeSchemaVersion = 1;

        /// <summary>
        /// RSA key size in bits. 2048 is the modern baseline
        /// (~112-bit security equivalent) and matches what every
        /// standard PKI uses for code-signing.
        /// </summary>
        public const int KeySizeBits = 2048;

        /// <summary>
        /// The MIME-ish file name suffix for a license file. The
        /// app's file picker and the generator's default both
        /// use this.
        /// </summary>
        public const string FileExtension = ".lic";

        /// <summary>
        /// Sign <paramref name="payload"/> with the supplied
        /// RSA private key XML (the format
        /// <c>RSACryptoServiceProvider.ToXmlString(true)</c>
        /// produces) and return the JSON envelope as a string.
        /// The generator writes the key in this XML format so
        /// it can be hand-edited by the developer in an
        /// emergency; the binary DER form is reserved for
        /// machine-only use.
        /// </summary>
        public static string Sign(LicensePayload payload, string privateKeyXml)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            if (string.IsNullOrWhiteSpace(privateKeyXml))
                throw new ArgumentException(
                    "Private key (RSA XML) is required.", nameof(privateKeyXml));

            using var rsa = RSA.Create(KeySizeBits);
            rsa.FromXmlString(privateKeyXml);

            return SignWithRsa(payload, rsa);
        }

        /// <summary>
        /// Sign <paramref name="payload"/> with a pre-built
        /// <see cref="RSA"/> instance. The caller is responsible
        /// for disposing the key. The key is NOT cloned — the
        /// caller may want to keep the key alive across many
        /// sign operations (the generator does this in a batch
        /// import flow).
        /// </summary>
        public static string SignWithRsa(LicensePayload payload, RSA rsa)
        {
            if (payload is null) throw new ArgumentNullException(nameof(payload));
            if (rsa is null) throw new ArgumentNullException(nameof(rsa));

            string canonical = payload.ToCanonicalJson();
            byte[] message = Encoding.UTF8.GetBytes(canonical);

            // PSS with SHA-256 and a 32-byte salt (the
            // maximum for the key size). Modern, deterministic
            // for the same input + key.
            byte[] signature = rsa.SignData(
                message,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pss);

            // Pretty-print the envelope so an operator can
            // inspect a .lic file in a text editor and see
            // what was issued.
            var envelope = new
            {
                envelopeVersion = EnvelopeSchemaVersion,
                payload,
                signature = Convert.ToBase64String(signature)
            };
            return JsonSerializer.Serialize(envelope, new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.Never
            });
        }

        /// <summary>
        /// Sign and write directly to <paramref name="outputPath"/>.
        /// Creates parent directories if needed. The file is
        /// written atomically (temp file + rename) so a half-
        /// written license never ends up on disk.
        /// </summary>
        public static void SignToFile(
            LicensePayload payload,
            string privateKeyXml,
            string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.",
                    nameof(outputPath));

            string signed = Sign(payload, privateKeyXml);
            var dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            // Atomic write so an interrupted save cannot
            // produce a half-signed license file.
            string tempPath = outputPath + ".tmp";
            File.WriteAllText(tempPath, signed,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            if (File.Exists(outputPath)) File.Delete(outputPath);
            File.Move(tempPath, outputPath);
        }

        /// <summary>
        /// Generates a fresh RSA-2048 key pair and returns both
        /// halves as XML strings (the format used by
        /// <see cref="Sign(LicensePayload, string)"/> and by
        /// <c>LicensePublicKey.Set</c>). The generator calls
        /// this on first run and saves the XML strings to
        /// <c>%AppData%\ProGlassAutomation\license-keys.json</c>.
        /// </summary>
        public static (string PublicKeyXml, string PrivateKeyXml)
            GenerateKeyPair()
        {
            using var rsa = RSA.Create(KeySizeBits);
            return (rsa.ToXmlString(includePrivateParameters: false),
                    rsa.ToXmlString(includePrivateParameters: true));
        }
    }
}
