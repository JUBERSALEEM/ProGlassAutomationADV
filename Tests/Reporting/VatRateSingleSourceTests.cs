using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using ProGlassAutomation.Services.Reporting;
using Xunit;

namespace ProGlassAutomation.Tests.Reporting;

/// <summary>
/// The UAE VAT rate is a government number that will eventually change. When
/// it does, every calculation must move together.
///
/// <para>
/// Until 2026-09-08 it did not. <see cref="VatCalculator"/> declared the rate
/// canonically, but three money paths hardcoded their own copy:
/// </para>
///
/// <list type="bullet">
///   <item><description><c>TaxInvoiceLineService.CalculateFinancials</c> — a
///     default parameter <c>vatRate = 0.05m</c></description></item>
///   <item><description><c>TaxInvoiceDetailViewModel.PrivateHelpers</c> —
///     <c>Math.Round(afterDiscount * 0.05m, 2)</c></description></item>
///   <item><description><c>ProfitLossStepViewModel.VatDivisor</c> — its own
///     <c>1.05m</c>, named "divisor" while the canonical one is named
///     "multiplier"</description></item>
/// </list>
///
/// <para>
/// All three now reference <see cref="VatCalculator"/>. This is the same
/// defect class as the Job Order / Delivery Note status splits and the target
/// margin tolerance: one business value, several declarations, no error when
/// they disagree.
/// </para>
/// </summary>
public class VatRateSingleSourceTests
{
    // ── The values themselves ──────────────────────────────────────────

    [Fact]
    public void Vat_Constants_Are_Internally_Consistent()
    {
        // The three constants must describe the SAME rate. If someone edits
        // one and not the others, gross-up and rate-application diverge.
        Assert.Equal(5.0m, VatCalculator.VAT_RATE_PERCENT);
        Assert.Equal(0.05m, VatCalculator.VAT_RATE_DECIMAL);
        Assert.Equal(1.05m, VatCalculator.VAT_MULTIPLIER);

        Assert.Equal(VatCalculator.VAT_RATE_PERCENT / 100m, VatCalculator.VAT_RATE_DECIMAL);
        Assert.Equal(1m + VatCalculator.VAT_RATE_DECIMAL, VatCalculator.VAT_MULTIPLIER);
    }

    /// <summary>
    /// Gross-up and rate-application must agree to the fils. This is the
    /// arithmetic that breaks if the constants drift apart.
    /// </summary>
    [Theory]
    [InlineData(100)]
    [InlineData(1234.56)]
    [InlineData(0.01)]
    [InlineData(99999.99)]
    public void Gross_Up_Matches_Rate_Application(double net)
    {
        var n = (decimal)net;

        var grossByMultiplier = n * VatCalculator.VAT_MULTIPLIER;
        var grossByRate = n + Math.Round(n * VatCalculator.VAT_RATE_DECIMAL, 2);

        Assert.True(Math.Abs(grossByMultiplier - grossByRate) <= 0.01m,
            $"net {n}: multiplier gives {grossByMultiplier}, rate gives {grossByRate}");
    }

    // ── The ratchet ────────────────────────────────────────────────────

    /// <summary>Files permitted to contain a VAT literal.</summary>
    private static readonly string[] Allowed =
    {
        "Services/Reporting/VatCalculator.cs",
    };

    /// <summary>
    /// Fails if a VAT calculation is written with its own literal instead of
    /// referencing <see cref="VatCalculator"/>.
    ///
    /// <para>
    /// Scoped deliberately. A bare <c>0.05m</c> is not automatically VAT —
    /// <c>ProfitLossStepViewModel.EffectiveYieldFactor</c> uses <c>0.05m</c> as
    /// a wastage-yield floor, which is a different concept and must stay
    /// independent. So this only flags a literal sitting next to VAT-specific
    /// vocabulary.
    /// </para>
    /// </summary>
    [Fact]
    public void No_File_Hardcodes_Its_Own_Vat_Rate()
    {
        var root = Root();
        var offenders = new List<string>();

        var vatWord = new Regex(@"\bvat\b|VAT_|Vat[A-Z]|VatDivisor|VatRate|VatAmount",
            RegexOptions.IgnoreCase);
        var literal = new Regex(@"\b1\.05m\b|\b0\.05m\b");

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
                if (t.Contains("VatCalculator")) continue;

                // A VAT-named identifier within two lines of a bare literal.
                if (!vatWord.IsMatch(t)) continue;
                if (!literal.IsMatch(t))
                {
                    var window = string.Join(" ", lines
                        .Skip(Math.Max(0, i - 1))
                        .Take(3)
                        .Where(l => !l.TrimStart().StartsWith("//")));
                    if (!literal.IsMatch(window)) continue;
                }

                offenders.Add($"{rel}:{i + 1}  {t.Trim()}");
            }
        }

        Assert.True(offenders.Count == 0,
            "These files apply VAT with their own numeric literal instead of " +
            "VatCalculator. The UAE rate will change one day; these would not:\n  " +
            string.Join("\n  ", offenders.Distinct().OrderBy(x => x)));
    }

    /// <summary>
    /// The wastage-yield floor shares the value 0.05 but is NOT VAT. This
    /// guard keeps the ratchet above from being "fixed" by widening it into
    /// something that forces an unrelated constant to move with the tax rate.
    /// </summary>
    [Fact]
    public void Yield_Floor_Is_Not_Vat_And_Stays_Independent()
    {
        var src = File.ReadAllText(Path.Combine(
            Root(),
            "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.TargetPricing.cs"));

        Assert.Contains("return yield <= 0m ? 0.05m : yield;", src);
        Assert.DoesNotContain("VatCalculator", src.Split("EffectiveYieldFactor()")[1].Split("}")[0]);
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
