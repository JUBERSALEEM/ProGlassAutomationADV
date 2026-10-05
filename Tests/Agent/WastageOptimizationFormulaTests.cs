using System.Collections.Generic;
using System.Linq;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice.Export;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step5DataGrid;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step6OtherCharges.Wastages;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class WastageOptimizationFormulaTests
    {
        [Fact]
        public void Optimization_formula_derives_rate_and_amount_from_selected_sqm()
        {
            var row = new WastageRow
            {
                UseOptimizationFormula = true,
                FormulaPricePerSqm = 40m,
                OptimizationPercent = 70m
            };

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 10m,
                PricePerSqm = 40m,
                IsChecked = true
            });

            Assert.Equal(57.142857m, row.Section1Value);
            Assert.Equal(47.058824m, row.Section2Value);
            Assert.Equal(10.084033m, row.BaseResultValue);
            Assert.Equal(10.084033m, row.Rate);
            Assert.Equal(100.84m, row.ResultAmount);
        }

        [Fact]
        public void Manual_rate_mode_preserves_entered_rate()
        {
            var row = new WastageRow
            {
                Rate = 12m
            };

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 3m,
                PricePerSqm = 40m,
                IsChecked = true
            });

            Assert.False(row.UseOptimizationFormula);
            Assert.Equal(12m, row.Rate);
            Assert.Equal(36m, row.ResultAmount);
        }

        [Fact]
        public void Switching_back_to_manual_mode_restores_manual_rate()
        {
            var row = new WastageRow
            {
                Rate = 22m,
                UseOptimizationFormula = true,
                FormulaPricePerSqm = 40m,
                OptimizationPercent = 70m
            };

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 2m,
                PricePerSqm = 40m,
                IsChecked = true
            });

            Assert.NotEqual(22m, row.Rate);

            row.UseOptimizationFormula = false;

            Assert.Equal(22m, row.Rate);
            Assert.Equal(44m, row.ResultAmount);
        }

        [Fact]
        public void Optimization_percent_text_accepts_decimal_input()
        {
            var row = new WastageRow();

            row.OptimizationPercentText = "15.60";

            Assert.Equal(15.60m, row.OptimizationPercent);
        }

        [Fact]
        public void Selected_source_auto_fills_sheet_price_and_size()
        {
            var row = new WastageRow();

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 5m,
                PricePerSqm = 48m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                IsChecked = true
            });

            Assert.Equal(48m, row.FormulaPricePerSqm);
            Assert.Equal("3210", row.SelectedSheetWidthText);
            Assert.Equal("2250", row.SelectedSheetHeightText);
            Assert.Equal(5m, row.ChargeableSqm);
        }

        [Fact]
        public void Utilized_sqm_raises_change_notification_when_formula_result_changes()
        {
            var row = new WastageRow();
            bool raised = false;
            row.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(row.UtilizedSqm))
                    raised = true;
            };

            row.OptimizationPercentText = "70";
            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                SqmValue = 5m,
                PricePerSqm = 48m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                IsChecked = true
            });

            Assert.True(raised);
            Assert.Equal(12.10m, decimal.Round(row.UtilizedSqm, 2));
        }

        [Fact]
        public void Matching_sources_across_outer_and_inner_are_aggregated_automatically()
        {
            var row = new WastageRow();

            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 1,
                SideLabel = "Outer (W1/H1)",
                ColorName = "Clear",
                SqmValue = 5m,
                PricePerSqm = 48m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                IsChecked = true
            });
            row.AvailableSources.Add(new SpecGroupColorSide
            {
                SpecNumber = 2,
                SideLabel = "Inner (W2/H2)",
                ColorName = "Clear",
                SqmValue = 3m,
                PricePerSqm = 48m,
                SheetWidthMm = 3210,
                SheetHeightMm = 2250,
                IsChecked = true
            });

            Assert.Equal(8m, row.ChargeableSqm);
            Assert.Equal(48m, row.FormulaPricePerSqm);
            Assert.Equal("3210", row.SelectedSheetWidthText);
            Assert.Equal("2250", row.SelectedSheetHeightText);
        }

        [Fact]
        public void Advanced_wastage_rows_start_on_by_default_for_new_detected_colors()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            group.SelectedGlassType = "DGU";
            group.SelectedSpecName = "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass";
            group.OuterGlassColor = "HD Grey";
            group.InnerGlassColor = "Clear";

            var vm = new WastagesSectionViewModel(grid);
            vm.ApplyAdvancedSuggestions(new[]
            {
                new Services.ProformaInvoice.AutoWastageSuggestion
                {
                    SpecGroupNumber = 1,
                    SourceSideLabel = "Outer (W1/H1)",
                    DisplayName = "HD Grey — outer glass wastage"
                }
            });

            Assert.Single(vm.WastageRows);
            Assert.True(vm.WastageRows[0].IsActive);
        }

        [Fact]
        public void Advanced_auto_generation_splits_outer_and_inner_into_separate_rows()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            group.SelectedGlassType = "DGU";
            group.SelectedSpecName =
                "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass";
            group.OuterGlassColor = "Clear";
            group.InnerGlassColor = "Clear";

            var vm = new WastagesSectionViewModel(grid);
            vm.ApplyAdvancedSuggestions(new[]
            {
                new Services.ProformaInvoice.AutoWastageSuggestion
                {
                    SpecGroupNumber = 1,
                    SourceSideLabel = "Outer (W1/H1)",
                    DisplayName = "Clear — outer glass wastage"
                },
                new Services.ProformaInvoice.AutoWastageSuggestion
                {
                    SpecGroupNumber = 1,
                    SourceSideLabel = "Inner (W2/H2)",
                    DisplayName = "Clear — inner glass wastage"
                }
            });

            // A DGU has an outer AND an inner pane — the same colour must
            // still produce TWO wastage charges, each with its own SHEET QTY.
            Assert.Equal(2, vm.WastageRows.Count);
            Assert.Equal(1, vm.WastageRows.Count(r =>
                r.AvailableSources.Any(s =>
                    s.IsChecked && s.SideLabel == "Outer (W1/H1)")));
            Assert.Equal(1, vm.WastageRows.Count(r =>
                r.AvailableSources.Any(s =>
                    s.IsChecked && s.SideLabel == "Inner (W2/H2)")));

            var outer = vm.WastageRows.Single(r =>
                r.AvailableSources.Any(s =>
                    s.IsChecked && s.SideLabel == "Outer (W1/H1)"));
            var inner = vm.WastageRows.Single(r =>
                r.AvailableSources.Any(s =>
                    s.IsChecked && s.SideLabel == "Inner (W2/H2)"));
            Assert.Equal("Outer", outer.SideDisplay);
            Assert.Equal("Inner", inner.SideDisplay);
        }

        [Fact]
        public void Advanced_refresh_preserves_per_side_sheet_quantity()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            group.SelectedGlassType = "DGU";
            group.SelectedSpecName =
                "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass";
            group.OuterGlassColor = "Clear";
            group.InnerGlassColor = "Clear";

            var vm = new WastagesSectionViewModel(grid);
            var suggestions = new[]
            {
                new Services.ProformaInvoice.AutoWastageSuggestion
                {
                    SpecGroupNumber = 1,
                    SourceSideLabel = "Outer (W1/H1)",
                    DisplayName = "Clear — outer glass wastage"
                },
                new Services.ProformaInvoice.AutoWastageSuggestion
                {
                    SpecGroupNumber = 1,
                    SourceSideLabel = "Inner (W2/H2)",
                    DisplayName = "Clear — inner glass wastage"
                }
            };

            vm.ApplyAdvancedSuggestions(suggestions);

            var outer = vm.WastageRows.Single(r =>
                r.AvailableSources.Any(s =>
                    s.IsChecked && s.SideLabel == "Outer (W1/H1)"));
            var inner = vm.WastageRows.Single(r =>
                r.AvailableSources.Any(s =>
                    s.IsChecked && s.SideLabel == "Inner (W2/H2)"));
            outer.SheetQuantity = 6;
            inner.SheetQuantity = 3;

            // A spec edit refreshes the rows — the operator's per-side
            // SHEET QTY must survive the rebuild.
            vm.ApplyAdvancedSuggestions(suggestions);

            var outerAfter = vm.WastageRows.Single(r =>
                r.AvailableSources.Any(s =>
                    s.IsChecked && s.SideLabel == "Outer (W1/H1)"));
            var innerAfter = vm.WastageRows.Single(r =>
                r.AvailableSources.Any(s =>
                    s.IsChecked && s.SideLabel == "Inner (W2/H2)"));

            Assert.Equal(6, outerAfter.SheetQuantity);
            Assert.Equal(3, innerAfter.SheetQuantity);
        }

        [Fact]
        public void Step4_dgu_snapshot_handoff_keeps_type_panes_sheets_and_u_insert()
        {
            var group = new SpecGroup { SelectedGlassType = "SGU" };
            var snapshot = new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "DGU",
                GeneratedSpecName =
                    "6mm HD Grey FT Glass + 12mm ASP With U-Insert + 6mm Clear FT Glass",
                EstimatedRatePerSqm = 137.50m,
                Dgu = new DguPricingPanelSnapshot
                {
                    OuterGlassColor = "HD Grey",
                    InnerGlassColor = "Clear",
                    OuterSheetWidthMm = 3210,
                    OuterSheetHeightMm = 2250,
                    InnerSheetWidthMm = 2440,
                    InnerSheetHeightMm = 1830,
                    OuterBasePrice = 52m,
                    InnerBasePrice = 31m,
                    SpacerExtra = "U-Insert",
                    UInsertPricePerSqm = 9.75m
                }
            };

            group.ApplyStep4PricingSnapshot(snapshot);

            Assert.Equal("DGU", group.SelectedGlassType);
            Assert.Equal(snapshot.GeneratedSpecName, group.SelectedSpecName);
            Assert.Equal("HD Grey", group.OuterGlassColor);
            Assert.Equal("Clear", group.InnerGlassColor);
            Assert.Equal(3210, group.OuterSheetWidthMm);
            Assert.Equal(2250, group.OuterSheetHeightMm);
            Assert.Equal(2440, group.InnerSheetWidthMm);
            Assert.Equal(1830, group.InnerSheetHeightMm);
            Assert.Equal(52m, group.OuterSheetPricePerSqm);
            Assert.Equal(31m, group.InnerSheetPricePerSqm);
            Assert.Equal(9.75m, group.UInsertPricePerSqm);
            Assert.Equal(137.50m, group.BaseRate);
            Assert.Equal(137.50m, group.ImportedPrice);
            Assert.Equal(snapshot.GeneratedSpecName, group.LastSyncedStep4SpecName);
            Assert.Equal("DGU", group.LastSyncedStep4GlassType);
        }

        [Fact]
        public void Manual_step5_type_change_keeps_a_cleared_spec_under_operator_ownership()
        {
            var group = new SpecGroup();
            group.ApplyStep4PricingSnapshot(new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "DGU",
                GeneratedSpecName =
                    "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass"
            });
            // Switching build families deliberately clears the incompatible
            // DGU spec. That blank is a user decision, not a fresh-group
            // bootstrap that Step 4 may reclaim on the next Continue.
            group.SelectedGlassType = "LAM";

            Assert.True(group.HasManualStep5GlassTypeOverride);
            Assert.True(group.HasManualStep5SpecOverride);
            Assert.Equal(string.Empty, group.LastSyncedStep4SpecName);
            Assert.Equal(string.Empty, group.LastSyncedStep4GlassType);
            Assert.Equal(string.Empty, group.SelectedSpecName);
        }

        [Fact]
        public void Source_picker_keeps_each_dgu_pane_side_and_color_visible_for_two_specs()
        {
            var grid = new DataGridViewModel();
            grid.EnsureSpecGroupCount(2);

            var firstSpec =
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass";
            var secondSpec =
                "6mm Bronze FT Glass + 12mm ASP + 6mm Green FT Glass";

            grid.SpecGroups[0].ApplyStep4PricingSnapshot(new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "DGU",
                GeneratedSpecName = firstSpec,
                Dgu = new DguPricingPanelSnapshot
                {
                    OuterGlassColor = "HD Grey",
                    InnerGlassColor = "Clear"
                }
            });
            grid.SpecGroups[1].ApplyStep4PricingSnapshot(new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "DGU",
                GeneratedSpecName = secondSpec,
                Dgu = new DguPricingPanelSnapshot
                {
                    OuterGlassColor = "Bronze",
                    InnerGlassColor = "Green"
                }
            });

            var vm = new WastagesSectionViewModel(grid);
            var row = new WastageRow();
            vm.PopulateSourcesForRestoredRow(row);

            Assert.Equal(4, row.AvailableSources.Count);
            Assert.Contains(row.AvailableSources, source =>
                source.SpecNumber == 1
                && source.SideLabel == "Outer (W1/H1)"
                && source.SourceDisplayLabel == "Spec 1 · Outer (HD Grey)"
                && source.DisplayLabel.Contains(firstSpec));
            Assert.Contains(row.AvailableSources, source =>
                source.SpecNumber == 1
                && source.SideLabel == "Inner (W2/H2)"
                && source.SourceDisplayLabel == "Spec 1 · Inner (Clear)");
            Assert.Contains(row.AvailableSources, source =>
                source.SpecNumber == 2
                && source.SideLabel == "Outer (W1/H1)"
                && source.SourceDisplayLabel == "Spec 2 · Outer (Bronze)");
            Assert.Contains(row.AvailableSources, source =>
                source.SpecNumber == 2
                && source.SideLabel == "Inner (W2/H2)"
                && source.SourceDisplayLabel == "Spec 2 · Inner (Green)");
        }

        [Fact]
        public void Source_metadata_uses_effective_dgu_type_and_rejects_blank_color_matches()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            string dguSpec =
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass";

            // Simulates the stale type/spec mismatch seen before the Step 4
            // coordinator repair. The grammar must still select DGU metadata.
            group.SelectedGlassType = "SGU";
            group.SelectedSpecName = dguSpec;
            group.OuterGlassColor = "HD Grey";
            group.InnerGlassColor = "Clear";

            var metricHub = new MetricDiscoveryHubViewModel(grid.SpecGroups);
            var effectiveMetric = Assert.Single(metricHub.PerSpecMetrics);
            Assert.Equal("DGU", effectiveMetric.GlassType);
            Assert.True(effectiveMetric.IsDoubleGlass);
            Assert.Equal(2, effectiveMetric.PaneFactor);

            var dguSnapshot = new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "DGU",
                GeneratedSpecName = dguSpec,
                Dgu = new DguPricingPanelSnapshot
                {
                    OuterGlassColor = "HD Grey",
                    InnerGlassColor = "Clear",
                    OuterSheetWidthMm = 3210,
                    OuterSheetHeightMm = 2250,
                    InnerSheetWidthMm = 2440,
                    InnerSheetHeightMm = 1830,
                    OuterBasePrice = 50m,
                    InnerBasePrice = 30m
                }
            };

            var vm = new WastagesSectionViewModel(grid);
            vm.BindStep4SectionsProvider(() => new[] { dguSnapshot });
            var row = new WastageRow();
            vm.PopulateSourcesForRestoredRow(row);

            Assert.Contains(row.AvailableSources, source =>
                source.SideLabel == "Outer (W1/H1)"
                && source.SheetWidthMm == 3210
                && source.PricePerSqm == 50m);
            Assert.Contains(row.AvailableSources, source =>
                source.SideLabel == "Inner (W2/H2)"
                && source.SheetWidthMm == 2440
                && source.PricePerSqm == 30m);

            // A blank colour must not match a blank Step 4 candidate merely
            // because string.Equals("", "") is true.
            group.SelectedSpecName = "Unparseable custom glass";
            group.OuterGlassColor = "";
            group.InnerGlassColor = "";
            var blankSnapshot = new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "SGU",
                GeneratedSpecName = "Unparseable custom glass",
                Sgu = new SguPricingPanelSnapshot
                {
                    GlassColor = "",
                    SheetWidthMm = 3210,
                    SheetHeightMm = 2250,
                    BasePricePerSqm = 55m
                }
            };
            vm.BindStep4SectionsProvider(() => new[] { blankSnapshot });
            var blankRow = new WastageRow();
            vm.PopulateSourcesForRestoredRow(blankRow);
            var blankSource = Assert.Single(blankRow.AvailableSources);
            Assert.Equal(0, blankSource.SheetWidthMm);
            Assert.Equal(0m, blankSource.PricePerSqm);
        }

        [Fact]
        public void Source_metadata_does_not_borrow_an_indexed_step4_section_for_a_manual_spec()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            string manualSpec =
                "6mm Bronze FT Glass + 12mm ASP + 6mm Green FT Glass";
            group.SelectedGlassType = "DGU";
            group.SelectedSpecName = manualSpec;
            group.OuterGlassColor = "Bronze";
            group.InnerGlassColor = "Green";
            group.OuterSheetWidthMm = 2000;
            group.OuterSheetHeightMm = 1000;
            group.OuterSheetPricePerSqm = 17m;
            group.InnerSheetWidthMm = 1800;
            group.InnerSheetHeightMm = 900;
            group.InnerSheetPricePerSqm = 19m;

            // This is the first Step 4 section by index, but it belongs to a
            // different generated spec. Its dimensions/rates must never bleed
            // into a manual Step 5 selection merely because both are DGU.
            var unrelatedStep4 = new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "DGU",
                GeneratedSpecName =
                    "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass",
                Dgu = new DguPricingPanelSnapshot
                {
                    OuterGlassColor = "HD Grey",
                    InnerGlassColor = "Clear",
                    OuterSheetWidthMm = 3210,
                    OuterSheetHeightMm = 2250,
                    InnerSheetWidthMm = 2440,
                    InnerSheetHeightMm = 1830,
                    OuterBasePrice = 50m,
                    InnerBasePrice = 30m
                }
            };

            var vm = new WastagesSectionViewModel(grid);
            vm.BindStep4SectionsProvider(() => new[] { unrelatedStep4 });
            var row = new WastageRow();
            vm.PopulateSourcesForRestoredRow(row);

            Assert.Contains(row.AvailableSources, source =>
                source.SideLabel == "Outer (W1/H1)"
                && source.ColorName == "Bronze"
                && source.SheetWidthMm == 2000
                && source.PricePerSqm == 17m);
            Assert.Contains(row.AvailableSources, source =>
                source.SideLabel == "Inner (W2/H2)"
                && source.ColorName == "Green"
                && source.SheetWidthMm == 1800
                && source.PricePerSqm == 19m);
        }

        [Fact]
        public void DguLam_metadata_uses_semantic_sides_when_all_panes_have_the_same_color()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            string hybridSpec =
                "6mm Clear FT Glass + 12mm ASP + 6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass";
            group.SelectedGlassType = "DGU+LAM";
            group.SelectedSpecName = hybridSpec;

            var snapshot = new Step4PricingSnapshot
            {
                SelectedGlassTypeCode = "DGU+LAM",
                GeneratedSpecName = hybridSpec,
                DguLam = new DguLamPricingPanelSnapshot
                {
                    OuterGlassColor = "Clear",
                    OuterBasePrice = 10m,
                    LamOuterColor = "Clear",
                    LamOuterBasePrice = 20m,
                    LamInnerColor = "Clear",
                    LamInnerBasePrice = 30m
                }
            };

            var vm = new WastagesSectionViewModel(grid);
            vm.BindStep4SectionsProvider(() => new[] { snapshot });
            var row = new WastageRow();
            vm.PopulateSourcesForRestoredRow(row);

            Assert.Equal(10m, row.AvailableSources.Single(source =>
                source.SideLabel == "Outer (W1/H1)").PricePerSqm);
            Assert.Equal(20m, row.AvailableSources.Single(source =>
                source.SideLabel == "Lam Outer (W2/H2)").PricePerSqm);
            Assert.Equal(30m, row.AvailableSources.Single(source =>
                source.SideLabel == "Lam Inner (W2/H2)").PricePerSqm);
        }

        [Fact]
        public void Exported_wastage_uses_w2_dimensions_for_both_laminated_panes()
        {
            var groups = new List<SpecGroupSnapshot>
            {
                new()
                {
                    GroupNumber = 1,
                    ItemRows = new List<GlassItemRowSnapshot>
                    {
                        new()
                        {
                            W1 = 1000,
                            H1 = 1000,
                            W2 = 2000,
                            H2 = 1000,
                            Qty = 1
                        }
                    }
                }
            };

            // W1/H1 is 1 sqm while W2/H2 is 2 sqm. Lam Outer is semantic
            // outer glass, but its canonical source key explicitly maps to
            // the laminated W2/H2 pane.
            Assert.Equal(1m, ProformaChargeMetricsCalculator.ComputeWastageQty(
                groups, new List<string> { "1-Outer (W1/H1)" }));
            Assert.Equal(2m, ProformaChargeMetricsCalculator.ComputeWastageQty(
                groups, new List<string> { "1-Lam Outer (W2/H2)" }));
            Assert.Equal(2m, ProformaChargeMetricsCalculator.ComputeWastageQty(
                groups, new List<string> { "1-Lam Inner (W2/H2)" }));
        }

        [Fact]
        public void DguLam_sources_expose_outer_and_both_laminated_sides()
        {
            var grid = new DataGridViewModel();
            var group = grid.SpecGroups[0];
            group.SelectedGlassType = "DGU+LAM";
            group.SelectedSpecName =
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass + 1.52mm Clear PVB + 6mm Bronze FT Glass";

            var row = group.ItemRows[0];
            row.W1 = 1000;
            row.H1 = 2000;
            row.W2 = 900;
            row.H2 = 1800;
            row.Qty = 2;

            var vm = new WastagesSectionViewModel(grid);
            var wastageRow = new WastageRow();

            vm.PopulateSourcesForRestoredRow(wastageRow);

            Assert.Contains(wastageRow.AvailableSources,
                s => s.SideLabel == "Outer (W1/H1)" && s.ColorName == "HD Grey");
            Assert.Contains(wastageRow.AvailableSources,
                s => s.SideLabel == "Lam Outer (W2/H2)" && s.ColorName == "Clear");
            Assert.Contains(wastageRow.AvailableSources,
                s => s.SideLabel == "Lam Inner (W2/H2)" && s.ColorName == "Bronze");
        }
    }
}
