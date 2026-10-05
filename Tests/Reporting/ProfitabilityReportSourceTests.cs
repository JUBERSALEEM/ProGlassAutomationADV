// Pins the Profitability report to persisted Step 9 P&L.
// Source-level — no SDK. Does not touch PI Step 9.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Reporting
{
    public sealed class ProfitabilityReportSourceTests
    {
        [Fact]
        public void Report_uses_stored_step9_net_and_profit_not_vat_header()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Reporting", "ReportingDataService.Profitability.cs"));

            Assert.Contains("decimal revenue = costed ? pl!.NetExVat : 0m;", src);
            Assert.Contains("decimal profit = costed ? pl!.Profit : 0m;", src);
            Assert.DoesNotContain(
                "VatCalculator.Round(pi.GrandTotalVatInclusive / VatCalculator.VAT_MULTIPLIER)",
                src);
            Assert.DoesNotContain("profit = hasCost ? revenue - cost", src);
        }

        [Fact]
        public void Job_profitability_reads_approved_pi_step9_snapshot()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "JobProfitability",
                "JobProfitabilityViewModel.cs"));

            Assert.Contains("GetByStatusAsync(ProformaInvoiceStatus.Approved)", src);
            Assert.Contains("decimal net = costed ? pl!.NetExVat : 0m;", src);
            Assert.Contains("decimal profit = costed ? pl!.Profit : 0m;", src);
            Assert.Contains("(TotalProfit * 100m) / TotalCost", src);
            Assert.Contains("(TotalProfit * 100m) / TotalNet", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Services", "Reporting",
                    "ReportingDataService.Profitability.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
