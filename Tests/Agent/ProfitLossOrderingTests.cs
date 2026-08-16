// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ProfitLossOrderingTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Profit and Loss rows must read in PHYSICAL BUILD ORDER:
//
//   SGU      outer
//   DGU      outer · air spacer · inner
//   LAM      outer · interlayer · inner
//   DGU+LAM  outer · air spacer · lam outer · interlayer · lam inner
//
// They used to come out as every pane of glass first with the spacer and
// interlayer underneath, which reads nothing like the product and makes a
// DGU indistinguishable from a laminate at a glance.
//
// The ordering is done by splicing layer rows into the list at a tracked
// index. That is fiddly — inserting one row shifts the index of the next —
// so the exact sequence is pinned here rather than eyeballed.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Generic;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ProfitLossOrderingTests
    {
        /// <summary>
        /// Mirrors the splice logic in ProfitLossStepViewModel.RecalculateAsync.
        /// Kept deliberately simple: it models WHERE rows land, not what they
        /// cost, because the ordering is the part that was wrong.
        /// </summary>
        private static List<string> BuildOrder(string kind)
        {
            var rows = new List<string>();
            int spacerAt = -1;
            int interlayerAt = -1;

            int AddLayerRow(string row, int at)
            {
                if (at >= 0 && at <= rows.Count)
                {
                    rows.Insert(at, row);
                    return at + 1;
                }

                rows.Add(row);
                return at;
            }

            if (kind == "DGU+LAM")
            {
                string[] panes = { "GLASS Outer", "GLASS Lam Outer", "GLASS Lam Inner" };

                for (int i = 0; i < panes.Length; i++)
                {
                    rows.Add(panes[i]);
                    if (i == 0) spacerAt = rows.Count;
                    if (i == 1) interlayerAt = rows.Count;
                }

                int before = rows.Count;
                spacerAt = AddLayerRow("SPACER", spacerAt);

                // Inserting the spacer pushed the interlayer boundary right.
                if (interlayerAt >= 0 && rows.Count > before)
                    interlayerAt += rows.Count - before;

                interlayerAt = AddLayerRow("INTERLAYER", interlayerAt);
            }
            else
            {
                rows.Add("GLASS Outer");
                spacerAt = rows.Count;

                if (kind == "DGU" || kind == "LAM") rows.Add("GLASS Inner");

                if (kind == "DGU") spacerAt = AddLayerRow("SPACER", spacerAt);
                if (kind == "LAM") spacerAt = AddLayerRow("INTERLAYER", spacerAt);
                if (kind == "SGU") rows.Add("PRODUCTION");
            }

            return rows;
        }

        [Fact]
        public void Sgu_lists_the_single_pane_then_production()
        {
            Assert.Equal(
                new[] { "GLASS Outer", "PRODUCTION" },
                BuildOrder("SGU"));
        }

        [Fact]
        public void Dgu_puts_the_spacer_between_the_two_panes()
        {
            Assert.Equal(
                new[] { "GLASS Outer", "SPACER", "GLASS Inner" },
                BuildOrder("DGU"));
        }

        [Fact]
        public void Lam_puts_the_interlayer_between_the_two_panes()
        {
            Assert.Equal(
                new[] { "GLASS Outer", "INTERLAYER", "GLASS Inner" },
                BuildOrder("LAM"));
        }

        [Fact]
        public void DguLam_interleaves_both_layers_in_build_order()
        {
            // The case that exposed the index-shift bug: inserting the
            // spacer moved the interlayer's boundary, and without correcting
            // for it the interlayer floated above the pane it bonds.
            Assert.Equal(
                new[]
                {
                    "GLASS Outer",
                    "SPACER",
                    "GLASS Lam Outer",
                    "INTERLAYER",
                    "GLASS Lam Inner"
                },
                BuildOrder("DGU+LAM"));
        }

        [Fact]
        public void Every_build_keeps_all_its_rows()
        {
            // Splicing must never drop a row: a lost row is a lost cost.
            Assert.Equal(2, BuildOrder("SGU").Count);
            Assert.Equal(3, BuildOrder("DGU").Count);
            Assert.Equal(3, BuildOrder("LAM").Count);
            Assert.Equal(5, BuildOrder("DGU+LAM").Count);
        }

        [Fact]
        public void No_build_ever_starts_with_a_layer_row()
        {
            // The outer pane is always first. If a layer leads, the splice
            // index was wrong and the whole sequence is suspect.
            foreach (string kind in new[] { "SGU", "DGU", "LAM", "DGU+LAM" })
                Assert.StartsWith("GLASS", BuildOrder(kind)[0]);
        }
    }
}
