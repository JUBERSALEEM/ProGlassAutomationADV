// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Architecture/MasterDnFilterParityTests.cs
// PROJECT: ProGlassAutomation
// NAMESPACE: ProGlassAutomation.Tests.Architecture
//
// PURPOSE:
//   One rule, stated once, enforced everywhere: a query that counts or lists
//   delivery notes must exclude Master DNs.
//
// WHY THIS EXISTS:
//   Found 2026-09-08 while migrating the Delivery Note dashboard onto
//   IDashboardMetricsService (Phase 3). DeliveryNoteRepository.GetAllAsync and
//   every per-status query beside it filter `!x.IsMasterDn`. The dashboard
//   metrics service had no such filter, so it counted master notes that no
//   list screen ever shows.
//
//   That is the same defect class as every other fix in this remediation: one
//   rule with two homes and nothing that errors when they disagree. The
//   symptom is not a crash — it is a dashboard showing a different total from
//   the screen the user acts on.
//
//   This test fails in one direction only: someone adds a delivery-note query
//   to the metrics service without the filter. There is no allow-list to go
//   stale, because the correct answer is "every query, no exceptions".
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Architecture
{
    public sealed class MasterDnFilterParityTests
    {
        private const string ServiceRelPath =
            "Services/Dashboard/Core/DashboardMetricsService.cs";

        [Fact]
        public void Metrics_service_filters_Master_DNs_from_every_delivery_note_query()
        {
            var path = Path.Combine(Root(), ServiceRelPath);
            Assert.True(File.Exists(path), $"Expected {ServiceRelPath} to exist.");

            var text = File.ReadAllText(path);

            // Every `db.DeliveryNotes` read in the service. The projection
            // chains that follow are short, so a fixed window is enough to
            // reach the Where() clause — and deliberately small enough that a
            // filter placed three statements away would NOT be credited.
            var queries = Regex.Matches(text, @"db\.DeliveryNotes");

            Assert.True(
                queries.Count > 0,
                "No db.DeliveryNotes query found in the metrics service. Either the " +
                "delivery metrics moved elsewhere (update this test's path) or they " +
                "were removed.");

            var unfiltered = new List<string>();

            foreach (Match q in queries)
            {
                var window = text.Substring(
                    q.Index,
                    System.Math.Min(QueryWindow, text.Length - q.Index));

                if (!MasterDnFilter.IsMatch(window))
                    unfiltered.Add(Summarise(window));
            }

            Assert.True(
                unfiltered.Count == 0,
                "Delivery-note queries in the dashboard metrics service must exclude " +
                "Master DNs, matching DeliveryNoteRepository.GetAllAsync and every " +
                "per-status query beside it. Without the filter the Overview and " +
                "Delivery Note dashboards count notes that no list screen shows.\n" +
                "Unfiltered queries:\n  " + string.Join("\n  ", unfiltered));
        }

        [Fact]
        public void Delivery_repository_still_filters_Master_DNs()
        {
            // The other half of the parity: if the repository's filter is ever
            // removed, this service's filter stops being "parity" and becomes
            // the outlier. Pin both ends.
            var path = Path.Combine(
                Root(), "Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs");

            Assert.True(File.Exists(path));

            var text = File.ReadAllText(path);

            Assert.Contains("!x.IsMasterDn", text);
        }

        /// <summary>
        /// How far past `db.DeliveryNotes` to look for the filter. Generous
        /// enough for the AsNoTracking/Where/Select chain, small enough that
        /// an unrelated later Where() cannot be mistaken for this query's.
        /// </summary>
        private const int QueryWindow = 400;

        private static readonly Regex MasterDnFilter =
            new(@"!\s*\w+\.IsMasterDn", RegexOptions.Compiled);

        /// <summary>First line of the query, for the failure message.</summary>
        private static string Summarise(string window)
        {
            var first = window.Split('\n')
                .Select(l => l.Trim())
                .FirstOrDefault(l => l.Length > 0);
            return first ?? "(empty)";
        }

        private static string Root()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                dir = dir.Parent;
            Assert.NotNull(dir);
            return dir!.FullName;
        }
    }
}
