// ═══════════════════════════════════════════════════════════════════════
// FILE: RecentStatementItem.cs
// PROJECT: ProGlassAutomation
// MODULE: StDashboardViewModel.cs
//
// RecentStatementItem split out of StDashboardViewModel.cs (Phase 11/12 God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Statement.Pdf;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Dashboard.Statement
{
    public class RecentStatementItem
    {
        /// <summary>
        /// 1-based serial number displayed as "#1", "#2", etc.
        /// Assigned during VM projection — reflects display order,
        /// not database ID.
        /// </summary>
        public int Index { get; init; }

        public string StatementNumber { get; init; } = "";
        public string Title { get; init; } = "";
        public string SubTitle { get; init; } = "";
        public string TotalInvoicedDisplay { get; init; } = "";
        public string OutstandingDisplay { get; init; } = "";
        public string OverdueDisplay { get; init; } = "";
        public bool HasOverdue { get; init; }
        public string CreatedAtDisplay { get; init; } = "";
        public string PdfPath { get; init; } = "";
        public bool HasPdf { get; init; }
        public bool HasAttachments { get; init; }
        public int InvoiceCount { get; init; }
    }
}
