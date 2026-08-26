// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProfitLossSguCostingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Locks the SGU Step 9 business rule requested by the user:
//   • SGU glass is costed by required SQM after wastage, not by rounding
//     up to whole stock sheets.
//   • Cutting and tempering remain separate SQM-based charges.
//
// The arithmetic is tested directly via public helpers; the integration
// points are pinned with a source check so the rule cannot silently drift
// back inside ProfitLossStepViewModel.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step9ProfitLoss;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class ProfitLossSguCostingTests
    {
        [Fact]
        public void Area_with_wastage_prices_sgu_glass_by_required_sqm()
        {
            decimal cutSqm = 10m;
            decimal waste = 10m;
            decimal rate = 46m;

            decimal required = PnLMeasurement.AreaWithWastage(cutSqm, waste);
            decimal amount = decimal.Round(required * rate, 2);

            Assert.Equal(11.0000m, required);
            Assert.Equal(506.00m, amount);
        }

        [Fact]
        public void Whole_sheets_required_is_still_available_for_multi_pane_builds()
        {
            Assert.Equal(2, PnLMeasurement.WholeSheetsRequired(11m, 7.2225m));
        }

        [Fact]
        public void Sgu_processing_is_kept_as_two_separate_cost_lines()
        {
            var root = LocateProjectRoot();
            var src = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs"));

            Assert.Contains("group, \"Cutting\"", src);
            Assert.Contains("group, \"Tempering\"", src);
            Assert.DoesNotContain("group, \"Cutting + tempering\"", src);
            Assert.Contains("priceByRequiredArea: !isDgu && !isLam && !isDguLam", src);
            Assert.Contains("priced by required area", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "ViewModels", "ProformaInvoice", "Step9ProfitLoss", "ProfitLossStepViewModel.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
