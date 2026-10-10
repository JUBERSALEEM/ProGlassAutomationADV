// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/OrderReport/Repository/IOrderReportRepository.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — repository contract (2026-09-06)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.OrderReport;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.OrderReport.Repository
{
    public interface IOrderReportRepository
    {
        /// <summary>Ensure DB file + table exist. Called once at app startup.</summary>
        Task InitializeAsync();

        /// <summary>Returns the saved report with its full snapshot by id.</summary>
        Task<SavedOrderReport?> GetByIdAsync(int id);

        /// <summary>Returns all reports as lightweight list items (CreatedAt DESC).</summary>
        Task<List<OrderReportListItem>> GetAllAsync(
            CancellationToken cancellationToken = default);

        /// <summary>Next report number, e.g. REP-2026-001 (MAX-based, collision-proof).</summary>
        Task<string> GetNextReportNumberAsync();

        Task<SavedOrderReport> InsertAsync(SavedOrderReport entity);
        Task UpdateAsync(SavedOrderReport entity, int expectedRowVersion);
        Task DeleteAsync(int id, int expectedRowVersion);

        /// <summary>Updates only PdfPath after PDF generation using the captured version.</summary>
        Task<int> UpdatePdfPathAsync(
            int reportId, string pdfPath, int expectedRowVersion);
    }
}
