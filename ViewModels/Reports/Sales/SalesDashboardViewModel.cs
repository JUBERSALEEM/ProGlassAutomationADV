using ProGlassAutomation.Models.Reports.Sales;
using ProGlassAutomation.Services.Reports.CalculationService;
using ProGlassAutomation.Services.Reports.Mapper;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ProGlassAutomation.ViewModels.Reports.Sales
{
    public class MonthlySalesTrendItem : ViewModelBase
    {
        public string MonthName { get; set; } = "";
        public decimal Amount { get; set; }
        public string DisplayAmount => $"AED {Amount:N0}";
    }

    public class SalesDashboardViewModel : ViewModelBase
    {
        private readonly SalesAnalyticsService _analyticsService;

        // ── KPIs ─────────────────────────────────────────────────────────

        private SalesSummary _summary = new();
        public SalesSummary Summary
        {
            get => _summary;
            set { _summary = value; OnPropertyChanged(); }
        }

        // ── Lists & Charts ───────────────────────────────────────────────

        public ObservableCollection<MonthlySalesTrendItem> MonthlyTrends { get; } = new();
        public ObservableCollection<CustomerSalesReport> TopCustomers { get; } = new();
        public ObservableCollection<SalesmanSalesReport> SalesmenPerformance { get; } = new();
        public ObservableCollection<ProjectSalesReport> ProjectBreakdown { get; } = new();

        public SalesDashboardViewModel()
        {
            _analyticsService = new SalesAnalyticsService();
        }

        public void UpdateDashboard(List<SalesReport> filteredRecords)
        {
            if (filteredRecords == null) return;

            // 1. Compute KPIs
            Summary = _analyticsService.ComputeSummary(filteredRecords);

            // 2. Compute 6-Month Trend Chart
            MonthlyTrends.Clear();
            var trends = _analyticsService.GetMonthlySalesTrend(filteredRecords);
            foreach (var t in trends)
            {
                MonthlyTrends.Add(new MonthlySalesTrendItem
                {
                    MonthName = t.Key,
                    Amount = t.Value
                });
            }

            // 3. Compute Top Customer Rankings
            TopCustomers.Clear();
            var topCusts = SalesReportMapper.GroupByCustomer(filteredRecords).Take(5);
            foreach (var tc in topCusts)
            {
                TopCustomers.Add(tc);
            }

            // 4. Compute Salesmen Performance
            SalesmenPerformance.Clear();
            var salesmenList = SalesReportMapper.GroupBySalesman(filteredRecords);
            foreach (var sm in salesmenList)
            {
                SalesmenPerformance.Add(sm);
            }

            // 5. Compute Project Breakdown
            ProjectBreakdown.Clear();
            var projList = SalesReportMapper.GroupByProject(filteredRecords).Take(5);
            foreach (var p in projList)
            {
                ProjectBreakdown.Add(p);
            }
        }
    }
}