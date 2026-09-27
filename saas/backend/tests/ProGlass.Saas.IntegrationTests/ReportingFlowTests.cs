using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Delivery;
using ProGlass.Saas.Application.Fiscal;
using ProGlass.Saas.Application.JobOrders;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Application.Reporting;
using ProGlass.Saas.Application.Subscriptions;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// The reporting core (Phase 20) against live PostgreSQL: the
/// order-to-cash cohort funnel (Quoted → In production → Delivered →
/// Invoiced → Paid), the headline money cards, the delivery analytics
/// ageing, and the profitability report over the persisted Step 9 P&amp;L.
/// </summary>
[Collection("database")]
public sealed class ReportingFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ReportingService _sut;
    private readonly ProformaService _proformas;
    private readonly FiscalService _fiscal;

    public ReportingFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        _sut = new ReportingService(new ReportingStore(factory));
        _proformas = new ProformaService(
            new ProformaStore(factory), audit,
            new SpecificationsService(
                new SpecificationsStore(factory), audit));
        _fiscal = new FiscalService(new FiscalStore(factory), audit,
            new PlanLimitGuard(new SubscriptionStore(factory)));
    }

    // ── helpers ────────────────────────────────────────────────────────

    private async Task<ProformaDto> CreateQuotedPiAsync(
        Guid tenant, Guid actor, string specName, int w, int h, int qty, decimal rate)
    {
        var pi = (await _proformas.CreateDraftAsync(
            tenant, actor, null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None)).Value!;
        var grid = await _proformas.SaveSpecGroupsAsync(
            tenant, pi.Id, actor, null,
            new SaveSpecGroupsRequest(new List<SpecGroupRequest>
            {
                new(1, "SGU", specName, rate, "manual",
                    new List<SpecGroupItemRequest>
                    {
                        new(1, "Front", w, h, 0, 0, qty, null, ""),
                    }),
            }), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);
        return pi;
    }

    // ── the funnel end-to-end ──────────────────────────────────────────

    [Fact]
    public async Task ExecutiveSummary_CohortFunnel_Profitability_And_Mix()
    {
        var tenant = _fixture.CreateTenant("rep1");
        var actor = Guid.NewGuid();
        var factory = new SaasDbContextFactory(_fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var jobs = new JobOrderService(new JobOrderStore(factory), audit);
        var delivery = new DeliveryService(new DeliveryNoteStore(factory), audit);

        // PI-A: 1000×2000 (2 m²) qty 2 at 100/m² → materials 400, and a
        // saved Step 9 so it is a COSTED quote.
        var piA = await CreateQuotedPiAsync(tenant, actor,
            "6mm Clear FT Glass", 1000, 2000, 2, 100m);
        // Step 6 (empty) + Step 7 refresh the grand total Step 9 reads as
        // gross revenue (the WPF flow: 6 → 7 → 9).
        await _proformas.SaveStep6Async(
            tenant, piA.Id, actor, null,
            new SaveStep6Request(new List<OtherChargeRowRequest>(), null, null),
            CancellationToken.None);
        var step7 = await _proformas.SaveStep7Async(
            tenant, piA.Id, actor, null,
            new SaveStep7Request(null, null, null, null), CancellationToken.None);
        Assert.True(step7.Success, step7.Message);
        var step9 = await _proformas.SaveStep9Async(
            tenant, piA.Id, actor, null,
            new SaveStep9Request(WastagePercent: 15m, CostAtSellingPrice: false),
            CancellationToken.None);
        Assert.True(step9.Success, step9.Message);
        var kpis = step9.Value!.Kpis!;
        Assert.True(kpis.NetExVat > 0m);

        // PI-B: a quote that never converts (the funnel's drop-off).
        var piB = await CreateQuotedPiAsync(tenant, actor,
            "10mm Tinted", 500, 1000, 2, 50m);

        // PI-A converts: job order → full dispatch (2 of 2) → invoice.
        var job = await jobs.CreateFromProformaAsync(
            tenant, actor, new CreateJobOrderRequest(piA.Id),
            null, CancellationToken.None);
        Assert.True(job.Success, job.Message);

        var dn = await delivery.CreateOrOpenDispatchingAsync(
            tenant, actor, job.Value!.Id, null, CancellationToken.None);
        Assert.True(dn.Success, dn.Message);
        var line = dn.Value!.Note!.Items.Single(i => i.SpecGroupNumber == 1);
        var updated = await delivery.UpdateDispatchAsync(
            tenant, dn.Value.Note.Id, actor,
            new UpdateDispatchRequest(null, null, null, null, null, null, null,
                new List<DispatchLineRequest> { new(line.ItemId, 2) }),
            null, CancellationToken.None);
        Assert.True(updated.Success, updated.Message);

        var invoice = await _fiscal.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(
                ProformaId: piA.Id, CustomerName: "Funnel Co",
                Subtotal: 0m,
                Items: new List<TaxInvoiceItemInput>
                {
                    new(1, 1, "6mm Clear FT Glass", "SGU", "Front",
                        1000, 2000, 0, 0, 2, 100m),
                }),
            null, CancellationToken.None);
        Assert.True(invoice.Success, invoice.Message);
        // Derived subtotal: 2 × 2.0 m² × 100 = 400; +5% VAT → 420.
        Assert.Equal(420m, invoice.Value!.GrandTotal);
        var issued = await _fiscal.TransitionAsync(tenant, invoice.Value.Id, actor,
            new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);
        Assert.True(issued.Success, issued.Message);

        // ── The executive summary under the (empty = all-time) filter ──
        var summary = await _sut.GetExecutiveSummaryAsync(
            tenant, new ReportingFilter(), CancellationToken.None);

        Assert.Equal("All time", summary.PeriodDescription);
        Assert.True(summary.HasData);

        // Funnel: 2 quoted → 1 in production → 1 fully delivered →
        // 1 invoiced → 0 paid.
        Assert.Equal(5, summary.Funnel.Count);
        Assert.Equal("Quoted (Proforma)", summary.Funnel[0].Label);
        Assert.Equal(2, summary.Funnel[0].Count);
        Assert.Equal(100m, summary.Funnel[0].StepConversionPct);
        Assert.Equal(1, summary.Funnel[1].Count);   // in production
        Assert.Equal(1, summary.Funnel[2].Count);   // fully delivered
        Assert.Equal(1, summary.Funnel[3].Count);   // invoiced
        Assert.Equal(420m, summary.Funnel[3].Value);
        Assert.Equal(0, summary.Funnel[4].Count);   // paid
        // 1 → 0 paid conversion is a flagged bottleneck (0% < 60%).
        Assert.True(summary.Funnel[4].IsBottleneck);
        Assert.False(summary.Funnel[1].IsBottleneck);
        // Shares of the 2-quote cohort.
        Assert.Equal(50m, summary.Funnel[1].SharePct);
        Assert.Equal(2, summary.Funnel[0].FirstStageCount);

        // Headline money.
        Assert.True(summary.QuotedValue > 0m);
        Assert.Equal(420m, summary.InvoicedValue);
        Assert.Equal(420m, summary.OutstandingValue);   // Issued, not Paid
        Assert.Equal(1, summary.UnconvertedQuotes);
        Assert.Equal(100m, summary.DeliveryCompletionPct);
        Assert.Equal(0, summary.CriticalPendingQty);

        // Profitability enrichment: PI-A costed, PI-B missing.
        Assert.Equal(1, summary.InvoicesWithoutCost);
        Assert.True(summary.HasCostGap);
        Assert.Equal(kpis.NetExVat, summary.Revenue);
        Assert.Equal(kpis.TotalGlassCost + kpis.TotalProductionCost, summary.Cost);

        // Mix: the dominant specification comes from the delivery rows.
        Assert.Equal("6mm Clear FT Glass", summary.DominantSpecification);

        // ── The profitability rows themselves ───────────────────────────
        var rows = await _sut.GetProfitabilityRowsAsync(
            tenant, new ReportingFilter(), CancellationToken.None);
        Assert.Equal(2, rows.TotalCount);

        var rowA = rows.Items.Single(r => r.Key == piA.ProformaNo);
        Assert.True(rowA.HasCostData);
        Assert.Equal(kpis.NetExVat, rowA.Revenue);
        Assert.Equal(kpis.TotalGlassCost, rowA.GlassCost);
        Assert.Equal(kpis.Profit, rowA.Profit);
        Assert.Equal(2, rowA.Qty);
        Assert.Equal(4m, rowA.Sqm);
        Assert.StartsWith("Purchase price (wastage 15%)", rowA.CostSource);
        Assert.False(rowA.IsStaleCost);   // documented: no price stamp in SaaS

        var rowB = rows.Items.Single(r => r.Key == piB.ProformaNo);
        Assert.False(rowB.HasCostData);
        Assert.Equal(0m, rowB.Revenue);
        Assert.Equal("Missing — no Step 9 calculation saved", rowB.CostSource);

        var profit = await _sut.GetProfitabilitySummaryAsync(
            tenant, new ReportingFilter(), CancellationToken.None);
        Assert.Equal(1, profit.InvoicesWithCost);
        Assert.Equal(1, profit.InvoicesWithoutCost);
        Assert.Equal(kpis.NetExVat, profit.TotalRevenue);
        Assert.Equal(kpis.Profit, profit.TotalProfit);

        // ── Delivery analytics: nothing pending (2 of 2 delivered) ─────
        var deliveryAnalytics = await _sut.GetDeliveryAnalyticsAsync(
            tenant, new ReportingFilter(), CancellationToken.None);
        Assert.Equal(0, deliveryAnalytics.PendingQty);
        Assert.Equal(1, deliveryAnalytics.CompletedRows);
    }

    // ── tax invoice summary status semantics ───────────────────────────

    [Fact]
    public async Task TaxInvoiceSummary_Statuses_And_Outstanding()
    {
        var tenant = _fixture.CreateTenant("rep2");
        var actor = Guid.NewGuid();

        // Three standalone invoices: one stays Issued, one is Paid, one
        // goes Overdue.
        var a = await _fiscal.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(CustomerName: "Status Co", Subtotal: 100m),
            null, CancellationToken.None);
        var b = await _fiscal.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(CustomerName: "Status Co", Subtotal: 200m),
            null, CancellationToken.None);
        var c = await _fiscal.CreateAsync(tenant, actor,
            new CreateTaxInvoiceRequest(CustomerName: "Status Co", Subtotal: 400m),
            null, CancellationToken.None);
        Assert.True(a.Success && b.Success && c.Success);

        foreach (var inv in new[] { a, b, c })
        {
            var t = await _fiscal.TransitionAsync(tenant, inv.Value!.Id, actor,
                new TaxInvoiceTransitionRequest("Issued"), null, CancellationToken.None);
            Assert.True(t.Success, t.Message);
        }
        await _fiscal.TransitionAsync(tenant, b.Value!.Id, actor,
            new TaxInvoiceTransitionRequest("Paid"), null, CancellationToken.None);
        await _fiscal.TransitionAsync(tenant, c.Value!.Id, actor,
            new TaxInvoiceTransitionRequest("Overdue"), null, CancellationToken.None);

        var summary = await _sut.GetExecutiveSummaryAsync(
            tenant, new ReportingFilter(), CancellationToken.None);

        // Headline cards: invoiced 105+210+420=735; outstanding excludes
        // the Paid one (105+420=525); overdue count 1.
        Assert.Equal(735m, summary.InvoicedValue);
        Assert.Equal(525m, summary.OutstandingValue);
        Assert.Equal(1, summary.OverdueInvoices);

        // The TI summary is also directly assertable through the store
        // (the executive page composes from it).
        var store = new ReportingStore(new SaasDbContextFactory(_fixture.AppDb));
        var ti = await store.GetTaxInvoiceSummaryAsync(
            tenant, new ReportingFilter(), CancellationToken.None);
        Assert.Equal(3, ti.TotalInvoices);
        Assert.Equal(1, ti.Issued);
        Assert.Equal(1, ti.Paid);
        Assert.Equal(1, ti.Overdue);
        Assert.Equal(525m, ti.Outstanding);
    }
}
