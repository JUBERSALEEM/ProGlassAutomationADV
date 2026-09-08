using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Tests.Architecture;

/// <summary>
/// Ratchet against the defect class that produced three separate wrong-number
/// bugs in one afternoon on 2026-09-08.
///
/// <para><b>The pattern.</b> A module declares a status vocabulary. Because
/// the persisted property is a <c>string</c> rather than the enum sitting next
/// to it, nothing stops a call site from comparing against a literal of its
/// own choosing. Two call sites choose differently. Neither throws. Two
/// screens disagree.</para>
///
/// <list type="bullet">
///   <item><description>Job Order — <c>"In Production"</c> vs <c>"InProduction"</c>.
///     Overview under-counted jobs in production; the Job Order screen disagreed.</description></item>
///   <item><description>Delivery Note — <c>"Draft"</c> vs <c>"Dispatching"</c>.
///     <c>Preparing + Dispatched</c> did not equal <c>TotalNotes</c>.</description></item>
///   <item><description>Target pricing — <c>0.005m</c> vs <c>0.05m</c> (see
///     <c>TargetMarginToleranceTests</c> for that ratchet).</description></item>
/// </list>
///
/// <para><b>What this test does.</b> It counts comparisons of a
/// <c>.Status</c> property against a bare string literal, per file. The
/// current 60 are grandfathered in <see cref="AllowList"/> with a ceiling per
/// file. A new offending file fails. An allowlisted file that grows fails. The
/// message names the canonical home to use instead.</para>
///
/// <para><b>What it deliberately does not do.</b> It does not flag display
/// labels, <c>switch</c> arms, metadata maps, or the canonical value classes
/// themselves — those are not comparisons against persisted state. It is a
/// ratchet, not a linter: the list shrinks as files migrate, and shrinking it
/// is the point.</para>
/// </summary>
public class StatusLiteralRatchetTests
{
    /// <summary>
    /// Grandfathered offenders, with the maximum number of raw status-literal
    /// comparisons each may contain. Measured 2026-09-08: 60 total.
    ///
    /// Sorted by size so the worst offenders are the obvious next targets.
    /// </summary>
    private static readonly Dictionary<string, int> AllowList = new(StringComparer.Ordinal)
    {
        // 17 — the largest single offender. Mostly per-status filters in the
        // list query; the natural first migration.
        ["Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs"] = 17,

        // 9 — already matches BOTH "PartiallyPaid" and "Partially Paid" in
        // four places. Correct today (nothing stores the spaced form, verified
        // 2026-09-08), but that defence is what made the Job Order split hard
        // to spot. Should collapse onto TaxInvoiceStatusValues.
        ["Services/Reporting/ReportingDataService.TaxInvoice.cs"] = 9,

        // 5 — AI context prompt text. Lower risk: read-only narration.
        ["Services/AI/AIContext/AiContextBuilder.cs"] = 5,

        // 4 — TaxInvoiceMetricsService. Has its own ExcludedFromRevenue /
        // ExcludedFromOverdue sets, which TaxInvoiceStatusValues now mirrors.
        ["Services/Dashboard/TaxInvoiceMetrics/TaxInvoiceMetricsService.cs"] = 4,
        ["Services/Reporting/ReportingDataService.Vat.cs"] = 4,
        ["ViewModels/DeliveryNote/GodView/DeliveryNoteGodViewViewModel.cs"] = 4,
        ["ViewModels/JobOrder/GodView/JobOrderGodViewViewModel.cs"] = 4,

        ["Services/Inquiry/InquiryLifecycleService.cs"] = 3,
        ["Services/TaxInvoice/Pdf/TaxInvoicePdfDocument.cs"] = 3,
        ["ViewModels/TaxInvoice/GodView/TaxInvoiceGodViewViewModel.cs"] = 3,

        // 2 — ReportingDataService.Jo. Explicitly normalises the
        // "InProduction" ↔ "In Production" split; migrate to
        // JobOrderStatusValues.IsInProduction.
        ["Services/Reporting/ReportingDataService.Jo.cs"] = 2,
        ["Services/TaxInvoice/Repository/TaxInvoiceRepository.cs"] = 2,
    };

    /// <summary>Where each module's vocabulary lives, for the failure message.</summary>
    private static readonly (string Match, string Home)[] CanonicalHomes =
    {
        ("DeliveryNote", "DeliveryNoteStatusValues (Models/DeliveryNote/Common/DeliveryNoteStatus/)"),
        ("TaxInvoice",   "TaxInvoiceStatusValues (Models/TaxInvoice/Common/TaxInvoiceStatus/)"),
        ("JobOrder",     "JobOrderStatusValues (Models/JobOrder/Common/JobOrderStatus/)"),
        ("Reporting",    "the owning module's *StatusValues class"),
        ("Inquiry",      "InquiryStatuses (Models/Inquiry/InquiryRecord.cs)"),
        ("Proforma",     "ProformaInvoiceStatus (Models/ProformaInvoice/Common/)"),
    };

    /// <summary>A <c>.Status</c> property compared against a bare literal.</summary>
    private static readonly Regex[] Patterns =
    {
        new(@"\.Status\b[^""\r\n]{0,40}(?:==|!=)\s*""", RegexOptions.Compiled),
        new(@"""[^""\r\n]{0,20}(?:==|!=)[^""\r\n]{0,20}\.Status\b", RegexOptions.Compiled),
        new(@"\.Status\.Equals\s*\(\s*""", RegexOptions.Compiled),
    };

    [Fact]
    public void No_New_Raw_Status_Literal_Comparisons()
    {
        var root = Root();
        var found = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            var rel = Path.GetRelativePath(root, file).Replace('\\', '/');
            if (rel.Contains("/obj/") || rel.Contains("/bin/")) continue;
            if (rel.StartsWith("Tests/", StringComparison.Ordinal)) continue;

            var count = 0;
            foreach (var line in File.ReadAllLines(file))
            {
                var t = line.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                if (Patterns.Any(p => p.IsMatch(line))) count++;
            }

            if (count > 0) found[rel] = count;
        }

        var newFiles = found.Keys.Where(f => !AllowList.ContainsKey(f)).OrderBy(f => f).ToList();
        var grown = found
            .Where(kv => AllowList.TryGetValue(kv.Key, out var max) && kv.Value > max)
            .Select(kv => $"{kv.Key}: now {kv.Value}, ceiling {AllowList[kv.Key]}")
            .OrderBy(x => x)
            .ToList();

        var problems = new List<string>();

        if (newFiles.Count > 0)
        {
            problems.Add("These files compare a .Status property against a bare string literal " +
                         "and are not on the allow-list:\n" +
                         string.Join("\n", newFiles.Select(f =>
                         {
                             var home = CanonicalHomes
                                 .FirstOrDefault(h => f.Contains(h.Match, StringComparison.Ordinal)).Home
                                 ?? "that module's canonical *StatusValues class";
                             return $"  {f} ({found[f]})\n      → use {home}";
                         })));
        }

        if (grown.Count > 0)
        {
            problems.Add("These allow-listed files grew past their ceiling. Route the new " +
                         "comparison through the canonical home, or raise the ceiling " +
                         "deliberately and say why:\n  " + string.Join("\n  ", grown));
        }

        Assert.True(problems.Count == 0, string.Join("\n\n", problems));
    }

    /// <summary>
    /// The other half of a ratchet: a file that has been cleaned up must be
    /// removed from the list, or the ceiling quietly stops meaning anything.
    /// </summary>
    [Fact]
    public void Allow_List_Has_No_Stale_Entries()
    {
        var root = Root();
        var stale = new List<string>();

        foreach (var entry in AllowList)
        {
            var path = Path.Combine(root, entry.Key.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path))
            {
                stale.Add($"{entry.Key} — file no longer exists");
                continue;
            }

            var count = 0;
            foreach (var line in File.ReadAllLines(path))
            {
                var t = line.TrimStart();
                if (t.StartsWith("//") || t.StartsWith("*") || t.StartsWith("/*")) continue;
                if (Patterns.Any(p => p.IsMatch(line))) count++;
            }

            if (count < entry.Value)
                stale.Add($"{entry.Key} — now {count}, ceiling says {entry.Value}; lower it");
        }

        Assert.True(stale.Count == 0,
            "Allow-list entries that no longer offend. Shrink the list so it stays honest:\n  " +
            string.Join("\n  ", stale));
    }

    /// <summary>
    /// A ratchet is worthless if its baseline is wrong. This asserts the
    /// recorded total, so silently editing the regex cannot quietly widen or
    /// narrow what is being counted.
    /// </summary>
    [Fact]
    public void Baseline_Total_Is_Sixty()
    {
        Assert.Equal(60, AllowList.Values.Sum());
    }

    /// <summary>
    /// Every module in the allow-list must actually have a canonical home to
    /// migrate to. Without this the ratchet would say "use the home" for a
    /// module that does not have one.
    /// </summary>
    [Theory]
    [InlineData("Models/JobOrder/Common/JobOrderStatus/JobOrderStatusValues.cs", "JobOrderStatusValues")]
    [InlineData("Models/DeliveryNote/Common/DeliveryNoteStatus/DeliveryNoteStatus.cs", "DeliveryNoteStatusValues")]
    [InlineData("Models/TaxInvoice/Common/TaxInvoiceStatus/TaxInvoiceStatus.cs", "TaxInvoiceStatusValues")]
    [InlineData("Models/Inquiry/InquiryRecord.cs", "InquiryStatuses")]
    [InlineData("Models/ProformaInvoice/Common/ProformaInvoiceStatus.cs", "ProformaInvoiceStatus")]
    public void Every_Module_Has_A_Canonical_Home_To_Migrate_To(string relativePath, string typeName)
    {
        var path = Path.Combine(Root(), relativePath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), $"missing canonical home: {relativePath}");
        Assert.Contains(typeName, File.ReadAllText(path), StringComparison.Ordinal);
    }

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
