// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/TargetPriceModelTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// PINS THE APPROVED TARGET-PRICE KPI MODEL (Step 9 + Job Profitability)
// ─────────────────────────────────────────────────────────────────────────
//  Given selling price   = Step 5 → Specification → Base Price
//  Specification target  = ((Outer + Inner) ÷ Yield + ASP) × 1.15  (DGU)
//
//  U-INSERT ACCOUNTING (REVISED 2026-09-04 — toggle semantics; the rate
//  is NEVER added to the required target for either treatment):
//    Step 6 U-insert row ACTIVE  ("trigger ON")  → U-insert is billed as
//        its own Step 6 line on top of the given price. The FINAL REQUIRED
//        TARGET stays the NORMAL specification target — the rate is kept
//        display-only.
//    Step 6 U-insert row NOT active ("toggle OFF") → U-insert is covered
//        INSIDE the Step 5 given price. The FINAL REQUIRED TARGET STILL
//        stays the NORMAL specification target: adding the rate would
//        double-count a U-insert the customer's given price already pays
//        for (e.g. a 125 all-in given ≈ 20% over a 103.82 basis judged
//        against a folded 129.39 instead of the base-price 119.39). The
//        rate is shown on the KPI for reference only.
//
//  Approved KPI margin   = (Given − Final Required Target)
//                          ÷ Final Required Target × 100
//                          (Final Required Target = base-price specification
//                          target in every treatment)
//
// MASTER VECTOR (must always pass):
//   Spec: 6mm HD Grey FT Glass (47) + 12mm ASP (45)
//         + 6mm Clear Low E FT Glass (47)
//   Step 5 Base Price = AED 160/m²   area ≈ 36.6891 m² (any positive area)
//   Specification target            = 178.93
//   Final targets:  toggle OFF, rate 0  = 178.93
//                   toggle OFF, rate +7  = 178.93  | rate +10 = 178.93
//                   toggle ON (any rate) = 178.93 (normal spec target)
//   Margins:        −10.58% in every treatment (the rate never moves it)
//
// These are PURE model tests: no WPF dispatcher, no database, no Sheet
// Store, no filesystem. UI auto-sync event tests and repository-level
// tests for the persisted vector are added in the UI/DB suites.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.ProformaInvoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.ProformaInvoice.TargetPricing;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public class TargetPriceModelTests
    {
        // ── Approved DGU vector (shared by many tests) ──────────────

        private const int SpecNumber = 47;
        private const string SpecName =
            "6mm HD Grey FT Glass (47) + 12mm ASP (45) + 6mm Clear Low E FT Glass (47)";
        private const decimal GivenPrice = 160m;
        private const decimal Area = 36.6891m;

        private static SpecificationRateCard DguCard(decimal outer = 47m,
            decimal inner = 47m, decimal asp = 45m, decimal yield = 0.85m)
        {
            return new SpecificationRateCard
            {
                BuildType = GlassBuildType.Dgu,
                OuterGlassRatePerSqm = outer,
                InnerGlassRatePerSqm = inner,
                AspRatePerSqm = asp,
                YieldFactor = yield,
                TargetUpliftPercent = 15m
            };
        }

        private static SpecTargetPriceResult CalculateDgu(
            SpecificationRateCard? card = null,
            decimal given = GivenPrice,
            decimal area = Area)
        {
            return SpecificationTargetCalculator.Calculate(
                SpecNumber,
                SpecName,
                "DGU",
                GlassBuildType.Dgu,
                card ?? DguCard(),
                given,
                area);
        }

        private static UInsertResolution Included(decimal rate = 0m) => new()
        {
            Treatment = UInsertTreatment.IncludedInStep5Price,
            RatePerSqm = rate
        };

        private static UInsertResolution Additional(decimal rate) => new()
        {
            Treatment = UInsertTreatment.Additional,
            RatePerSqm = rate
        };

        // ── 1. Approved formula vector: specification target 178.93 ─

        [Fact]
        public void Dgu_specification_target_matches_approved_vector_178_93()
        {
            var spec = CalculateDgu();

            Assert.Equal(178.93m, Math.Round(spec.SpecificationTargetPerSqm, 2));
            Assert.Equal(178.93m, Math.Round(spec.FinalRequiredTargetPerSqm, 2));
            Assert.Contains("0.85", spec.FormulaText);
            Assert.Contains("1.15", spec.FormulaText);

            // Given 160 vs target 178.93 → −10.58% is a genuine BELOW TARGET
            // (never an "up to date" — a spec is only UP TO DATE when its
            // given price sits inside the ±0.005% AT-TARGET tolerance).
            Assert.Equal(TargetStatus.BelowTarget, spec.Status);
        }

        // ── 2/3/4. Final target + approved margins across treatments ─
        //
        //  Revised rule (2026-09-04): the U-insert rate is NEVER added to
        //  the required target. Toggle OFF (IncludedInStep5Price) = the
        //  given price already contains the U-insert, so the target stays
        //  the base-price specification target (adding the rate would
        //  double-count it); Toggle ON (Additional) = the Step 6 line bills
        //  it separately, so the target also stays the specification
        //  target. The rate is display-only in both cases.

        private static void AssertFinalAndMargin(UInsertResolution resolution,
            decimal expectedFinal, decimal expectedMargin)
        {
            var spec = CalculateDgu();
            SpecificationTargetCalculator.ApplyUInsert(spec, resolution);

            Assert.Equal(expectedFinal, Math.Round(spec.FinalRequiredTargetPerSqm, 2));
            Assert.True(spec.MarginAgainstTargetPercent.HasValue);
            Assert.Equal(expectedMargin,
                Math.Round(spec.MarginAgainstTargetPercent!.Value, 2));

            // KPI denominator is the FINAL required target (= the base-price
            // specification target) — never the pre-uplift base cost.
            decimal expectedDiff = Math.Round(GivenPrice - expectedFinal, 2);
            Assert.Equal(expectedDiff, Math.Round(spec.DifferencePerSqm, 2));
        }

        [Fact]
        public void Included_zero_rate_vector_final_178_93_and_margin_minus_10_58()
            => AssertFinalAndMargin(Included(0m), 178.93m, -10.58m);

        [Fact]
        public void Included_7_vector_final_178_93_and_margin_minus_10_58()
            => AssertFinalAndMargin(Included(7m), 178.93m, -10.58m);

        [Fact]
        public void Included_10_vector_final_178_93_and_margin_minus_10_58()
            => AssertFinalAndMargin(Included(10m), 178.93m, -10.58m);

        [Fact]
        public void Additional_7_vector_keeps_normal_target_178_93_and_margin_minus_10_58()
            => AssertFinalAndMargin(Additional(7m), 178.93m, -10.58m);

        [Fact]
        public void Additional_10_vector_keeps_normal_target_178_93_and_margin_minus_10_58()
            => AssertFinalAndMargin(Additional(10m), 178.93m, -10.58m);

        // ── 5. Toggle OFF: the rate is shown, never added ─────────────

        [Fact]
        public void U_insert_included_rate_is_shown_but_never_added_to_the_target()
        {
            var spec = CalculateDgu();
            SpecificationTargetCalculator.ApplyUInsert(spec, Included(7m));

            // The rate stays visible for the KPI display…
            Assert.Equal(7m, spec.UInsertRatePerSqm);
            Assert.Equal(UInsertTreatment.IncludedInStep5Price, spec.UInsertTreatment);
            // …but it is never folded into the required target: the given
            // price already includes the U-insert, so the target stays the
            // base-price specification target.
            Assert.Equal(0m, spec.AppliedUInsertPerSqm);
            Assert.Equal(Math.Round(spec.SpecificationTargetPerSqm, 2),
                Math.Round(spec.FinalRequiredTargetPerSqm, 2));
        }

        [Fact]
        public void Included_rate_never_moves_the_target_or_reclassifies_status()
        {
            // Given 200 vs spec target 178.93 → +11.78% (ABOVE TARGET).
            // Toggle OFF with a AED 25/m² U-insert covered in the price:
            // the target must NOT move to 203.93, and the spec must stay
            // ABOVE TARGET — the U-insert is inside the given price, not an
            // additional cost the price must clear.
            var spec = CalculateDgu(given: 200m, area: 10m);
            Assert.Equal(TargetStatus.AboveTarget, spec.Status);

            SpecificationTargetCalculator.ApplyUInsert(spec, Included(25m));

            Assert.Equal(Math.Round(178.93m, 2),
                Math.Round(spec.FinalRequiredTargetPerSqm, 2));
            Assert.Equal(11.78m,
                Math.Round(spec.MarginAgainstTargetPercent!.Value, 2));
            Assert.Equal(TargetStatus.AboveTarget, spec.Status);
        }

        // ── 6. Toggle ON: the extra is billed separately — the required
        //        target stays the NORMAL specification target ─────────

        [Fact]
        public void U_insert_additional_keeps_normal_target_rate_display_only()
        {
            var spec = CalculateDgu();
            SpecificationTargetCalculator.ApplyUInsert(spec, Additional(7m));

            // Rate is preserved for the Step 6 display…
            Assert.Equal(7m, spec.UInsertRatePerSqm);
            // …but is NOT folded into the required target (the Step 6 line
            // recovers the cost, so the KPI checks the given price against
            // the normal specification target).
            Assert.Equal(0m, spec.AppliedUInsertPerSqm);
            Assert.Equal(Math.Round(spec.SpecificationTargetPerSqm, 2),
                Math.Round(spec.FinalRequiredTargetPerSqm, 2));
            Assert.Equal(UInsertTreatment.Additional, spec.UInsertTreatment);
        }

        // ── 7. Step 6 resolver: rate is dynamic, never hardcoded ────

        [Fact]
        public void Resolver_prefers_step4_seed_then_row_then_template_default()
        {
            decimal Resolve(decimal seed, decimal rowRate, decimal templateDefault,
                bool rowExists = true, bool rowActive = true, bool requires = true)
            {
                var q = new Step6UInsertQuery
                {
                    SpecRequiresUInsert = requires,
                    RowExists = rowExists,
                    RowActiveForSpec = rowActive,
                    Step4SeedRate = seed,
                    RowRate = rowRate,
                    TemplateDefaultRate = templateDefault
                };
                return Step6ChargeResolver.ResolveUInsert(q).RatePerSqm;
            }

            // Step 4 per-spec seed wins…
            Assert.Equal(8m, Resolve(8m, 7m, 10m));
            // …then the Step 6 row rate…
            Assert.Equal(7m, Resolve(0m, 7m, 10m));
            // …then the Step 6 template default.
            Assert.Equal(10m, Resolve(0m, 0m, 10m));
        }

        [Fact]
        public void Resolver_treats_inactive_u_insert_row_as_included_in_step5_price()
        {
            // Toggle OFF: the U-insert is covered inside the Step 5 price.
            // The resolver keeps the rate so the KPI can SHOW it — the
            // calculator never adds it to the required target.
            var r = Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
            {
                SpecRequiresUInsert = true,
                RowExists = true,
                RowActiveForSpec = false,
                RowRate = 7m
            });

            Assert.Equal(UInsertTreatment.IncludedInStep5Price, r.Treatment);
            Assert.Equal(7m, r.RatePerSqm);
        }

        [Fact]
        public void Resolver_treats_active_u_insert_row_as_separately_billed_additional()
        {
            // Toggle ON: the U-insert is billed as its own Step 6 line on
            // top of the given price — the treatment is Additional and the
            // rate is resolved (display), but the calculator must NOT fold
            // it into the required target.
            var r = Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
            {
                SpecRequiresUInsert = true,
                RowExists = true,
                RowActiveForSpec = true,
                RowRate = 7m
            });

            Assert.Equal(UInsertTreatment.Additional, r.Treatment);
            Assert.Equal(7m, r.RatePerSqm);
        }

        [Fact]
        public void Resolver_flags_unresolved_when_u_insert_required_but_no_rate()
        {
            var r = Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
            {
                SpecRequiresUInsert = true,
                RowExists = false,
                RowActiveForSpec = false,
                Step4SeedRate = 0m,
                RowRate = 0m,
                TemplateDefaultRate = 0m
            });

            Assert.Equal(UInsertTreatment.Unresolved, r.Treatment);
            Assert.True(r.HasWarning);
        }

        [Fact]
        public void Resolver_returns_not_applicable_when_spec_has_no_u_insert()
        {
            var r = Step6ChargeResolver.ResolveUInsert(new Step6UInsertQuery
            {
                SpecRequiresUInsert = false,
                RowExists = true,
                RowActiveForSpec = true,
                RowRate = 7m
            });

            Assert.Equal(UInsertTreatment.NotApplicable, r.Treatment);
        }

        // ── 8. Never a fake 0% margin when inputs are missing ───────

        [Fact]
        public void Missing_base_price_reports_missing_and_null_margin()
        {
            var spec = CalculateDgu(given: 0m);
            SpecificationTargetCalculator.ApplyUInsert(spec, Included());

            Assert.Equal(TargetStatus.MissingBasePrice, spec.Status);
            Assert.Null(spec.MarginAgainstTargetPercent);
        }

        [Fact]
        public void Missing_glass_rate_reports_missing_and_null_margin()
        {
            var card = DguCard(outer: 0m, inner: 0m);
            var spec = CalculateDgu(card);

            Assert.Equal(TargetStatus.MissingGlassRate, spec.Status);
            Assert.Null(spec.MarginAgainstTargetPercent);
            Assert.Equal(0m, spec.SpecificationTargetPerSqm);
        }

        [Fact]
        public void Missing_asp_rate_reports_missing_and_null_margin()
        {
            var card = DguCard(asp: 0m);
            var spec = CalculateDgu(card);

            Assert.Equal(TargetStatus.MissingAspRate, spec.Status);
            Assert.Null(spec.MarginAgainstTargetPercent);
        }

        [Fact]
        public void Missing_yield_reports_missing_and_null_margin()
        {
            var card = DguCard(yield: 0m);
            var spec = CalculateDgu(card);

            Assert.Equal(TargetStatus.MissingYield, spec.Status);
            Assert.Null(spec.MarginAgainstTargetPercent);
        }

        // ── 9. Multiple spec groups stay isolated (never mix Base Rates) ──

        [Fact]
        public void Multi_group_aggregation_keeps_each_spec_on_its_own_base_rate()
        {
            var specA = CalculateDgu(given: 160m, area: 10m);       // spec target 178.93
            // Toggle OFF with a rate: U-insert is covered inside the given
            // price, so group A's final required target stays 178.93 (the
            // rate is shown, never added).
            SpecificationTargetCalculator.ApplyUInsert(specA, Included(7m)); // final 178.93

            // Second independent group: different rates AND different given price.
            var cardB = new SpecificationRateCard
            {
                BuildType = GlassBuildType.Dgu,
                OuterGlassRatePerSqm = 55m,
                InnerGlassRatePerSqm = 45m,
                AspRatePerSqm = 45m,
                YieldFactor = 0.85m,
                TargetUpliftPercent = 15m
            };
            var specB = SpecificationTargetCalculator.Calculate(
                2, "2nd group", "DGU", GlassBuildType.Dgu, cardB, 200m, 20m);
            SpecificationTargetCalculator.ApplyUInsert(specB, Included());

            // specB's own formula: ((55 + 45) ÷ 0.85 + 45) × 1.15
            decimal pre = (55m + 45m) / 0.85m + 45m;
            decimal expectedB = Math.Round(pre * 1.15m, 2);
            Assert.Equal(expectedB, Math.Round(specB.SpecificationTargetPerSqm, 2));
            Assert.Equal(178.93m, Math.Round(specA.SpecificationTargetPerSqm, 2));

            // Group A's margin is still against ITS final target (178.93),
            // never diluted by group B's 200/m² given price.
            Assert.Equal(-10.58m,
                Math.Round(specA.MarginAgainstTargetPercent!.Value, 2));

            var job = TargetProfitabilityAggregator.AggregateJob(new[]
            {
                specA, specB
            });

            Assert.Equal(2, job.Specs.Count);
            Assert.Equal(10m + 20m, job.TotalAreaSqm);
            Assert.Equal(Math.Round(160m * 10m + 200m * 20m, 2), job.TotalGivenValue);
            decimal requiredA = Math.Round(178.93m * 10m, 2);
            decimal requiredB = Math.Round(expectedB * 20m, 2);
            Assert.Equal(Math.Round(requiredA + requiredB, 2), job.TotalRequiredTargetValue);

            // Value-weighted portfolio margin must equal the hand formula.
            decimal expectedPortfolio = Math.Round(
                (job.TotalGivenValue - job.TotalRequiredTargetValue)
                / job.TotalRequiredTargetValue * 100m, 2);
            Assert.True(job.MarginAgainstTargetPercent.HasValue);
            Assert.Equal(expectedPortfolio,
                Math.Round(job.MarginAgainstTargetPercent!.Value, 2));
        }

        // ── 10. Aggregation statuses / incomplete counting ─────────

        [Fact]
        public void Aggregate_marks_incomplete_when_any_spec_has_no_valid_margin()
        {
            var good = CalculateDgu(given: 200m, area: 5m);
            SpecificationTargetCalculator.ApplyUInsert(good, Included());

            var bad = CalculateDgu(given: 0m, area: 5m); // missing base price
            SpecificationTargetCalculator.ApplyUInsert(bad, Included());

            var job = TargetProfitabilityAggregator.AggregateJob(new[]
            {
                good, bad
            });

            Assert.Equal(1, job.ValidSpecCount);
            Assert.Equal(1, job.IncompleteSpecCount);
            // One invalid spec → no job margin (never diluted to a fake %).
            Assert.Null(job.MarginAgainstTargetPercent);
            Assert.Equal(TargetStatus.MissingBasePrice, job.Status);
        }

        [Fact]
        public void Average_job_margin_excludes_incomplete_jobs()
        {
            var margins = new decimal?[] { -13.95m, null, 5.00m, -2.50m };

            decimal? avg = TargetProfitabilityAggregator.AverageJobMargin(margins);

            Assert.NotNull(avg);
            decimal expected = Math.Round((-13.95m + 5.00m - 2.50m) / 3m, 2);
            Assert.Equal(expected, avg!.Value);
        }

        // ── 11. Job-level display helpers never paint 0% for missing data ──

        [Fact]
        public void Job_row_without_target_data_reports_incomplete_or_stale_not_zero()
        {
            var uncosted = new ProGlassAutomation.ViewModels.JobProfitability.JobProfitabilityRow
            {
                HasTargetData = false,
                IsCosted = false
            };
            Assert.Equal("INCOMPLETE", uncosted.TargetStatusLabel);
            Assert.Equal("—", uncosted.TargetMarginDisplay);
            Assert.False(uncosted.HasValidTargetMargin);

            var stale = new ProGlassAutomation.ViewModels.JobProfitability.JobProfitabilityRow
            {
                HasTargetData = false,
                IsCosted = true
            };
            Assert.Equal("STALE", stale.TargetStatusLabel);
        }

        // ── 12. Snapshot additive block round-trips ────────────────

        [Fact]
        public void Snapshot_target_block_round_trips_through_json()
        {
            var spec = CalculateDgu();
            // Toggle OFF with a rate → the target does NOT move (the given
            // price already covers the U-insert); the persisted block must
            // round-trip the base-price specification target.
            SpecificationTargetCalculator.ApplyUInsert(spec, Included(7m));

            var snap = new ProformaInvoiceSnapshot();
            snap.ProfitLoss = new ProfitLossSnapshot
            {
                TotalGlassCost = 1234.56m,
                TargetTotalGivenValue = spec.GivenValue,
                TargetTotalRequiredValue = spec.RequiredTargetValue,
                TargetAverageGivenPricePerSqm = spec.GivenPricePerSqm,
                TargetAverageFinalRequiredTargetPerSqm = spec.FinalRequiredTargetPerSqm,
                TargetMarginAgainstTargetPercent = spec.MarginAgainstTargetPercent,
                TargetStatus = spec.Status.ToString(),
                TargetUInsertTreatmentSummary = "Included in given price",
                TargetValidSpecCount = 1,
                TargetIncompleteSpecCount = 0,
                TargetCalculationVersion = "2.0"
            };
            snap.ProfitLoss.SpecTargets.Add(new SpecTargetSnapshot
            {
                SpecNumber = spec.SpecNumber,
                SpecName = spec.SpecName,
                GlassTypeCode = "DGU",
                BuildType = spec.BuildType.ToString(),
                GivenPricePerSqm = spec.GivenPricePerSqm,
                AreaSqm = spec.AreaSqm,
                GivenValue = spec.GivenValue,
                OuterGlassRatePerSqm = spec.OuterGlassRatePerSqm,
                InnerGlassRatePerSqm = spec.InnerGlassRatePerSqm,
                AspRatePerSqm = spec.AspRatePerSqm,
                YieldFactor = spec.YieldFactor,
                TargetUpliftPercent = spec.TargetUpliftPercent,
                FormulaText = spec.FormulaText,
                SpecificationTargetPerSqm = spec.SpecificationTargetPerSqm,
                UInsertTreatment = spec.UInsertTreatment.ToString(),
                UInsertRatePerSqm = spec.UInsertRatePerSqm,
                AppliedUInsertPerSqm = spec.AppliedUInsertPerSqm,
                FinalRequiredTargetPerSqm = spec.FinalRequiredTargetPerSqm,
                DifferencePerSqm = spec.DifferencePerSqm,
                RequiredTargetValue = spec.RequiredTargetValue,
                DifferenceValue = spec.DifferenceValue,
                MarginAgainstTargetPercent = spec.MarginAgainstTargetPercent,
                Status = spec.Status.ToString()
            });

            string json = ProformaInvoiceMapper.SerializeSnapshot(snap);
            var back = ProformaInvoiceMapper.DeserializeSnapshot(
                new SavedProformaInvoice { FullSnapshotJson = json });

            Assert.NotNull(back);
            Assert.NotNull(back!.ProfitLoss);
            Assert.Single(back.ProfitLoss.SpecTargets);
            Assert.Equal(spec.GivenPricePerSqm,
                back.ProfitLoss.SpecTargets[0].GivenPricePerSqm);
            Assert.Equal(178.93m,
                Math.Round(back.ProfitLoss.SpecTargets[0].FinalRequiredTargetPerSqm, 2));
            Assert.Equal(spec.MarginAgainstTargetPercent,
                back.ProfitLoss.SpecTargets[0].MarginAgainstTargetPercent);
            Assert.Equal("Included in given price",
                back.ProfitLoss.TargetUInsertTreatmentSummary);
            Assert.Equal(spec.RequiredTargetValue, back.ProfitLoss.TargetTotalRequiredValue);
        }

        // ── 13. Snapshots saved BEFORE the target model stay safe ─────

        [Fact]
        public void Pre_target_snapshot_deserialises_with_empty_target_block()
        {
            // An invoice saved before the additive target block existed has
            // P&L figures but no SpecTargets and no target totals. The new
            // deserializer must leave the additive block empty (never throw
            // and never invent a margin).
            var snap = new ProformaInvoiceSnapshot
            {
                ProfitLoss = new ProfitLossSnapshot
                {
                    TotalGlassCost = 100m,
                    MarginPercent = 12.5m
                }
            };
            string json = ProformaInvoiceMapper.SerializeSnapshot(snap);

            var back = ProformaInvoiceMapper.DeserializeSnapshot(
                new SavedProformaInvoice { FullSnapshotJson = json });

            Assert.NotNull(back);
            Assert.NotNull(back!.ProfitLoss);
            // Consumers must treat missing target data as INCOMPLETE/STALE —
            // never as a 0% target margin.
            Assert.Null(back.ProfitLoss.TargetMarginAgainstTargetPercent);
            Assert.Empty(back.ProfitLoss.SpecTargets);
            Assert.Equal(0, back.ProfitLoss.TargetTotalGivenValue);
            Assert.Equal(0, back.ProfitLoss.TargetTotalRequiredValue);
            Assert.Equal(0, back.ProfitLoss.TargetValidSpecCount);
            Assert.Equal(0, back.ProfitLoss.TargetIncompleteSpecCount);
            // Legacy actual-P&L fields still round-trip untouched.
            Assert.Equal(12.5m, back.ProfitLoss.MarginPercent);
        }

        // ── 14. Given-price source rules are centralised ────────────

        [Fact]
        public void Step5_price_source_is_the_only_given_price_source()
        {
            Assert.Equal("Step 5 Specification Base Price", Step5PriceSource.SourceLabel);
            Assert.Equal(160m, Step5PriceSource.GivenPricePerSqm(160m));
            Assert.False(Step5PriceSource.HasGivenPrice(0m));
            Assert.True(Step5PriceSource.HasGivenPrice(160m));
        }
    }
}
