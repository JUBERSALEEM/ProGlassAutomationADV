namespace ProGlass.Saas.Domain.Tenancy;

/// <summary>
/// Marker implemented by every entity that belongs to exactly one tenant.
/// The database enforces isolation with Row-Level Security (migration
/// 0005_rls.sql); the application layer enforces it again with a global
/// EF Core query filter built over this interface (SaasDbContext).
/// </summary>
public interface ITenantOwned
{
    /// <summary>Owning tenant. Part of every composite index.</summary>
    Guid TenantId { get; set; }
}
