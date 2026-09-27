namespace ProGlass.Saas.Application.MasterData;

/// <summary>Master-data failure codes (clients branch on the code, per the
/// API §0 error contract).</summary>
public enum MasterDataErrorCode
{
    None = 0,
    ValidationFailed,
    NotFound,
    RenameConflict,
}

/// <summary>Typed result: a value or a code + operator-readable message.</summary>
public sealed record MasterDataResult<T>(
    bool Success, T? Value, MasterDataErrorCode ErrorCode, string Message)
{
    public static MasterDataResult<T> Ok(T value) =>
        new(true, value, MasterDataErrorCode.None, "");

    public static MasterDataResult<T> Fail(MasterDataErrorCode code, string message) =>
        new(false, default, code, message);
}

/// <summary>API design §0 pagination envelope.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);

// ── Requests (API write shape; the WPF editor/auto-save distinction lives
//    in the service methods, not in the DTOs) ────────────────────────────

public sealed record CompanyRequest(
    string Name,
    string Trn,
    string Location,
    string Representative,
    string RepresentativeMobile,
    string Email,
    string AccountNumber,
    string BankName,
    string LogoStorageKey,
    string LogoLayout,
    decimal LogoWidthMm,
    decimal LogoHeightMm,
    bool IsDefault);

public sealed record ClientRequest(
    string CustomerName,
    string CustomerTrn,
    string ReferenceCode,
    string SalesRepresentative,
    string DocumentPreparedBy,
    string BillingAddress,
    string AttentionExecutiveName,
    string Phone);

public sealed record ProjectRequest(
    string IdentifierName,
    string ContractNo,
    string Reference,
    string SiteDeliveryLocation,
    string LpoNumber,
    Guid? CustomerId);

public sealed record SalesmanRequest(
    string Name,
    string Department);

// ── Read models ────────────────────────────────────────────────────────

public sealed record CompanyDto(
    Guid Id, string Name, string Trn, string Location,
    string Representative, string RepresentativeMobile, string Email,
    string AccountNumber, string BankName, string LogoStorageKey,
    string LogoLayout, decimal LogoWidthMm, decimal LogoHeightMm,
    bool IsDefault, int UsageCount, DateTime? LastUsedAtUtc, DateTime CreatedAtUtc);

public sealed record ClientDto(
    Guid Id, string CustomerName, string CustomerTrn, string ReferenceCode,
    string SalesRepresentative, string DocumentPreparedBy, string BillingAddress,
    string AttentionExecutiveName, string Phone,
    int UsageCount, DateTime? LastUsedAtUtc, DateTime CreatedAtUtc);

public sealed record ProjectDto(
    Guid Id, string IdentifierName, string ContractNo, string Reference,
    string SiteDeliveryLocation, string LpoNumber, Guid? CustomerId,
    int UsageCount, DateTime? LastUsedAtUtc, DateTime CreatedAtUtc);

public sealed record SalesmanDto(
    Guid Id, string Name, string Department,
    int UsageCount, DateTime? LastUsedAtUtc, DateTime CreatedAtUtc);

/// <summary>WPF logo placement vocabulary, verbatim
/// (Models/MasterData/CompanyLogoLayout.cs). Stored as text in the DB.</summary>
public static class CompanyLogoLayouts
{
    public const string None = "None";
    public const string LeftPortrait = "LeftPortrait";
    public const string TopPanel = "TopPanel";

    public static readonly string[] All = { None, LeftPortrait, TopPanel };

    public static bool IsValid(string? layout) =>
        All.Contains(layout ?? "", StringComparer.Ordinal);
}
