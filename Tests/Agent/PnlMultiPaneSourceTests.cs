using System;
using System.IO;
using Xunit;
using System.Linq;

namespace ProGlassAutomation.Tests.Agent
{
    // ═══════════════════════════════════════════════════════════════════
    // FILE: Tests/Agent/PnlMultiPaneSourceTests.cs
    // FIX-2026-09-05 companion pins — 3+ pane builds (triple-glazed DGU).
    //
    // The wastage colour-loss fix made Middle panes first-class. These
    // SOURCE pins stop a silent retreat of the three behaviours that are
    // NOT reachable headless (they live inside WPF-bound view-models):
    //
    //   1. Step 9 CollectPanes enumerates ResolveAllPanes — the middle
    //      sheet of a triple unit is a material row, always.
    //   2. Step 9 fallback rates + sheet geometry route Middle to the
    //      INNER pane family (build-side geometry), never to the outer.
    //   3. Step 6 wastage keys Middle rows by their own side — folding
    //      them into Outer or Inner would double-charge one and erase
    //      the other.
    //   4. The rules engine only emits middle suggestions for genuinely
    //      3+ pane parses (no phantom rows on two-pane specs).
    //
    // Behaviour-level coverage lives in SpecParsingTests (the grammar).
    // These pins cover the CONSUMERS, in the established repo style.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class PnlMultiPaneSourceTests
    {
        private static string ReadFamily(string root, string relative)
        {
            var sb = new System.Text.StringBuilder(
                File.ReadAllText(Path.Combine(root, relative)));
            string dir = Path.GetDirectoryName(Path.Combine(root, relative))!;
            string name = Path.GetFileNameWithoutExtension(relative);
            foreach (var part in Directory.EnumerateFiles(dir, name + ".Part*.cs")
                         .OrderBy(f => f, StringComparer.Ordinal))
                sb.Append(File.ReadAllText(part));
            return sb.ToString();
        }

        [Fact]
        public void Step9_CollectPanes_enumerates_all_panes()
        {
            string root = LocateProjectRoot();
            string quote = ReadFamily(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs");

            Assert.Contains("PnLSpecParser.ResolveAllPanes(specName)", quote);
        }

        [Fact]
        public void Step9_middle_panes_share_the_inner_family()
        {
            string root = LocateProjectRoot();
            string quote = ReadFamily(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs");

            // Inner-family routing must mention Middle in BOTH places:
            // FallbackPaneRate AND BuildGlassRowAsync sheet selection.
            int hits = quote.Split(
                new[] { "IndexOf(\"Middle\", StringComparison.OrdinalIgnoreCase)" },
                StringSplitOptions.None).Length - 1;
            Assert.True(hits >= 2,
                $"Expected Middle-side routing in fallback rate AND sheet geometry; found {hits} site(s).");
            Assert.Contains("side.IndexOf(\"Inner\", StringComparison.OrdinalIgnoreCase) >= 0", quote);
        }

        [Fact]
        public void Wastage_side_keys_separate_middle_from_inner()
        {
            string root = LocateProjectRoot();
            string wastage = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));

            // NORMALIZATION ORDER IS THE GUARANTEE: "Middle" must be
            // classified BEFORE "Inner" and before the Outer fallback —
            // otherwise a "Middle (W2/H2)" row folds into another pane's
            // charge and the operator silently double-charges one pane
            // while never charging the other.
            int methodAt = wastage.IndexOf(
                "private static string NormalizeSideKey",
                StringComparison.Ordinal);
            Assert.True(methodAt > 0, "NormalizeSideKey must exist in WastagesSectionViewModel.");
            string body = wastage.Substring(methodAt, 2200);

            int middleAt = body.IndexOf("IndexOf(\"Middle\"", StringComparison.Ordinal);
            int innerAt = body.IndexOf("IndexOf(\"Inner\"", StringComparison.Ordinal);
            Assert.True(middleAt > 0, "NormalizeSideKey must classify Middle.");
            Assert.True(innerAt > 0, "NormalizeSideKey must classify Inner.");
            Assert.True(middleAt < innerAt,
                "Middle classification must run before Inner — a middle row must never fold into inner.");

            // And the middle SOURCE must carry the inner sheet geometry.
            Assert.Contains("\"Middle (W2/H2)\"", wastage);
            Assert.Contains("$\"Middle {m} (W2/H2)\"", wastage);
        }

        [Fact]
        public void Rules_engine_emits_middle_suggestions_only_for_three_plus_panes()
        {
            string root = LocateProjectRoot();
            string rules = ReadFamily(root,
                "Services/ProformaInvoice/OtherChargeAutomationRules.cs");

            // Gated on the PARSE, not on the type flag: a plain DGU type
            // with a two-pane spec must not gain a phantom middle row.
            Assert.Contains("glassMatches.Count > 2", rules);
            Assert.Contains("\"Middle (W2/H2)\"", rules);
        }

        [Fact]
        public void Sentinel_labels_never_become_colours_for_middle_panes()
        {
            string root = LocateProjectRoot();
            string wastage = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step6OtherCharges/Wastages/WastagesSectionViewModel.cs"));

            Assert.Contains("\"Middle glass wastage\"", wastage);
        }

        [Fact]
        public void Step1_id_fallback_is_never_silent()
        {
            // 2026-09-05 hardening pin: GenerateUniqueProformaIdAsync's
            // catch path MUST log. A silent fallback turns a repo outage
            // into an invisible duplicate-id risk on a money document.
            string root = LocateProjectRoot();
            string step1 = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step1CompanySetup/CompanySetupViewModel.cs"));

            Assert.Contains("AppLogService.Warning", step1);
            Assert.Contains("SuggestNextIdAsync failed", step1);
        }

        [Fact]
        public void Asp_is_charged_per_cavity_everywhere_it_is_priced()
        {
            // BUSINESS DECISION 2026-09-05 (owner-confirmed): EVERY
            // air-spacer cavity pays the spacer field — a triple build
            // carries TWO ASP charges. The multiplier must be wired in
            // BOTH places ASP is priced, or cost and sanity-basis drift
            // apart (a valid triple rate reads "healthy margin" against
            // an understated basis = silent overquote to the customer).
            string root = LocateProjectRoot();
            string calc = ReadFamily(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/ProfitLossStepViewModel.cs");
            string parser = File.ReadAllText(Path.Combine(root,
                "ViewModels/ProformaInvoice/Step9ProfitLoss/PnLSpecParser.cs"));

            Assert.Contains("PnLSpecParser.SpacerCount", parser);
            Assert.Contains("int spacerCavities =", calc);

            // Both cost branches (DGU and DGU+LAM) multiply by cavities.
            int costSites = calc.Split(
                new[] { "spacerPrice * spacerCavities" },
                StringSplitOptions.None).Length - 1;
            Assert.True(costSites >= 2,
                $"Expected per-cavity multiplier in BOTH ASP cost branches; found {costSites}.");

            // The rate-basis mirror carries the multiplier too — a basis
            // that stays x1 would overstate triple margins by exactly one
            // spacer price and bless an underquoted sell rate.
            int basisSites = calc.Split(
                new[] { "vm.SpacerPrice * spacerCavities" },
                StringSplitOptions.None).Length - 1;
            Assert.True(basisSites >= 2,
                $"QuoteBasisFor must mirror the cost model per cavity in its DGU and DGU+LAM lanes; found {basisSites}.");

            // Row integrity: a multi-cavity row reports its own shape
            // (quantity = sqm x cavities, description announces it).
            Assert.Contains("cavit", calc);
            Assert.Contains("cutSqm * spacerCavities, 2)", calc);
        }

        [Fact]
        public void SpacerCount_has_runtime_facts_in_the_grammar_battery()
        {
            // The pin's own cargo: the runtime fact battery for
            // SpacerCount lives in SpecParsingTests (the grammar home).
            // If it goes missing there, this consumer pin keeps nothing.
            string root = LocateProjectRoot();
            string battery = File.ReadAllText(Path.Combine(root,
                "Tests/Agent/SpecParsingTests.cs"));

            Assert.Contains("SpacerCount_counts_every_cavity_token", battery);
            Assert.Contains("SpacerCount_never_confuses_pvb_thickness_for_a_cavity", battery);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step5DataGrid", "DataGridView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
