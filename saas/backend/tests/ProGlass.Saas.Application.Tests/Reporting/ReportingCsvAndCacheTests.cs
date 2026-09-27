// ═══════════════════════════════════════════════════════════════════════
//  Phase 23 — the CSV export path (WPF GenericCsvLayout + ExportFileNamer
//  ports) and the reporting cache (WPF ReportingCacheService port) golden
//  tests. Pure — no database.
// ═══════════════════════════════════════════════════════════════════════

using ProGlass.Saas.Application.Reporting;

namespace ProGlass.Saas.Application.Tests.Reporting;

public sealed class ReportingCsvAndCacheTests
{
    // ── CSV layout ────────────────────────────────────────────────────

    private sealed record Row(string Name, int Qty, decimal Value, string Note);

    [Fact]
    public void Csv_Metadata_Block_Header_And_Rows_Rfc4180()
    {
        var rows = new List<Row>
        {
            new("Acme", 2, 100m, "plain"),
            new("Beta, Inc", 1, 50.5m, "has \"quotes\""),
        };

        var csv = ReportingCsv.Build(
            rows, "Delivery Report",
            new[] { "Customer", "Qty", "Value", "Note" },
            r => new[] { r.Name, r.Qty.ToString(), r.Value.ToString("N2"), r.Note },
            new CsvExportMetadata("Delivery Report", new[]
            {
                ("Tenant", "abc"),
                ("Rows", "2")
            }));

        var lines = csv.Split("\r\n");
        Assert.Equal("REPORT,DELIVERY REPORT", lines[0]);
        Assert.Equal("Tenant,abc", lines[1]);
        Assert.Equal("Rows,2", lines[2]);
        Assert.Equal("", lines[3]);                       // blank separator
        Assert.Equal("Customer,Qty,Value,Note", lines[4]);
        Assert.Equal("Acme,2,100.00,plain", lines[5]);
        Assert.Equal("\"Beta, Inc\",1,50.50,\"has \"\"quotes\"\"\"", lines[6]);
        Assert.EndsWith("\r\n", csv);
    }

    [Fact]
    public void Csv_Sectioned_Report_Groups_By_First_Column()
    {
        var rows = new List<Row>
        {
            new("A", 1, 1m, "x"),
            new("B", 2, 2m, "y"),
            new("A", 3, 3m, "z"),
        };

        var csv = ReportingCsv.Build(
            rows, "Sections",
            new[] { "SECTION", "Qty", "Value" },
            r => new[] { r.Name, r.Qty.ToString(), r.Value.ToString() });

        var lines = csv.Split("\r\n").Where(l => l.Length > 0).ToList();
        // Group key row, then the reduced header, then the reduced rows.
        Assert.Equal("A", lines[0]);
        Assert.Equal("Qty,Value", lines[1]);
        Assert.Equal("1,1", lines[2]);
        Assert.Equal("3,3", lines[3]);
        Assert.Equal("B", lines[4]);
        Assert.Equal("Qty,Value", lines[5]);
        Assert.Equal("2,2", lines[6]);
    }

    [Fact]
    public void Csv_Empty_Rows_Still_Write_The_Header()
    {
        var csv = ReportingCsv.Build(
            new List<Row>(), "Empty", new[] { "A", "B" },
            r => new[] { r.Name, r.Qty.ToString() });
        Assert.Equal("A,B\r\n", csv);
    }

    // ── file naming ───────────────────────────────────────────────────

    [Fact]
    public void FileNamer_Sanitizes_Segments_And_Appends_Date()
    {
        var name = ExportFileNamer.Build(
            "Delivery Report #1", "Acme Glass (LLC)", "ref/2026",
            new DateTime(2026, 9, 27), "csv");
        Assert.Equal("DELIVERY-REPORT-1_ACME-GLASS-LLC_REF2026_27-09-2026.csv", name);
    }

    [Fact]
    public void FileNamer_Blank_Id_Falls_Back_To_DOCUMENT()
    {
        var name = ExportFileNamer.Build(null, null, null, default, "csv");
        Assert.Matches("^DOCUMENT_\\d{2}-\\d{2}-\\d{4}\\.csv$", name);
    }

    // ── header/map sets ───────────────────────────────────────────────

    [Fact]
    public void Layouts_Header_Counts_Match_Map_Lengths()
    {
        Assert.Equal(ReportingCsvLayouts.JoHeaders.Length,
            ReportingCsvLayouts.JoMap(SampleJo()).Length);
        Assert.Equal(ReportingCsvLayouts.DnHeaders.Length,
            ReportingCsvLayouts.DnMap(SampleDn()).Length);
        Assert.Equal(ReportingCsvLayouts.ProfitabilityHeaders.Length,
            ReportingCsvLayouts.ProfitabilityMap(SampleProfit()).Length);
    }

    [Fact]
    public void JoMap_Renders_Delivery_Status_And_Numbers()
    {
        var map = ReportingCsvLayouts.JoMap(SampleJo());
        Assert.Equal("JOB-1", map[0]);
        Assert.Equal("PI-1", map[1]);
        Assert.Equal("1,000.00", map[10]);
        Assert.Equal("3", map[13]);      // delivered
        Assert.Equal("2", map[14]);      // balance
    }

    private static JoReportRow SampleJo() => new(
        Guid.NewGuid(), "JOB-1", Guid.NewGuid(), "PI-1", "Acme Ltd", "Saleh",
        "Acme", "Tower", "Released", "High", new DateOnly(2026, 9, 30),
        new DateTime(2026, 9, 20), 5, 10m, 1_000m, 3);

    private static DeliveryReportRow SampleDn() => new(
        Guid.NewGuid(), Guid.NewGuid(), "PI-1", 1, 1, "6mm Clear", "Acme",
        "Tower", "GR-1", "Clear", "SGU", 1000, 2000, 0, 0, 2, 1, default);

    private static ProfitabilityRowDto SampleProfit() => new(
        "PI-1", "PI-1", "PI", 100m, 60m, 40m, 20m, 40m, 10, 1m, 1,
        true, false, "", "Purchase price (wastage 15%)");

    // ── the cache ─────────────────────────────────────────────────────

    [Fact]
    public async Task Cache_Hits_Within_Ttl_And_Misses_After()
    {
        using var cache = new ReportingCacheService();
        var calls = 0;

        var first = await cache.GetOrAddAsync("k1", () =>
        {
            calls++;
            return Task.FromResult("v1");
        });
        var second = await cache.GetOrAddAsync("k1", () =>
        {
            calls++;
            return Task.FromResult("v2");
        });

        Assert.Equal("v1", first);
        Assert.Equal("v1", second);      // cached, factory not re-run
        Assert.Equal(1, calls);

        // Short expiry elapses → fresh factory run.
        cache.Set("k2", "old", TimeSpan.FromMilliseconds(-1));
        Assert.False(cache.TryGet<string>("k2", out _));
        var (count, expired) = cache.GetStats();
        Assert.Equal(1, count);          // only k1
    }

    [Fact]
    public async Task Cache_Keys_Separate_Tenants_And_Filters()
    {
        using var cache = new ReportingCacheService();
        var t1 = Guid.NewGuid();
        var t2 = Guid.NewGuid();

        var k1 = cache.BuildKey(t1, "executive", new ReportingFilter { Customer = "a" });
        var k2 = cache.BuildKey(t2, "executive", new ReportingFilter { Customer = "a" });
        var k3 = cache.BuildKey(t1, "executive", new ReportingFilter { Customer = "b" });
        var k4 = cache.BuildKey(t1, "executive", null);

        Assert.NotEqual(k1, k2);         // tenant prefix — the security boundary
        Assert.NotEqual(k1, k3);
        Assert.NotEqual(k1, k4);
        Assert.StartsWith(t1.ToString("N"), k1);
    }

    [Fact]
    public async Task Cache_Invalidate_Clears_Everything()
    {
        using var cache = new ReportingCacheService();
        await cache.GetOrAddAsync("a", () => Task.FromResult(1));
        await cache.GetOrAddAsync("b", () => Task.FromResult(2));

        var (count, _) = cache.GetStats();
        Assert.Equal(2, count);

        cache.Invalidate();
        var (after, _) = cache.GetStats();
        Assert.Equal(0, after);
    }

    [Fact]
    public async Task Cache_Does_Not_Cache_Failures()
    {
        using var cache = new ReportingCacheService();
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => cache.GetOrAddAsync<int>("boom",
                () => Task.FromException<int>(new InvalidOperationException("x"))));

        var (count, _) = cache.GetStats();
        Assert.Equal(0, count);
    }
}
