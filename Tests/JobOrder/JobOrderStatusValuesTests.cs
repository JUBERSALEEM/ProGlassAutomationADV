// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/JobOrder/JobOrderStatusValuesTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Regression net for the "InProduction" ↔ "In Production" split.
//
// THE BUG THIS PINS
//   SavedJobOrder.Status is a string; JobOrderStatus is an enum whose member
//   is `InProduction`. Jobs written through the enum store "InProduction",
//   older rows store "In Production", and both are live in the same table.
//
//   DashboardMetricsService.GetProductionMetricsAsync compared against only
//   "In Production", so it missed every enum-written job. The Overview
//   dashboard therefore reported fewer jobs in production than the Job Order
//   dashboard did — two screens, two answers to the same question, with no
//   error anywhere.
//
//   ReportingDataService had already documented the split and matched both.
//   The fix was not to patch a third call site but to give job status one
//   canonical home, JobOrderStatusValues, and have every consumer use it.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.JobOrder.Common.JobOrderStatus;
using Xunit;

namespace ProGlassAutomation.Tests.JobOrder
{
    public sealed class JobOrderStatusValuesTests
    {
        // ── the split ────────────────────────────────────────────────

        [Theory]
        [InlineData("InProduction")]     // what the enum serialises to
        [InlineData("In Production")]    // legacy rows
        [InlineData("inproduction")]     // case must not matter
        [InlineData("IN PRODUCTION")]
        [InlineData("  InProduction  ")] // stored values are not always trimmed
        public void IsInProduction_accepts_both_stored_spellings(string stored)
            => Assert.True(JobOrderStatusValues.IsInProduction(stored));

        [Theory]
        [InlineData("Draft")]
        [InlineData("Released")]
        [InlineData("Completed")]
        [InlineData("Delivered")]
        [InlineData("")]
        [InlineData("   ")]
        public void IsInProduction_rejects_every_other_status(string stored)
            => Assert.False(JobOrderStatusValues.IsInProduction(stored));

        [Fact]
        public void IsInProduction_rejects_null()
            => Assert.False(JobOrderStatusValues.IsInProduction(null));

        // ── canonical labelling ──────────────────────────────────────

        [Theory]
        [InlineData("InProduction")]
        [InlineData("In Production")]
        [InlineData("inproduction")]
        public void Canonical_collapses_the_split_to_one_label(string stored)
            => Assert.Equal("In Production", JobOrderStatusValues.Canonical(stored));

        [Theory]
        [InlineData("Draft")]
        [InlineData("Released")]
        [InlineData("Completed")]
        [InlineData("Delivered")]
        public void Canonical_preserves_statuses_that_have_no_split(string stored)
            => Assert.Equal(stored, JobOrderStatusValues.Canonical(stored));

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void Canonical_returns_Unknown_for_missing_status(string? stored)
            => Assert.Equal("Unknown", JobOrderStatusValues.Canonical(stored));

        /// <summary>
        /// The reason Canonical exists: a breakdown grouped on the raw string
        /// shows the same state twice, and a chart splits one number into two
        /// bars. Both spellings must land in one bucket.
        /// </summary>
        [Fact]
        public void Canonical_groups_both_spellings_into_one_bucket()
        {
            var rows = new[] { "InProduction", "In Production", "inproduction" };

            var buckets = new System.Collections.Generic.HashSet<string>();
            foreach (var r in rows) buckets.Add(JobOrderStatusValues.Canonical(r));

            Assert.Single(buckets);
        }

        // ── completion semantics ─────────────────────────────────────

        [Theory]
        [InlineData("Completed", true)]
        [InlineData("Delivered", true)]
        [InlineData("completed", true)]
        [InlineData("InProduction", false)]
        [InlineData("In Production", false)]
        [InlineData("Draft", false)]
        [InlineData("Released", false)]
        public void IsFinished_covers_completed_and_delivered_only(string stored, bool expected)
            => Assert.Equal(expected, JobOrderStatusValues.IsFinished(stored));

        /// <summary>
        /// A delivered job is never overdue even if its delivery date has
        /// passed. That rule is applied by callers via IsFinished, so it must
        /// not quietly start returning false for "Delivered".
        /// </summary>
        [Fact]
        public void A_delivered_job_is_finished_and_therefore_never_overdue()
            => Assert.True(JobOrderStatusValues.IsFinished(JobOrderStatusValues.Delivered));

        // ── drift guards ─────────────────────────────────────────────

        /// <summary>
        /// The constant must equal the enum member's name. If someone renames
        /// the enum member without renaming the constant, new records store a
        /// value no comparison recognises — the exact failure class this file
        /// exists to catch.
        /// </summary>
        [Theory]
        [InlineData(JobOrderStatus.Draft, JobOrderStatusValues.Draft)]
        [InlineData(JobOrderStatus.Released, JobOrderStatusValues.Released)]
        [InlineData(JobOrderStatus.InProduction, JobOrderStatusValues.InProduction)]
        [InlineData(JobOrderStatus.Completed, JobOrderStatusValues.Completed)]
        [InlineData(JobOrderStatus.Delivered, JobOrderStatusValues.Delivered)]
        public void Constants_still_match_the_enum_member_names(JobOrderStatus status, string constant)
            => Assert.Equal(status.ToString(), constant);

        [Fact]
        public void The_display_label_is_the_legacy_spelling()
            => Assert.Equal(JobOrderStatusValues.InProductionLegacy,
                            JobOrderStatusValues.InProductionDisplay);
    }
}
