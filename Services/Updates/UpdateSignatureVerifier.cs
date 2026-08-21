// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Updates/UpdateSignatureVerifier.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — OTA Authenticode signature verification (Phase 10)
// VERSION: Phase 10 — 2026-08-21
//
// PURPOSE (Audit C-1, second half — authenticity):
//   UpdateService.IsAllowedDownloadUrl proves WHERE the installer came
//   from (HTTPS + exact GitHub host). It cannot prove WHO built it. A
//   malicious or tampered binary legitimately hosted on GitHub would pass
//   the origin check and then be executed. This verifier closes that gap:
//
//     1. Extract the signer certificate from the file's Authenticode
//        signature and validate it is a non-expired, Code-Signing
//        certificate from a trusted publisher.
//     2. Verify the signature itself with WinVerifyTrust
//        (WINTRUST_ACTION_GENERIC_VERIFY_V2) — the ONLY check that also
//        validates the file hash. X509Certificate2.CreateFromSignedFile
//        alone is NOT sufficient: it merely extracts the leaf certificate
//        and is trivially bypassed by transplanting a valid certificate
//        onto a tampered executable (SigThief / SigPirate). See
//        Docs/OTA.md §Signature verification.
//
//   The result is a plain decision object — the verifier never throws,
//   never blocks on network I/O (revocation checks are disabled so an
//   offline desktop can still verify the signature chain against the
//   local root store), and never executes or installs anything.
//
// USAGE:
//   var verdict = UpdateSignatureVerifier.Verify(path);
//   if (!verdict.IsValid)  → delete file, block install, audit log
//
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Services.Updates
{
    /// <summary>Result of verifying a downloaded installer's Authenticode signature.</summary>
    public sealed class SignatureVerificationResult
    {
        /// <summary>True only when the signature is present, valid and trusted.</summary>
        public bool IsValid { get; set; }

        /// <summary>Leaf signer subject (e.g. "CN=Juber Saleem, O=…").</summary>
        public string CertificateSubject { get; set; } = "";

        /// <summary>Leaf signer SHA-1 thumbprint (for pinning / support).</summary>
        public string CertificateThumbprint { get; set; } = "";

        /// <summary>Human-readable reason when <see cref="IsValid"/> is false.</summary>
        public string Reason { get; set; } = "";
    }

    /// <summary>
    /// Authenticode signature verification for OTA installers. Static,
    /// side-effect-free (apart from WinVerifyTrust state which is always
    /// closed), never throws.
    /// </summary>
    public static class UpdateSignatureVerifier
    {
        /// <summary>Code Signing EKU OID (RFC 5280 §4.2.1.12 / Authenticode).</summary>
        public const string CodeSigningEkuOid = "1.3.6.1.5.5.7.3.3";

        /// <summary>
        /// Fully verifies an installer file. Returns IsValid=true only when
        /// the file carries a valid Authenticode signature (signature AND
        /// file hash verified by WinVerifyTrust) from a non-expired
        /// Code-Signing certificate that matches the pinned publisher (when
        /// pins are supplied).
        /// </summary>
        /// <param name="filePath">Path of the downloaded installer.</param>
        /// <param name="pinnedThumbprints">
        /// Optional allow-list of leaf-certificate thumbprints (uppercase,
        /// no spaces). Empty/omitted = any trusted Code-Signing certificate
        /// is accepted (WinVerifyTrust still enforces chain trust).
        /// </param>
        /// <param name="pinnedSubjects">
        /// Optional allow-list of certificate subject CNs (exact match).
        /// Empty/omitted = not enforced.
        /// </param>
        /// <param name="now">Injectable clock for tests.</param>
        public static SignatureVerificationResult Verify(
            string filePath,
            string[]? pinnedThumbprints = null,
            string[]? pinnedSubjects = null,
            DateTime? now = null)
        {
            var result = new SignatureVerificationResult();

            if (string.IsNullOrWhiteSpace(filePath))
            {
                result.Reason = "No installer path was provided.";
                return result;
            }

            if (!File.Exists(filePath))
            {
                result.Reason = "The installer file does not exist.";
                return result;
            }

            var clock = now ?? DateTime.UtcNow;

            // ── 1. Extract + sanity-check the signer certificate ───────
            // Uses GetCertContentType + the certificate constructor, the
            // supported replacement for the obsoleted CreateFromSignedFile
            // (SYSLIB0057). This fails for unsigned or tampered files and
            // for files that are not Authenticode-signed at all. The
            // certificate is created directly inside the using so it is
            // disposed on every path (CA2000).
            try
            {
                if (X509Certificate2.GetCertContentType(filePath) !=
                    X509ContentType.Authenticode)
                {
                    result.Reason =
                        "The file is not Authenticode-signed (no code signature found).";
                    return result;
                }

                using (var cert = new X509Certificate2(filePath))
                {
                    result.CertificateSubject = cert.Subject ?? "";
                    result.CertificateThumbprint = cert.Thumbprint ?? "";

                    if (!IsWithinValidityPeriod(cert, clock))
                    {
                        result.Reason =
                            "The signing certificate is expired or not yet valid.";
                        return result;
                    }

                    if (!HasCodeSigningEku(cert))
                    {
                        result.Reason =
                            "The signing certificate does not carry the Code Signing usage.";
                        return result;
                    }

                    if (!MatchesPinnedPublisher(
                            cert.Subject ?? "",
                            cert.Thumbprint ?? "",
                            pinnedSubjects,
                            pinnedThumbprints))
                    {
                        result.Reason =
                            "The signing certificate is not a trusted publisher.";
                        return result;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Reason = $"No valid signer certificate: {ex.Message}";
                return result;
            }

            // ── 2. Verify signature + file hash (the authoritative check) ──
            var trust = AuthenticodeVerifier.VerifyEmbeddedSignature(filePath);
            if (!trust.IsValid)
            {
                result.Reason = trust.Reason;
                return result;
            }

            result.IsValid = true;
            return result;
        }

        /// <summary>
        /// True when the certificate carries the Code Signing EKU.
        /// Pure predicate — testable with a self-issued certificate.
        /// </summary>
        public static bool HasCodeSigningEku(X509Certificate2 cert)
        {
            if (cert == null) return false;

            try
            {
                foreach (var extension in cert.Extensions)
                {
                    if (extension is not X509EnhancedKeyUsageExtension eku)
                        continue;

                    foreach (var oid in eku.EnhancedKeyUsages)
                    {
                        if (string.Equals(oid.Value, CodeSigningEkuOid,
                                StringComparison.Ordinal))
                            return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        /// <summary>
        /// True when the certificate is within its validity window at
        /// <paramref name="now"/>. Pure predicate.
        /// </summary>
        public static bool IsWithinValidityPeriod(
            X509Certificate2 cert, DateTime now)
        {
            if (cert == null) return false;

            try
            {
                var utc = now.Kind == DateTimeKind.Utc
                    ? now
                    : now.ToUniversalTime();

                // NotBefore / NotAfter are UTC for X509 v3 certificates.
                return cert.NotBefore.ToUniversalTime() <= utc &&
                       utc <= cert.NotAfter.ToUniversalTime();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// True when the signer matches the pinned publisher, or when no
        /// pins are configured (chain trust is still enforced by
        /// WinVerifyTrust). Pure predicate.
        /// </summary>
        public static bool MatchesPinnedPublisher(
            string subject,
            string thumbprint,
            string[]? pinnedSubjects,
            string[]? pinnedThumbprints)
        {
            var hasSubjectPins = pinnedSubjects is { Length: > 0 };
            var hasThumbprintPins = pinnedThumbprints is { Length: > 0 };

            if (!hasSubjectPins && !hasThumbprintPins)
                return true;

            if (hasThumbprintPins)
            {
                var normalized = (thumbprint ?? "").Replace(" ", "");
                foreach (var pin in pinnedThumbprints!)
                {
                    if (string.Equals(pin?.Replace(" ", ""), normalized,
                            StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }

            foreach (var pin in pinnedSubjects!)
            {
                if (string.Equals(pin, subject, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Thin P/Invoke wrapper over WinVerifyTrust — the Windows API that
    /// verifies an embedded Authenticode signature AND the file hash
    /// against the local trusted-root store. See Docs/OTA.md.
    /// </summary>
    internal static class AuthenticodeVerifier
    {
        // WINTRUST_ACTION_GENERIC_VERIFY_V2
        private static readonly Guid GenericVerifyV2 =
            new("{00AAC56B-CD44-11d0-8CC2-00C04FC295EE}");

        private const uint WtdUiNone = 2;
        private const uint WtdRevokeNone = 0x00000000;
        private const uint WtdChoiceFile = 1;
        private const uint WtdStateActionVerify = 1;
        private const uint WtdStateActionClose = 2;
        private const uint WtdUseIe4TrustFlag = 0x00000001;
        private const uint WtdSaferFlag = 0x00000100;

        // Common HRESULTs mapped to readable messages.
        private const int TrustENoSignature = unchecked((int)0x800B0100);
        private const int TrustESubjectNotTrusted = unchecked((int)0x800B0004);
        private const int TrustEBadDigest = unchecked((int)0x80096010);
        private const int CertEExpired = unchecked((int)0x800B0101);
        private const int CertERevoked = unchecked((int)0x800B010C);
        private const int CertEUntrustedRoot = unchecked((int)0x800B0109);

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustFileInfo
        {
            public uint cbStruct;
            public IntPtr pcwszFilePath; // LPCWSTR
            public IntPtr hFile;
            public IntPtr pgKnownSubject;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct WinTrustData
        {
            public uint cbStruct;
            public IntPtr pPolicyCallbackData;
            public IntPtr pSIPClientData;
            public uint dwUIChoice;
            public uint fdwRevocationChecks;
            public uint dwUnionChoice;
            public IntPtr pInfoStruct;
            public uint dwStateAction;
            public IntPtr hWVTStateData;
            public IntPtr pwszURLReference;
            public uint dwProvFlags;
            public uint dwUIContext;
        }

        [DllImport("wintrust.dll", SetLastError = true)]
        private static extern int WinVerifyTrust(
            IntPtr hwnd,
            ref Guid pgActionID,
            IntPtr pWVTData);

        /// <summary>Verifies the embedded signature + hash. Never throws.</summary>
        public static SignatureVerificationResult VerifyEmbeddedSignature(
            string filePath)
        {
            var result = new SignatureVerificationResult();

            IntPtr pFilePath = IntPtr.Zero;
            IntPtr pFileInfo = IntPtr.Zero;
            IntPtr pData = IntPtr.Zero;

            try
            {
                var fileInfo = new WinTrustFileInfo
                {
                    cbStruct = (uint)Marshal.SizeOf<WinTrustFileInfo>(),
                    pcwszFilePath = Marshal.StringToHGlobalUni(filePath),
                    hFile = IntPtr.Zero,
                    pgKnownSubject = IntPtr.Zero
                };
                pFilePath = fileInfo.pcwszFilePath;

                pFileInfo = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustFileInfo>());
                Marshal.StructureToPtr(fileInfo, pFileInfo, false);

                var data = new WinTrustData
                {
                    cbStruct = (uint)Marshal.SizeOf<WinTrustData>(),
                    pPolicyCallbackData = IntPtr.Zero,
                    pSIPClientData = IntPtr.Zero,
                    dwUIChoice = WtdUiNone,
                    fdwRevocationChecks = WtdRevokeNone,
                    dwUnionChoice = WtdChoiceFile,
                    pInfoStruct = pFileInfo,
                    dwStateAction = WtdStateActionVerify,
                    hWVTStateData = IntPtr.Zero,
                    pwszURLReference = IntPtr.Zero,
                    dwProvFlags = WtdUseIe4TrustFlag | WtdSaferFlag,
                    dwUIContext = 0
                };

                pData = Marshal.AllocHGlobal(Marshal.SizeOf<WinTrustData>());
                Marshal.StructureToPtr(data, pData, false);

                int hr = WinVerifyTrust(IntPtr.Zero, ref GenericVerifyV2, pData);

                // ── Close the trust provider state — MUST run even on ──
                // failure or WinVerifyTrust leaks provider state. The close
                // HRESULT is captured and logged (CA1806) rather than
                // ignored; a failed close does not change the verdict below.
                var close = Marshal.PtrToStructure<WinTrustData>(pData);
                close.dwStateAction = WtdStateActionClose;
                Marshal.StructureToPtr(close, pData, false);
                int closeHr = WinVerifyTrust(IntPtr.Zero, ref GenericVerifyV2, pData);
                if (closeHr != 0)
                {
                    AppLogService.Warning(
                        $"WinVerifyTrust close returned 0x{closeHr:X8}",
                        new LogContext
                        {
                            Module = "Updates",
                            Operation = "VerifySignature"
                        });
                }

                if (hr == 0)
                {
                    result.IsValid = true;
                    return result;
                }

                result.Reason = DescribeFailure(hr);
                return result;
            }
            catch (Exception ex)
            {
                result.Reason = $"Signature verification failed: {ex.Message}";
                return result;
            }
            finally
            {
                if (pData != IntPtr.Zero) Marshal.FreeHGlobal(pData);
                if (pFileInfo != IntPtr.Zero) Marshal.FreeHGlobal(pFileInfo);
                if (pFilePath != IntPtr.Zero) Marshal.FreeHGlobal(pFilePath);
            }
        }

        private static string DescribeFailure(int hr)
        {
            return hr switch
            {
                TrustENoSignature =>
                    "The installer has no digital signature.",
                TrustESubjectNotTrusted =>
                    "The signature is not from a trusted publisher.",
                TrustEBadDigest =>
                    "The installer was modified after it was signed (bad digest).",
                CertEExpired =>
                    "The signing certificate has expired.",
                CertERevoked =>
                    "The signing certificate has been revoked.",
                CertEUntrustedRoot =>
                    "The signing certificate chain does not chain to a trusted root.",
                _ => $"Signature verification failed (0x{hr:X8})."
            };
        }
    }
}
