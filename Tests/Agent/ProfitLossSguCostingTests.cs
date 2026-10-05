// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProfitLossSguCostingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Locks the SGU Step 9 business rule requested by the user:
//   • The Step 6 wastage setup SHEET QTY is authoritative for every pane
//     type — SGU included. Once the operator has set a sheet quantity for
//     the pane, that number wins (quantity = sheets, amount =
//     sheetSqm × sheets × rate), exactly as it does for DGU/LAM/DGU+LAM.
//   • Required-SQM pricing (cut × (1 + wastage) × rate) remains the
//     FALLBACK for a pane no wastage row claims.
//   • Cutting and tempering remain separate SQM-based charges.
//
// The arithmetic is tested directly via public helpers and a model of the
// branch in BuildGlassRowAsync; the integration points are pinned with a
// source check so the rule cannot silently drift back inside
// ProfitLossStepViewModel.
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
        public void Area_with_wastage_is_the_fallback_when_no_setup_qty_exists()
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
        public void Setup_sheet_qty_wins_over_required_area_for_sgu()
        {
            // Models BuildGlassRowAsync: fromStep6Setup=true gives
            //   quantity = setup sheet qty
            //   amount   = sheetSqm × sheets × rate
            // Required-area pricing of the same pane gives a different,
            // smaller quantity — the branch must prefer the operator's
            // sheet quantity.
            decimal cutSqm = 10m;
            decimal waste = 10m;
            decimal rate = 46m;
            decimal sheetSqm = 7.2225m;
            int setupSheets = 2;

            decimal needed = PnLMeasurement.AreaWithWastage(cutSqm, waste);
            int sheets = setupSheets;
            bool fromStep6Setup = true;

            decimal quantity = fromStep6Setup ? sheets : Math.Round(needed, 2);
            decimal amount = fromStep6Setup
                ? Math.Round(sheetSqm * sheets * rate, 2)
                : Math.Round(needed * rate, 2);

            Assert.Equal(2m, quantity);               // sheet qty, not 11 sqm
            Assert.Equal(664.47m, amount);            // 7.2225 × 2 × 46
            Assert.NotEqual(506.00m, amount);         // required-area fallback
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
            // SHEET QTY wins for SGU too; required area remains the fallback
            // for panes no wastage row claims.
            Assert.Contains("ResolveStep6WastageSheetQuantity", src);
            Assert.Contains("SHEET QTY from Step 6 wastage setup", src);
            Assert.Contains("fromStep6Setup", src);
            Assert.Contains("A DISABLED wastage row still owns", src);
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
