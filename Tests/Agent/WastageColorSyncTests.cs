// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/WastageColorSyncTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Regression locks for the Step 6 wastage colour pipeline:
//   1. Row colour auto-import from checked panes (was: colour box always
//      blank until the operator typed it).
//   2. Detected-colour pick list with per-spec pane retargeting
//      ("select colour → Spec 1 W1/H1 | W2/H2, Spec 2 W1/H1 | W2/H2").
//   3. Operator-typed colours are never stomped by source changes.
//   4. Colour conflict flag for mixed-colour selections.
//   5. Structured colour transport from the rules engine (no more
//      display-string scraping that printed "Outer glass wastage").
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges.Wastages;
using ProGlassAutomation.Services.ProformaInvoice;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public sealed class WastageColorSyncTests
    {
        private static SpecGroupColorSide Pane(
            int spec, string side, string colour,
            decimal sqm = 4m, bool isChecked = false) => new()
            {
                SpecNumber = spec,
                SideLabel = side,
                ColorName = colour,
                SqmValue = sqm,
                IsChecked = isChecked
            };

        [Fact]
        public void Checked_panes_of_one_colour_import_the_row_colour_automatically()
        {
            var row = new WastageRow();

            // Two specs, same colour on the checked side → the row names itself.
            row.AvailableSources.Add(Pane(1, "Outer (W1/H1)", "HD Grey", isChecked: true));
            row.AvailableSources.Add(Pane(1, "Inner (W2/H2)", "Clear"));
            row.AvailableSources.Add(Pane(2, "Outer (W1/H1)", "HD Grey", isChecked: true));
            row.AvailableSources.Add(Pane(2, "Inner (W2/H2)", "Clear"));

            Assert.Equal("HD Grey", row.ColorName);
            Assert.False(row.HasColorConflict);
        }

        [Fact]
        public void Detected_list_offers_every_pane_colour_across_specs_in_order()
        {
            var row = new WastageRow();
            row.AvailableSources.Add(Pane(1, "Outer (W1/H1)", "HD Grey"));
            row.AvailableSources.Add(Pane(1, "Inner (W2/H2)", "Clear"));
            row.AvailableSources.Add(Pane(2, "Outer (W1/H1)", "Bronze"));
            row.AvailableSources.Add(Pane(2, "Inner (W2/H2)", "Clear"));

            Assert.True(row.HasAvailableColors);
            Assert.Equal(
                new[] { "HD Grey", "Clear", "Bronze" },
                row.AvailableColors.ToArray());
        }

        [Fact]
        public void Picking_a_detected_colour_names_the_row_and_reticks_matching_panes_across_two_specs()
        {
            var row = new WastageRow();
            var spec1Outer = Pane(1, "Outer (W1/H1)", "HD Grey", sqm: 5m);
            var spec1Inner = Pane(1, "Inner (W2/H2)", "Clear", sqm: 5m, isChecked: true);
            var spec2Outer = Pane(2, "Outer (W1/H1)", "HD Grey", sqm: 3m);
            var spec2Inner = Pane(2, "Inner (W2/H2)", "Clear", sqm: 3m, isChecked: true);
            row.AvailableSources.Add(spec1Outer);
            row.AvailableSources.Add(spec1Inner);
            row.AvailableSources.Add(spec2Outer);
            row.AvailableSources.Add(spec2Inner);

            // The fast path the operator asked for: pick the colour once —
            // the row then charges both specs' matching W1/H1 panes only.
            row.SelectedAvailableColor = "HD Grey";

            Assert.Equal("HD Grey", row.ColorName);
            Assert.True(spec1Outer.IsChecked);
            Assert.True(spec2Outer.IsChecked);
            Assert.False(spec1Inner.IsChecked);
            Assert.False(spec2Inner.IsChecked);
            Assert.Equal(8m, row.TotalSqm);
            Assert.False(row.HasColorConflict);

            // Switching colour retargets the panes symmetrically.
            row.SelectedAvailableColor = "Clear";
            Assert.Equal("Clear", row.ColorName);
            Assert.True(spec1Inner.IsChecked);
            Assert.True(spec2Inner.IsChecked);
            Assert.False(spec1Outer.IsChecked);
            Assert.False(spec2Outer.IsChecked);
            Assert.Equal(8m, row.TotalSqm);
        }

        [Fact]
        public void Mixed_colour_selection_flags_a_conflict_and_keeps_the_row_unnamed()
        {
            var row = new WastageRow();
            row.AvailableSources.Add(Pane(1, "Outer (W1/H1)", "HD Grey", isChecked: true));
            row.AvailableSources.Add(Pane(1, "Inner (W2/H2)", "Clear", isChecked: true));

            Assert.True(row.HasColorConflict);
            // Ambiguous: auto-import must NOT guess between two colours.
            Assert.Equal("", row.ColorName);
        }

        [Fact]
        public void A_colour_typed_by_the_operator_is_never_overwritten_by_source_changes()
        {
            var row = new WastageRow();
            row.AvailableSources.Add(Pane(1, "Outer (W1/H1)", "Clear", isChecked: true));
            Assert.Equal("Clear", row.ColorName); // auto-imported

            // Operator takes ownership.
            row.ColorName = "Offcut — reusable";

            // Later source churn must not stomp the operator's label.
            row.AvailableSources.Add(Pane(2, "Outer (W1/H1)", "HD Grey", isChecked: true));

            Assert.Equal("Offcut — reusable", row.ColorName);
        }

        [Fact]
        public void Clearing_a_typed_colour_hands_the_row_back_to_auto_import()
        {
            var row = new WastageRow();
            var clear = Pane(1, "Outer (W1/H1)", "Clear", isChecked: true);
            row.AvailableSources.Add(clear);
            row.ColorName = "Custom label";

            // Deliberate clear → auto-import owns the row again.
            row.ColorName = "";

            Assert.Equal("Clear", row.ColorName);
        }

        [Fact]
        public void Auto_imported_colour_follows_pane_retargets()
        {
            var row = new WastageRow();
            var grey = Pane(1, "Outer (W1/H1)", "HD Grey", isChecked: true);
            var clear = Pane(2, "Outer (W1/H1)", "Clear");
            row.AvailableSources.Add(grey);
            row.AvailableSources.Add(clear);
            Assert.Equal("HD Grey", row.ColorName);

            // Retarget the row at the other spec's pane: the auto-imported
            // colour follows (the operator never edited it).
            grey.IsChecked = false;
            clear.IsChecked = true;

            Assert.Equal("Clear", row.ColorName);
        }

        [Fact]
        public void Row_colour_is_pushed_to_panes_for_match_and_conflict_badges()
        {
            var row = new WastageRow();
            var grey = Pane(1, "Outer (W1/H1)", "HD Grey", isChecked: true);
            var clear = Pane(1, "Inner (W2/H2)", "Clear");
            var unknown = Pane(2, "Outer (W1/H1)", "");
            row.AvailableSources.Add(grey);
            row.AvailableSources.Add(clear);
            row.AvailableSources.Add(unknown);

            // Auto-import already named the row "HD Grey".
            Assert.Equal("HD Grey", row.ColorName);
            Assert.True(grey.MatchesRowColor);
            Assert.True(clear.ConflictsRowColor);
            Assert.False(unknown.MatchesRowColor);
            Assert.False(unknown.ConflictsRowColor); // colourless → no badge
            Assert.False(unknown.HasColorIdentity);

            // Switching the row colour flips the badges live.
            row.SelectedAvailableColor = "Clear";
            Assert.True(clear.MatchesRowColor);
            Assert.True(grey.ConflictsRowColor);
        }

        [Fact]
        public void Colour_picker_selection_tracks_the_row_colour()
        {
            var row = new WastageRow();
            row.AvailableSources.Add(Pane(1, "Outer (W1/H1)", "HD Grey", isChecked: true));
            row.AvailableSources.Add(Pane(1, "Inner (W2/H2)", "Clear"));

            // Auto-imported row colour shows as the picker selection.
            Assert.Equal("HD Grey", row.SelectedAvailableColor);

            // A custom colour no spec carries clears the stale selection.
            row.ColorName = "Shop mix";
            Assert.Null(row.SelectedAvailableColor);
        }

        [Fact]
        public void Wastage_rule_suggestions_carry_structured_colour_names()
        {
            var rows = OtherChargeAutomationRules.GetWastageSuggestions(
                1,
                "DGU",
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Clear FT Glass",
                "",
                "");

            Assert.Equal(2, rows.Count);
            Assert.Equal("HD Grey",
                rows.Single(r => r.SourceSideLabel == "Outer (W1/H1)").ColorName);
            Assert.Equal("Clear",
                rows.Single(r => r.SourceSideLabel == "Inner (W2/H2)").ColorName);
        }

        [Fact]
        public void Dgu_lam_suggestions_carry_all_three_pane_colours()
        {
            var rows = OtherChargeAutomationRules.GetWastageSuggestions(
                2,
                "DGU+LAM",
                "6mm HD Grey FT Glass + 12mm ASP + 6mm Bronze FT Glass + 1.52mm Clear PVB + 6mm Clear FT Glass",
                "",
                "");

            Assert.Equal(3, rows.Count);
            Assert.Equal("HD Grey",
                rows.Single(r => r.SourceSideLabel == "Outer (W1/H1)").ColorName);
            Assert.Equal("Bronze",
                rows.Single(r => r.SourceSideLabel == "Lam Outer (W2/H2)").ColorName);
            Assert.Equal("Clear",
                rows.Single(r => r.SourceSideLabel == "Lam Inner (W2/H2)").ColorName);
        }

        [Fact]
        public void Unresolvable_colours_stay_blank_instead_of_becoming_row_names()
        {
            var rows = OtherChargeAutomationRules.GetWastageSuggestions(
                1, "SGU", "some free-text spec without grammar", "", "");

            var outer = rows.Single(r => r.SourceSideLabel == "Outer (W1/H1)");
            Assert.Equal("", outer.ColorName);
            // The human label remains available for display purposes.
            Assert.Equal("Outer glass wastage", outer.DisplayName);
        }

        [Fact]
        public void Step6_wastage_colour_pipeline_is_wired_end_to_end()
        {
            var root = LocateProjectRoot();
            var row = File.ReadAllText(Path.Combine(root,
                "Models/ProformaInvoice/Step6OtherCharges/Wastages/WastageRow.cs"));
            var side = File.ReadAllText(Path.Combine(root,
                "Models/ProformaInvoice/Step6OtherCharges/Wastages/SpecGroupColorSide.cs"));
            var section = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));
            var rules = File.ReadAllText(Path.Combine(root,
                "Services/ProformaInvoice/OtherChargeAutomationRules.cs"));
            var view = File.ReadAllText(Path.Combine(root,
                "Views/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionView.xaml"));
            var snapshot = File.ReadAllText(Path.Combine(root,
                "Models/ProformaInvoice/SavedInvoice/ProformaInvoiceSnapshot.cs"));
            var mapper = File.ReadAllText(Path.Combine(root,
                "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceMapper.Part1.cs"));

            // Row model: colour pick list + auto import + conflict flag.
            Assert.Contains("AvailableColors", row);
            Assert.Contains("SyncColourStateFromSources", row);
            Assert.Contains("ApplyColourSelection", row);
            Assert.Contains("HasColorConflict", row);
            Assert.Contains("IsManuallyAdded", row);
            Assert.Contains("ResyncColourPickerSelection", row);

            // Source model: row-colour badges + per-spec headers.
            Assert.Contains("MatchesRowColor", side);
            Assert.Contains("ConflictsRowColor", side);
            Assert.Contains("IsFirstOfSpec", side);
            Assert.Contains("SpecHeaderText", side);

            // Section: manual rows survive advanced rebuilds; structured
            // colour transport; sentinel labels never become colours.
            Assert.Contains("IsManuallyAdded = true", section);
            Assert.Contains("SuggestionColour", section);
            Assert.Contains("GenericWastageDisplayNames", section);
            Assert.Contains("StampSpecGroupHeaders", section);

            // Rules engine carries colour as data.
            Assert.Contains("public string ColorName { get; init; }", rules);

            // View: detected-colour picker + popup headers/badges.
            Assert.Contains("AvailableColors", view);
            Assert.Contains("SelectedAvailableColor", view);
            Assert.Contains("SpecHeaderText", view);
            Assert.Contains("MatchesRowColor", view);

            // Persistence: manual-row ownership survives save/load.
            Assert.Contains("public bool IsManuallyAdded", snapshot);
            Assert.Contains("IsManuallyAdded = wastage.IsManuallyAdded", mapper);
            Assert.Contains("IsManuallyAdded = wSnap.IsManuallyAdded", mapper);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step6OtherCharges", "OtherChargesView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
