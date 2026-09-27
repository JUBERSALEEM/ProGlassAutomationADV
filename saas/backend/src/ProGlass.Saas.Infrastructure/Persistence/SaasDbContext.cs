using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Tenancy;

namespace ProGlass.Saas.Infrastructure.Persistence;

/// <summary>
/// The SaaS DbContext. Two independent tenant-isolation layers are always
/// active (ADR-009):
///
///   1. EF Core GLOBAL QUERY FILTER on every <see cref="ITenantOwned"/>
///      entity, comparing TenantId against <see cref="CurrentTenantId"/>
///      (from the authenticated <see cref="ITenantContext"/> — never from
///      client input).
///   2. PostgreSQL ROW-LEVEL SECURITY (migration 0005_rls.sql), driven by
///      the app.tenant_id session GUC set by <see cref="TenantSessionInterceptor"/>.
///
/// Either layer alone blocks cross-tenant reads; together a bug in one
/// cannot expose another tenant's data. Tenant isolation is integration-
/// tested in ProGlass.Saas.IntegrationTests and in
/// saas/database/verify/apply_and_test.py (23 live checks).
///
/// Schema ownership: this context NEVER creates or migrates the schema
/// (EnsureCreated/Migrate are not used). DDL belongs to
/// <see cref="SqlMigrationRunner"/> — SQL-first migrations (ADR-008).
/// </summary>
public sealed class SaasDbContext : DbContext
{
    private readonly ITenantContext? _tenant;

    public SaasDbContext(DbContextOptions<SaasDbContext> options, ITenantContext? tenant = null)
        : base(options)
    {
        _tenant = tenant;
    }

    // ── Platform ──────────────────────────────────────────────────────
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Feature> Features => Set<Feature>();
    public DbSet<PlanFeature> PlanFeatures => Set<PlanFeature>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();
    public DbSet<TenantFeature> TenantFeatures => Set<TenantFeature>();

    // ── Identity ──────────────────────────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    // ── Master data ───────────────────────────────────────────────────
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Salesman> Salesmen => Set<Salesman>();

    // ── Sheet Store ───────────────────────────────────────────────────
    public DbSet<GlassSheet> GlassSheets => Set<GlassSheet>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<StockTransaction> StockTransactions => Set<StockTransaction>();

    // ── Documents ─────────────────────────────────────────────────────
    public DbSet<Inquiry> Inquiries => Set<Inquiry>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<Specification> Specifications => Set<Specification>();
    public DbSet<SpecificationUsage> SpecificationUsages => Set<SpecificationUsage>();
    public DbSet<TenantPricingConfig> TenantPricingConfigs => Set<TenantPricingConfig>();
    public DbSet<TenantVatConfig> TenantVatConfigs => Set<TenantVatConfig>();
    public DbSet<ProformaInvoice> ProformaInvoices => Set<ProformaInvoice>();
    public DbSet<SpecGroup> SpecGroups => Set<SpecGroup>();
    public DbSet<SpecGroupItem> SpecGroupItems => Set<SpecGroupItem>();
    public DbSet<OtherCharge> OtherCharges => Set<OtherCharge>();
    public DbSet<WastageRow> WastageRows => Set<WastageRow>();
    public DbSet<CustomCharge> CustomCharges => Set<CustomCharge>();
    public DbSet<PnlRow> PnlRows => Set<PnlRow>();
    public DbSet<JobOrder> JobOrders => Set<JobOrder>();
    public DbSet<ProductionPiece> ProductionPieces => Set<ProductionPiece>();
    public DbSet<ProductionEvent> ProductionEvents => Set<ProductionEvent>();
    public DbSet<MaterialReservation> MaterialReservations => Set<MaterialReservation>();
    public DbSet<DeliveryNote> DeliveryNotes => Set<DeliveryNote>();
    public DbSet<DeliveryNoteItem> DeliveryNoteItems => Set<DeliveryNoteItem>();

    // ── Fiscal ────────────────────────────────────────────────────────
    public DbSet<TaxInvoice> TaxInvoices => Set<TaxInvoice>();
    public DbSet<CreditNote> CreditNotes => Set<CreditNote>();
    public DbSet<Statement> Statements => Set<Statement>();
    public DbSet<StatementLine> StatementLines => Set<StatementLine>();

    // ── Optimization ──────────────────────────────────────────────────
    public DbSet<OptimizationSession> OptimizationSessions => Set<OptimizationSession>();
    public DbSet<OptimizationSheet> OptimizationSheets => Set<OptimizationSheet>();
    public DbSet<OptimizationPlacement> OptimizationPlacements => Set<OptimizationPlacement>();
    public DbSet<OptimizationCut> OptimizationCuts => Set<OptimizationCut>();
    public DbSet<OptimizationLeftover> OptimizationLeftovers => Set<OptimizationLeftover>();

    /// <summary>
    /// Tenant scope for the query filters. Guid.Empty (unauthenticated or
    /// platform context) matches no row — the filters fail closed.
    /// </summary>
    public Guid CurrentTenantId => _tenant?.TenantId ?? Guid.Empty;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SaasDbContext).Assembly);

        // Dynamic global query filter for every tenant-owned entity:
        //   e => e.TenantId == this.CurrentTenantId
        // Referencing a DbContext instance property in a query filter is
        // supported by EF Core and re-evaluated per query instance.
        foreach (var entityType in modelBuilder.Model.GetEntityTypes()
                     .Where(e => typeof(ITenantOwned).IsAssignableFrom(e.ClrType)))
        {
            var parameter = Expression.Parameter(entityType.ClrType, "e");
            var tenantId = Expression.Property(parameter, nameof(ITenantOwned.TenantId));
            var current = Expression.Property(
                Expression.Constant(this), nameof(CurrentTenantId));
            var body = Expression.Equal(tenantId, current);
            var filter = Expression.Lambda(body, parameter);
            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(filter);
        }
    }
}
