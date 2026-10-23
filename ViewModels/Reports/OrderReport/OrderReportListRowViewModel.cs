// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Reports/OrderReport/OrderReportListRowViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — saved report list row
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.OrderReport;

namespace ProGlassAutomation.ViewModels.Reports.OrderReport
{
    public class OrderReportListRowViewModel : ViewModelBase
    {
        private readonly OrderReportListItem _item;

        public OrderReportListRowViewModel(OrderReportListItem item)
        {
            _item = item;
        }

        public int Id => _item.Id;
        public int RowVersion => _item.RowVersion;
        public string ReportNumber => _item.ReportNumber;
        public string Title => _item.Title;
        public string SubTitle => _item.SubTitle;
        public string FilterCustomer => _item.FilterCustomer;
        public string FilterCompany => _item.FilterCompany;
        public string FilterProject => _item.FilterProject;
        public string FilterSalesman => _item.FilterSalesman;
        public int PiCount => _item.PiCount;
        public int TotalOrderedQty => _item.TotalOrderedQty;
        public int TotalDeliveredQty => _item.TotalDeliveredQty;
        public int TotalInvoicedQty => _item.TotalInvoicedQty;
        public int MissingDocuments => _item.MissingDocuments;
        public string TotalInvoicedValueDisplay => _item.TotalInvoicedValueDisplay;
        public string CreatedAtDisplay => _item.CreatedAtDisplay;
        public string PdfPath => _item.PdfPath;
        public bool HasPdf => _item.HasPdf;
        public bool HasMissingDocuments => _item.HasMissingDocuments;

        public string QuantitiesDisplay =>
            $"Ordered {TotalOrderedQty}  ·  Delivered {TotalDeliveredQty}  ·  Invoiced {TotalInvoicedQty}";

        public OrderReportListItem Source => _item;
    }
}
