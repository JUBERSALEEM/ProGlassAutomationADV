using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProGlassAutomation.Services.ProformaInvoice.TargetPricing;

namespace ProGlassAutomation.Tests.ProformaInvoice;

/// <summary>
/// Golden tests for the ABOVE / AT / BELOW TARGET band.
///
/// <para>
/// The band was declared eight times with two different values — 0.005m on
/// the Proforma side, 0.05m on the Job Profitability side — so the same
/// invoice could read ABOVE TARGET on the quotation and AT TARGET in Job
/// Profitability. <see cref="TargetMarginTolerance"/> is now the single
/// declaration and every site calls it.
/// </para>
///
/// <para>
/// <see cref="No_Other_File_Declares_Its_Own_Target_Tolerance"/> is the
/// ratchet: it fails if anyone reintroduces a local band, which is how the
/// split happened in the first place.
/// </para>
/// </summary>
public class TargetMarginToleranceTests
{
    // ── The band itself ────────────────────────────────────────────────

    [Fact]
    public void Tolerance_Is_Half_A_Hundredth_Of_A_Percent()
    {
        // If this number changes, Job Profitability and the proforma
        // quotation change together — that is the point. Update this test
        // deliberately, never to make a build green.
        Assert.Equal(0.005m, TargetMarginTolerance.AtTargetPercent);
    }

    [Theory]
    [InlineData(0.005)]   // exactly on the band edge -> ABOVE (inclusive)
    [InlineData(0.01)]
    [InlineData(0.04)]    // was AT TARGET under the old 0.05m band
    [InlineData(0.05)]
    [InlineData(2.5)]
    [InlineData(15.0)]
    public void Above_Target_Golden_Values(double margin)
    {
        var m = (decimal)margin;

        Assert.True(TargetMarginTolerance.IsAbove(m));
        Assert.False(TargetMarginTolerance.IsAt(m));
        Assert.False(TargetMarginTolerance.IsBelow(m));
    }

    [Theory]
    [InlineData(-0.005)]  // exactly on the band edge -> BELOW (inclusive)
    [InlineData(-0.01)]
    [InlineData(-0.04)]   // was AT TARGET under the old 0.05m band
    [InlineData(-0.05)]
    [InlineData(-9.75)]
    public void Below_Target_Golden_Values(double margin)
    {
        var m = (decimal)margin;

        Assert.True(TargetMarginTolerance.IsBelow(m));
        Assert.False(TargetMarginTolerance.IsAt(m));
        Assert.False(TargetMarginTolerance.IsAbove(m));
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.001)]
    [InlineData(-0.001)]
    [InlineData(0.0049)]
    [InlineData(-0.0049)]
    public void At_Target_Golden_Values(double margin)
    {
        var m = (decimal)margin;

        Assert.True(TargetMarginTolerance.IsAt(m));
        Assert.False(TargetMarginTolerance.IsAbove(m));
        Assert.False(TargetMarginTolerance.IsBelow(m));
    }

    /// <summary>
    /// Every margin classifies as exactly one of the three — the invariant
    /// the old code did not satisfy across files.
    /// </summary>
    [Theory]
    [InlineData(0.0)] [InlineData(0.005)] [InlineData(-0.005)]
    [InlineData(0.0049)] [InlineData(-0.0049)] [InlineData(1.0)] [InlineData(-1.0)]
    public void Exactly_One_Band_Per_Margin(double margin)
    {
        var m = (decimal)margin;
        var bands = 0;
        if (TargetMarginTolerance.IsAbove(m)) bands++;
        if (TargetMarginTolerance.IsBelow(m)) bands++;
        if (TargetMarginTolerance.IsAt(m)) bands++;

        Assert.Equal(1, bands);
    }

    [Fact]
    public void Bands_Are_Symmetric_Around_Zero()
    {
        foreach (var v in new[] { 0.005m, 0.02m, 0.049m, 0.05m, 1.0m, 12.5m })
        {
            Assert.Equal(TargetMarginTolerance.IsAbove(v), TargetMarginTolerance.IsBelow(-v));
            Assert.Equal(TargetMarginTolerance.IsAt(v), TargetMarginTolerance.IsAt(-v));
        }
    }

    // ── The ratchet ────────────────────────────────────────────────────

    /// <summary>Files allowed to contain a target-tolerance literal.</summary>
    private static readonly string[] Allowed =
    {
        "Services/ProformaInvoice/TargetPricing/TargetMarginTolerance.cs",
    };

    /// <summary>
    /// Fails if a target-status classification is written with its own
    /// numeric band instead of <see cref="TargetMarginTolerance"/>.
    /// </summary>
    [Fact]
    public void No_Other_File_Declares_Its_Own_Target_Tolerance()
    {
        var root = Root();
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (rel.Contains("/obj/") || rel.Contains("/bin/")) continue;
            if (rel.StartsWith("Tests/", StringComparison.Ordinal)) continue;
            if (Allowed.Contains(rel)) continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var t = lines[i].TrimStart();
                if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;

                // A target-status label or name sitting next to its own
                // numeric band is the split we are preventing.
                var isTargetStatusLine =
                    t.Contains("ABOVE TARGET") || t.Contains("BELOW TARGET") ||
                    t.Contains("\"AboveTarget\"") || t.Contains("\"BelowTarget\"") ||
                    t.Contains("TargetStatus.AboveTarget") || t.Contains("TargetStatus.BelowTarget");

                if (!isTargetStatusLine) continue;
                if (t.Contains("TargetMarginTolerance")) continue;

                // Look at this line and the two either side for a bare band.
                for (var k = Math.Max(0, i - 2); k < Math.Min(lines.Length, i + 3); k++)
                {
                    var probe = lines[k].TrimStart();
                    if (probe.StartsWith("//") || probe.StartsWith("*")) continue;
                    if (Regex.IsMatch(probe, @"\b0\.0*5m\b|\b0\.05m\b|\b0\.005m\b"))
                    {
                        offenders.Add($"{rel}:{i + 1}");
                        break;
                    }
                }
            }
        }

        Assert.True(offenders.Count == 0,
            "These files classify a target margin with their own numeric band instead of " +
            "TargetMarginTolerance, which is how the 0.005m/0.05m split happened:\n  " +
            string.Join("\n  ", offenders.Distinct().OrderBy(x => x)));
    }

    /// <summary>
    /// JobMarginBands is a DIFFERENT band — achieved margin vs BASE margin,
    /// not vs target — and ProfitMarginKpiTests pins its literal. This guard
    /// makes sure unifying the target band never silently reaches into it.
    /// </summary>
    [Fact]
    public void Job_Margin_Bands_Is_A_Separate_Band_And_Stays_Separate()
    {
        var src = File.ReadAllText(
            Path.Combine(Root(), "ViewModels/JobProfitability/JobProfitabilityRow.cs"));

        Assert.Contains("public const decimal TolerancePercent = 0.05m;", src);
        Assert.DoesNotContain("TargetMarginTolerance", src.Split("class JobMarginBands")[1]
            .Split("class ")[0]);
    }

    // ── Helpers ────────────────────────────────────────────────────────

    private static string Root()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null &&
               !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null,
            "Could not locate the repo root (no ProGlassAutomation.csproj above the test output).");
        return dir!.FullName;
    }
}
