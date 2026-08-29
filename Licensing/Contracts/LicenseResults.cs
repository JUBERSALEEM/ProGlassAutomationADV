// ═══════════════════════════════════════════════════════════════════════
// FILE: Licensing/Contracts/LicenseResults.cs
// PROJECT: ProGlass.Licensing
// MODULE: Stable error codes + structured results
//
// Application behavior must NEVER depend on English error strings. Every
// validation outcome maps to a stable <see cref="LicenseErrorCode"/>; the
// caller (generator or app) formats its own user-facing message.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Licensing;

/// <summary>
/// Stable, machine-readable license error codes. Do not renumber existing
/// values (they are part of the public contract); append new ones.
/// </summary>
public enum LicenseErrorCode
{
    None = 0,
    InvalidPayload = 1,
    InvalidSignature = 2,
    Corrupted = 3,
    WrongProduct = 4,
    Expired = 5,
    NotYetValid = 6,
    UnsupportedFormat = 7,
    VersionNotAllowed = 8,
    MachineMismatch = 9,
    DeviceLimitExceeded = 10,
    UnknownKey = 11,
    MissingKey = 12,
    UnboundFingerprintRequired = 13,
    FeatureNotRequested = 14,
    NonCanonicalData = 15,
    EmptyDocument = 16
}

/// <summary>
/// The result of verifying / validating a signed license. Structured:
/// callers read <see cref="IsValid"/> / <see cref="ErrorCode"/> and decide
/// UX; they never parse a message.
/// </summary>
public sealed class LicenseValidationResult
{
    public bool IsValid { get; init; }
    public LicenseErrorCode ErrorCode { get; init; } = LicenseErrorCode.None;
    public string? Message { get; init; }
    public SignedLicense? License { get; init; }
    public LicenseDocument? Document => License?.Document;
    public string? SigningKeyId { get; init; }
}

    /// <summary>
    /// Result of generating + self-verifying a license.
    /// </summary>
    public sealed class LicenseGenerationResult
    {
        public bool Success { get; init; }
        public LicenseErrorCode ErrorCode { get; init; } = LicenseErrorCode.None;
        public string? Message { get; init; }
        public string? KeyId { get; init; }
        public string? OutputPath { get; init; }
        public LicenseDocument? Document { get; init; }

        /// <summary>
        /// The signed license JSON produced by the generation run
        /// (the same bytes that were self-verified). Empty on failure.
        /// Populated so callers can wrap it with
        /// <see cref="LicenseKeyArmor.Encode"/> without re-reading the
        /// output file.
        /// </summary>
        public string? LicenseJson { get; init; }
    }
