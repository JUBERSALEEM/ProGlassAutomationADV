// Pins Overview KPI sources after the forensic pass. Source-level — no SDK.

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Dashboard
{
    public sealed class DashboardKpiSourceTests
    {
        [Fact]
        public void Overview_mtd_tiles_use_this_month_not_all_time()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "Dashboard", "Overview",
                "OverviewDashboardViewModel.cs"));

            Assert.Contains(
                "DashboardDateRange.ThisMonth(DateTime.Now)", src);
            Assert.DoesNotContain(
                "_dashboardFilter.CurrentRange", src);
        }

        [Fact]
        public void Pipeline_delivery_uses_currently_dispatched_not_lifetime_sent()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "ViewModels", "Dashboard", "Overview",
                "OverviewDashboardViewModel.cs"));

            Assert.Contains(
                "exec.Delivery.Preparing + exec.Delivery.CurrentlyDispatched",
                src);
            Assert.DoesNotContain(
                "exec.Delivery.Preparing + exec.Delivery.Dispatched", src);
        }

        [Fact]
        public void Pending_pieces_failure_is_not_shown_as_zero()
        {
            string svc = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Dashboard", "Core",
                "DashboardMetricsService.cs"));
            string model = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Dashboard", "Core",
                "DashboardMetricModels.cs"));

            Assert.Contains("PendingPiecesAvailable = false", svc);
            Assert.Contains("public bool PendingPiecesAvailable", model);
        }

        [Fact]
        public void Recent_feed_excludes_voided_pi_and_jo()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Dashboard", "Core",
                "DashboardMetricsService.cs"));

            Assert.Contains(".Where(i => !i.IsVoid)", src);
            Assert.Contains(".Where(j => !j.IsVoid)", src);
        }

        [Fact]
        public void Sales_metrics_sum_pi_grand_total_ex_vat_not_added()
        {
            string src = File.ReadAllText(Path.Combine(LocateProjectRoot(),
                "Services", "Dashboard", "Core",
                "DashboardMetricsService.cs"));

            Assert.Contains("Total        = i.GrandTotalVatInclusive", src);
            Assert.Contains(".Where(i => !i.IsVoid)", src);
            Assert.DoesNotContain("NetExVat", src);
            Assert.DoesNotContain("GrandTotal +", src);
        }

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Services", "Dashboard", "Core",
                    "DashboardMetricsService.cs");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
