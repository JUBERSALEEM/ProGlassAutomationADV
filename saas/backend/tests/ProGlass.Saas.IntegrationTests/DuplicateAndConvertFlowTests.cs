using Microsoft.Extensions.Logging.Abstractions;
using ProGlass.Saas.Application.Proforma;
using ProGlass.Saas.Domain.Entities;
using ProGlass.Saas.Domain.Proforma;
using ProGlass.Saas.Infrastructure.Persistence;
using Xunit;

namespace ProGlass.Saas.IntegrationTests;

/// <summary>
/// Duplicate (WPF SavedInvoicesListViewModel.DuplicateAsync + the snapshot
/// scrub) and inquiry → proforma conversion (ConvertToPiAsync +
/// StartFromInquiryAsync + InquiryLinkSync) against live PostgreSQL.
/// </summary>
[Collection("database")]
public sealed class DuplicateAndConvertFlowTests
{
    private readonly DatabaseFixture _fixture;
    private readonly ProformaService _sut;

    public DuplicateAndConvertFlowTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        var factory = new SaasDbContextFactory(fixture.AppDb);
        var audit = new AuditWriter(factory, NullLogger<AuditWriter>.Instance);
        var specs = new SpecificationsService(
            new SpecificationsStore(factory), audit);
        _sut = new ProformaService(new ProformaStore(factory), audit, specs);
    }

    private Task<ProformaResult<ProformaDto>> CreateAsync(Guid tenant) =>
        _sut.CreateDraftAsync(
            tenant, Guid.NewGuid(), null,
            new CreateProformaRequest(null, null, null, null, null, null, null, null),
            CancellationToken.None);

    /// <summary>
    /// A complete document: grid (1 group, 4 SQM at 120) + delivery 50 +
    /// Step 7 summary (grand 556.50) + Step 9 P&amp;L.
    /// </summary>
    private async Task<ProformaInvoice> FullDocumentAsync(Guid tenant)
    {
        var invoice = (await CreateAsync(tenant)).Value!;
        var grid = await _sut.SaveSpecGroupsAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveSpecGroupsRequest(
                new List<SpecGroupRequest>
                {
                    new(1, "SGU", "6mm Clear FT Glass",
                        120m, "formula", new List<SpecGroupItemRequest>
                    {
                        new(1, "Front", 1000, 2000, 0, 0, 2, null, ""),
                    },
                    OuterGlassColor: "Clear",
                    OuterSheetWidthMm: 3210, OuterSheetHeightMm: 2250,
                    OuterSheetPricePerSqm: 100m,
                    Step4CuttingCostPerSqm: 5m, Step4TemperingCostPerSqm: 8m),
                }), CancellationToken.None);
        Assert.True(grid.Success, grid.Message);

        var step6 = await _sut.SaveStep6Async(
            tenant, invoice.Id, Guid.NewGuid(), null, new SaveStep6Request(
                new List<OtherChargeRowRequest>
                {
                    new(ChargeTemplates.DeliveryKey, "Delivery (per trip)", "", "Qty",
                        true, "QTY", new List<int>(), 50m),
                }, null, null), CancellationToken.None);
        Assert.True(step6.Success, step6.Message);

        var step7 = await _sut.SaveStep7Async(
            tenant, invoice.Id, Guid.NewGuid(), null,
            new SaveStep7Request("Percentage", 5m, null, null), CancellationToken.None);
        Assert.True(step7.Success, step7.Message);

        var step9 = await _sut.SaveStep9Async(
            tenant, invoice.Id, Guid.NewGuid(), null,
            new SaveStep9Request(15m, false), CancellationToken.None);
        Assert.True(step9.Success, step9.Message);

        return invoice;
    }

    private Guid SeedInquiry(Guid tenant, string status, string boqJson) =>
        SeedInquiryAsync(tenant, status, boqJson).GetAwaiter().GetResult();

    private async Task<Guid> SeedInquiryAsync(
        Guid tenant, string status, string boqJson)
    {
        var id = Guid.NewGuid();
        using var db = _fixture.CreateContext(tenant);
        db.Inquiries.Add(new Inquiry
        {
            Id = id,
            TenantId = tenant,
            InquiryNo = $"INQ-{tenant.ToString()[..8].ToUpperInvariant()}",
            Status = status,
            StatusText = "",
            Priority = "normal",
            CustomerName = "ACME Contracting",
            ProjectName = "Tower Fitout",
            Salesman = "Test Salesman",
            Notes = "",
            BoqJson = boqJson,
            IsActive = true
        });
        await db.SaveChangesAsync();
        return id;
    }

    private const string Boq = """
        [
          {"sortOrder":1,"glassType":"DGU","specName":"6mm Bronze + 12mm ASP + 6mm Clear",
           "outerGlassColor":"Bronze","innerGlassColor":"Clear",
           "widthMm":2000,"heightMm":1500,"qty":3,"ratePerSqm":125.5},
          {"sortOrder":2,"glassType":"SGU","specName":"6mm Clear FT Glass",
           "widthMm":1000,"heightMm":1000,"qty":2,"ratePerSqm":0},
          {"sortOrder":3,"glassType":"SGU","specName":"",
           "widthMm":900,"heightMm":900,"qty":1},
          {"sortOrder":4,"glassType":"Curtain Wall","specName":"Custom thing",
           "widthMm":800,"heightMm":800,"qty":1}
        ]
        """;

    // ── duplicate ─────────────────────────────────────────────────────

    [DatabaseFact]
    public async Task Duplicate_copies_the_whole_document_as_a_fresh_draft()
    {
        var tenant = _fixture.CreateTenant("Dup");
        var source = await FullDocumentAsync(tenant);

        var result = await _sut.DuplicateAsync(
            tenant, source.Id, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(result.Success, result.Message);

        var copy = result.Value!;
        Assert.NotEqual(source.Id, copy.Id);
        Assert.Equal("Draft", copy.Status);
        Assert.NotEqual(source.ProformaNo, copy.ProformaNo);
        // The WPF duplicate's +7-day validity window.
        Assert.Equal(copy.IssueDate!.Value.AddDays(7), copy.ValidUntil);
        // Financials copied.
        Assert.Equal(source.GlassMaterialsSubtotal, copy.GlassMaterialsSubtotal);
        Assert.Equal(source.OtherChargesSubtotal, copy.OtherChargesSubtotal);
        Assert.Equal(source.DiscountAmount, copy.DiscountAmount);
        Assert.Equal(source.GrandTotalVatInclusive, copy.GrandTotalVatInclusive);

        // The sections round-trip: grid + step 6 + step 9.
        var grid = await _sut.GetSpecGroupsAsync(tenant, copy.Id, CancellationToken.None);
        var group = Assert.Single(grid.Value!);
        Assert.Equal("6mm Clear FT Glass", group.SpecName);
        Assert.Equal(100m, group.OuterSheetPricePerSqm);

        var step6 = await _sut.GetStep6Async(tenant, copy.Id, CancellationToken.None);
        var delivery = Assert.Single(step6.Value!.Charges);
        Assert.Equal(50m, delivery.Rate);

        // The P&L snapshot copied: gross is the source's grand total
        // (528.68 with the 5% discount), net = 528.68/1.05 = 503.50,
        // profit = 503.50 − 460 glass − 102 production.
        var step9 = await _sut.GetStep9Async(tenant, copy.Id, CancellationToken.None);
        Assert.True(step9.Value!.HasBeenCalculated);
        Assert.NotEmpty(step9.Value.Rows);
        Assert.Equal(528.68m, step9.Value.Kpis!.GrossVatInclusive);
        Assert.Equal(-58.50m, step9.Value.Kpis.Profit);

        // Step 7 inputs survive (the snapshot's Step 7 block equivalent).
        var step7 = await _sut.GetStep7Async(tenant, copy.Id, CancellationToken.None);
        Assert.Equal("Percentage", step7.Value!.DiscountType);
        Assert.Equal(5m, step7.Value.DiscountValue);
    }

    [DatabaseFact]
    public async Task Duplicate_detaches_the_inquiry_link()
    {
        var tenant = _fixture.CreateTenant("DupInq");
        var inquiryId = await SeedInquiryAsync(tenant, "Prepared", Boq);

        var converted = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(converted.Success, converted.Message);
        var sourceId = converted.Value!.Invoice.Id;

        var result = await _sut.DuplicateAsync(
            tenant, sourceId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(result.Success, result.Message);

        // A duplicate is a fresh quotation — NOT another PI for the same
        // inquiry (the senior-audit scrub rule).
        Assert.Null(result.Value!.InquiryId);

        // And the inquiry still points at the ORIGINAL PI.
        using (var db = _fixture.CreateContext(tenant))
        {
            var inquiry = db.Inquiries.Single(i => i.Id == inquiryId);
            Assert.Equal(sourceId, inquiry.ConvertedProformaId);
        }

        // Converting again is still refused — the duplicate did not free
        // the inquiry (and did not double-link it either).
        var again = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.False(again.Success);
        Assert.Equal(ProformaErrorCode.Conflict, again.ErrorCode);
    }

    [DatabaseFact]
    public async Task Duplicate_refuses_an_unscrubbable_snapshot()
    {
        var tenant = _fixture.CreateTenant("DupScrub");
        var invoice = (await CreateAsync(tenant)).Value!;

        // A WPF-imported snapshot without a Company block cannot be
        // detached — the duplicate must be REFUSED, never carry the JSON.
        using (var db = _fixture.CreateContext(tenant))
        {
            var row = db.ProformaInvoices.Single(p => p.Id == invoice.Id);
            row.SnapshotJson = "{\"Grid\":{\"Groups\":[]}}";
            db.SaveChanges();
        }

        var result = await _sut.DuplicateAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, CancellationToken.None);
        Assert.False(result.Success);
        Assert.Equal(ProformaErrorCode.Conflict, result.ErrorCode);
        Assert.Contains("detached", result.Message);
    }

    [DatabaseFact]
    public async Task Duplicate_scrubs_a_wpf_shaped_snapshot()
    {
        var tenant = _fixture.CreateTenant("DupSnap");
        var invoice = (await CreateAsync(tenant)).Value!;

        using (var db = _fixture.CreateContext(tenant))
        {
            var row = db.ProformaInvoices.Single(p => p.Id == invoice.Id);
            row.SnapshotJson =
                "{\"Company\":{\"InquiryId\":42,\"ProformaId\":\"PI-2026-0012\"}}";
            db.SaveChanges();
        }

        var result = await _sut.DuplicateAsync(
            tenant, invoice.Id, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(result.Success, result.Message);

        using (var db = _fixture.CreateContext(tenant))
        {
            var copy = db.ProformaInvoices.Single(p => p.Id == result.Value!.Id);
            Assert.NotNull(copy.SnapshotJson);
            Assert.Contains("\"InquiryId\":null", copy.SnapshotJson);
            Assert.Contains(copy.ProformaNo, copy.SnapshotJson);
            Assert.DoesNotContain("PI-2026-0012", copy.SnapshotJson);
        }
    }

    // ── convert ───────────────────────────────────────────────────────

    [DatabaseFact]
    public async Task Convert_creates_a_linked_draft_with_the_boq_grid()
    {
        var tenant = _fixture.CreateTenant("Cvt");
        var inquiryId = await SeedInquiryAsync(tenant, "Prepared", Boq);

        var result = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(result.Success, result.Message);

        var dto = result.Value!;
        Assert.Equal(2, dto.ImportedSpecGroups); // 2 importable of 4 lines
        Assert.Equal(2, dto.SkippedBoqLines);    // partial + unknown build

        var invoice = dto.Invoice;
        Assert.Equal("Draft", invoice.Status);
        Assert.Equal(inquiryId, invoice.InquiryId);

        // The grid mirrors the BOQ in SortOrder; the quoted rate is the
        // customer's own, the unquoted line keeps grid pricing.
        var grid = await _sut.GetSpecGroupsAsync(
            tenant, invoice.Id, CancellationToken.None);
        Assert.Equal(2, grid.Value!.Count);

        var first = grid.Value[0];
        Assert.Equal(1, first.GroupNumber);
        Assert.Equal("DGU", first.GlassType);
        Assert.Equal("6mm Bronze + 12mm ASP + 6mm Clear", first.SpecName);
        Assert.Equal("Bronze", first.OuterGlassColor);
        Assert.Equal("Clear", first.InnerGlassColor);
        Assert.Equal(125.5m, first.RatePerSqm);
        Assert.Equal(9.000m, first.TotalSqm);    // 3.0 × 3

        var second = grid.Value[1];
        Assert.Equal("SGU", second.GlassType);
        Assert.Equal(0m, second.RatePerSqm);      // not quoted yet

        Assert.Equal(1129.50m, invoice.GlassMaterialsSubtotal);

        // The link + the status advance (Prepared → Sent).
        using (var db = _fixture.CreateContext(tenant))
        {
            var inquiry = db.Inquiries.Single(i => i.Id == inquiryId);
            Assert.Equal(invoice.Id, inquiry.ConvertedProformaId);
            Assert.Equal("Sent", inquiry.Status);
        }
    }

    [DatabaseFact]
    public async Task Convert_guards_refuse_bad_states()
    {
        var tenant = _fixture.CreateTenant("CvtGuard");
        var received = await SeedInquiryAsync(tenant, "Received", Boq);
        var cancelled = await SeedInquiryAsync(tenant, "Cancelled", Boq);

        var notEligible = await _sut.ConvertInquiryToProformaAsync(
            tenant, received, Guid.NewGuid(), null, CancellationToken.None);
        Assert.False(notEligible.Success);
        Assert.Equal(ProformaErrorCode.Conflict, notEligible.ErrorCode);
        Assert.Contains("Prepared or Confirmed", notEligible.Message);

        var cancelledToo = await _sut.ConvertInquiryToProformaAsync(
            tenant, cancelled, Guid.NewGuid(), null, CancellationToken.None);
        Assert.False(cancelledToo.Success);

        var unknown = await _sut.ConvertInquiryToProformaAsync(
            tenant, Guid.NewGuid(), Guid.NewGuid(), null, CancellationToken.None);
        Assert.Equal(ProformaErrorCode.NotFound, unknown.ErrorCode);
    }

    [DatabaseFact]
    public async Task Convert_refuses_an_inquiry_with_an_existing_pi()
    {
        var tenant = _fixture.CreateTenant("CvtTwice");
        var inquiryId = await SeedInquiryAsync(tenant, "Prepared", Boq);

        var first = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(first.Success, first.Message);

        var second = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.False(second.Success);
        Assert.Equal(ProformaErrorCode.Conflict, second.ErrorCode);

        // Defense-in-depth: the link column alone is not the guard — a PI
        // linked by a revision (or a restored import) also blocks.
        using (var db = _fixture.CreateContext(tenant))
        {
            db.Inquiries.Single(i => i.Id == inquiryId).ConvertedProformaId = null;
            db.SaveChanges();
        }

        var third = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.False(third.Success);
        Assert.Equal(ProformaErrorCode.Conflict, third.ErrorCode);
    }

    [DatabaseFact]
    public async Task A_confirmed_inquiry_converts_but_keeps_its_status()
    {
        var tenant = _fixture.CreateTenant("CvtConfirmed");
        var inquiryId = await SeedInquiryAsync(tenant, "Confirmed", Boq);

        var result = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(result.Success, result.Message);

        // CanTransition(Confirmed → Sent) is false — the WPF link sync
        // skips silently; the PI operation still succeeds.
        using (var db = _fixture.CreateContext(tenant))
        {
            var inquiry = db.Inquiries.Single(i => i.Id == inquiryId);
            Assert.Equal("Confirmed", inquiry.Status);
            Assert.NotNull(inquiry.ConvertedProformaId);
        }
    }

    [DatabaseFact]
    public async Task An_empty_boq_converts_to_a_blank_grid()
    {
        var tenant = _fixture.CreateTenant("CvtEmpty");
        var inquiryId = await SeedInquiryAsync(tenant, "Prepared", "[]");

        var result = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(result.Success, result.Message);

        Assert.Equal(0, result.Value!.ImportedSpecGroups);
        var grid = await _sut.GetSpecGroupsAsync(
            tenant, result.Value.Invoice.Id, CancellationToken.None);
        Assert.Empty(grid.Value!);
    }

    // ── the PI lifecycle drives the inquiry (InquiryLinkSync) ─────────

    [DatabaseFact]
    public async Task Pi_lifecycle_advances_the_linked_inquiry()
    {
        var tenant = _fixture.CreateTenant("CvtSync");
        var inquiryId = await SeedInquiryAsync(tenant, "Prepared", Boq);

        var converted = await _sut.ConvertInquiryToProformaAsync(
            tenant, inquiryId, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(converted.Success, converted.Message);
        var pi = converted.Value!.Invoice;

        using (var db = _fixture.CreateContext(tenant))
        {
            Assert.Equal("Sent", db.Inquiries.Single(i => i.Id == inquiryId).Status);
        }

        // PI approved → inquiry Confirmed.
        var approved = await _sut.TransitionAsync(
            tenant, pi.Id, Guid.NewGuid(), null,
            new TransitionRequest(nameof(InvoiceLifecycleState.Approved), null),
            CancellationToken.None);
        Assert.True(approved.Success, approved.Message);

        using (var db = _fixture.CreateContext(tenant))
        {
            Assert.Equal("Confirmed",
                db.Inquiries.Single(i => i.Id == inquiryId).Status);
        }

        // PI voided → inquiry Cancelled.
        var voided = await _sut.VoidAsync(
            tenant, pi.Id, Guid.NewGuid(), null, "customer cancelled",
            CancellationToken.None);
        Assert.True(voided.Success, voided.Message);

        using (var db = _fixture.CreateContext(tenant))
        {
            Assert.Equal("Cancelled",
                db.Inquiries.Single(i => i.Id == inquiryId).Status);
        }
    }

    [DatabaseFact]
    public async Task Convert_is_tenant_isolated()
    {
        var tenantA = _fixture.CreateTenant("CvtIsoA");
        var tenantB = _fixture.CreateTenant("CvtIsoB");
        var inquiryA = await SeedInquiryAsync(tenantA, "Prepared", Boq);

        var fromB = await _sut.ConvertInquiryToProformaAsync(
            tenantB, inquiryA, Guid.NewGuid(), null, CancellationToken.None);
        Assert.False(fromB.Success);
        Assert.Equal(ProformaErrorCode.NotFound, fromB.ErrorCode);

        // Tenant A still converts fine, and B's guard saw nothing.
        var fromA = await _sut.ConvertInquiryToProformaAsync(
            tenantA, inquiryA, Guid.NewGuid(), null, CancellationToken.None);
        Assert.True(fromA.Success, fromA.Message);
    }
}
