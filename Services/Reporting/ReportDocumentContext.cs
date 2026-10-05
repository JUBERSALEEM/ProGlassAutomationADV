// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportDocumentContext.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — document identity context
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Globalization;

namespace ProGlassAutomation.Services.Reporting
{
    /// <summary>
    /// Optional, document-specific identity supplied to the generic reporting
    /// layouts. A report that does not supply this context keeps the legacy
    /// generic title, filename identity and header treatment.
    ///
    /// The context intentionally keeps report identity separate from report
    /// content: CSV, Excel and PDF can show the same PI/customer/project
    /// metadata without each report mapper duplicating it.
    /// </summary>
    public sealed class ReportDocumentContext
    {
        /// <summary>Short document family shown by the save dialog (for example, PI).</summary>
        public string DocumentPrefix { get; init; } = "PI";

        /// <summary>Complete report-specific identifier used as the first filename segment.</summary>
        public string DocumentId { get; init; } = "";

        /// <summary>Proforma identifier as entered in Step 1.</summary>
        public string ProformaId { get; init; } = "";

        /// <summary>Issuing company name from Step 1.</summary>
        public string CompanyName { get; init; } = "";

        /// <summary>Customer name from Step 2.</summary>
        public string CustomerName { get; init; } = "";

        /// <summary>Project identifier/name from Step 3.</summary>
        public string ProjectName { get; init; } = "";

        /// <summary>PI/report issue date from Step 1.</summary>
        public DateTime IssueDate { get; init; }

        /// <summary>Current PI workflow status from Step 1.</summary>
        public string Status { get; init; } = "";

        /// <summary>Timestamp captured once when the export starts.</summary>
        public DateTime GeneratedAt { get; init; }

        /// <summary>
        /// Metadata in a stable display order for CSV reports. Values are
        /// deliberately explicit (including unavailable draft fields) so a
        /// printed report cannot be mistaken for an anonymous export.
        /// </summary>
        public IReadOnlyList<KeyValuePair<string, string>> MetadataFields => new[]
        {
            new KeyValuePair<string, string>("Proforma No.", Display(ProformaId)),
            new KeyValuePair<string, string>("Company", Display(CompanyName)),
            new KeyValuePair<string, string>("Customer", Display(CustomerName)),
            new KeyValuePair<string, string>("Project", Display(ProjectName)),
            new KeyValuePair<string, string>("Report / Invoice Date", IssueDateDisplay),
            new KeyValuePair<string, string>("Status", Display(Status)),
            new KeyValuePair<string, string>("Generated", GeneratedAtDisplay)
        };

        /// <summary>Compact first line used by Excel and PDF headers.</summary>
        public string PrimaryMetadataLine =>
            $"Proforma No. {Display(ProformaId)}  •  Customer {Display(CustomerName)}";

        /// <summary>Compact second line used by Excel and PDF headers.</summary>
        public string SecondaryMetadataLine =>
            $"Company {Display(CompanyName)}  •  Project {Display(ProjectName)}";

        /// <summary>Compact date/status line used by Excel and PDF headers.</summary>
        public string TertiaryMetadataLine =>
            $"Report / Invoice Date {IssueDateDisplay}  •  Status {Display(Status)}";

        /// <summary>Stable, culture-invariant generated-time readout for visible report headers.</summary>
        public string GeneratedAtDisplay => GeneratedAt == default
            ? "—"
            : GeneratedAt.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);

        /// <summary>Stable, culture-invariant report/invoice-date readout for visible report headers.</summary>
        public string IssueDateDisplay => IssueDate == default
            ? "—"
            : IssueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture);

        /// <summary>Customer is the established second filename segment; use company only as a draft fallback.</summary>
        public string FilenameCustomer => FirstFilled(CustomerName, CompanyName);

        /// <summary>Project identifier/name is the established third filename segment.</summary>
        public string FilenameReference => FirstFilled(ProjectName);

        /// <summary>PI issue date takes precedence over the export timestamp in a professional filename.</summary>
        public DateTime FilenameDate => IssueDate != default
            ? IssueDate
            : GeneratedAt != default
                ? GeneratedAt
                : DateTime.Now;

        private static string Display(string? value) =>
            string.IsNullOrWhiteSpace(value) ? "—" : value.Trim();

        private static string FirstFilled(params string?[] values)
        {
            foreach (string? value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                    return value.Trim();
            }

            return string.Empty;
        }
    }
}
