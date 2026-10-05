// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/ReportingDataService.cs
// PROJECT: ProGlassAutomation
// MODULE: Central Reporting — Data Intelligence Layer (HIGH PERFORMANCE CORE)
//
// PURPOSE:
//   ONE centralized reporting/data-intelligence layer responsible for:
//     • relationships / joins / filtering / aggregation
//     • row-level data / specification matching / quantity reconciliation
//     • drill-down / profitability / cross-module traceability
//
// HIGH-SCALE ENTERPRISE HARDENING:
//   • DB-side pagination (.Skip/.Take in SQL before snapshot retrieval)
//   • Zero full-table in-memory JSON deserialization
//   • Database-side aggregation (.CountAsync, .SumAsync, GroupBy)
//   • CancellationToken propagated across every async boundary
//   • Safe string parsing with no OOM even with 100K+ records
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Services.JobOrder.Database;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.MasterData.Database;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Models.Statement;
using ProGlassAutomation.Services.Statement.Database;
using ProGlassAutomation.Services.TaxInvoice.Database;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Reporting
{
    public interface IReportingDataService
    {
        // ── PI Report ────────────────────────────────────────────────
        Task<PiReportSummary> GetPiSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<PiReportRow>> GetPiRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<PiReportRow>> GetPiLifecycleAsync(string proformaId, CancellationToken ct = default);

        // ── JO Report ────────────────────────────────────────────────
        Task<JoReportSummary> GetJoSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<JoReportRow>> GetJoRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Delivery Report — ROW-LEVEL ─────────────────────────────
        // Every row = one exact PI specification row (PI → Spec → Sr No)
        // with its Total / Delivered / Balance quantities reconciled from
        // the DeliveryNoteItems table. Delivered quantities are summed per
        // row key (JobOrderId + SpecGroupNumber + SerialNumber).
        Task<DeliveryReportSummary> GetDeliverySummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<DeliveryReportRow>> GetDeliveryRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        /// <summary>
        /// Job Order MIX analytics — which specifications, colours and
        /// customers dominate the filtered set.
        ///
        /// Built from the same filtered rows the detail table shows, so the
        /// analysis always reconciles to the rows beneath it.
        /// </summary>
        Task<JoAnalyticsSummary> GetJoAnalyticsAsync(
            ReportingFilter filter, CancellationToken ct = default);

        /// <summary>
        /// Delivery ageing and outstanding-work analytics. Answers "how old
        /// is the pending work, and who/what is it for" -- the part that
        /// turns a pending count into a call list.
        /// </summary>
        Task<DeliveryAnalyticsSummary> GetDeliveryAnalyticsAsync(
            ReportingFilter filter, CancellationToken ct = default);

        /// <summary>
        /// Cross-module ORDER-TO-CASH view: Proforma -> Job Order ->
        /// Delivery -> Tax Invoice -> Paid, with the conversion rate at
        /// each step so a bottleneck is visible.
        ///
        /// Composed from the existing per-module summaries under ONE
        /// filter, so it can never disagree with the reports it
        /// summarises.
        /// </summary>
        Task<ExecutiveSummary> GetExecutiveSummaryAsync(
            ReportingFilter filter, CancellationToken ct = default);

        /// <summary>
        /// The Delivery Notes that delivered this exact row (detail panel):
        /// DN #, delivery date, status, quantity delivered in each DN.
        /// </summary>
        Task<List<DeliveryNoteDelivery>> GetRowDeliveriesAsync(
            string jobOrderId, int specGroupNumber, int serialNumber,
            CancellationToken ct = default);

        // ── Delivery report filter lookups (data-driven, not hardcoded) ──
        Task<List<string>> GetDeliveryPiOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetDeliverySpecificationOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetDeliveryGlassRefOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetDeliveryProjectOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetDeliverySalesmanOptionsAsync(CancellationToken ct = default);

        // ── Module-specific filter lookups (each document's own dimension) ──
        Task<List<string>> GetJoSourcePiOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetTiProformaOptionsAsync(CancellationToken ct = default);

        // ── Tax Invoice Report ───────────────────────────────────────
        Task<TaxInvoiceReportSummary> GetTaxInvoiceSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<TaxInvoiceReportRow>> GetTaxInvoiceRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<TaxInvoiceReportRow>> GetPendingInvoicesAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Company / Customer / Salesman / Project ──────────────────
        Task<PagedResult<CompanyReportRow>> GetCompanyRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<CustomerReportRow>> GetCustomerRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<SalesmanReportRow>> GetSalesmanRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<ProjectReportRow>> GetProjectRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Glass / Spec Report — row-level intelligence ─────────────
        Task<PagedResult<GlassSpecReportRow>> GetGlassSpecRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<SpecAggregation>> GetSpecAggregationsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<RowTrace?> GetRowTraceAsync(string rowId, CancellationToken ct = default);

        // ── Statement Report ─────────────────────────────────────────
        Task<StatementReportSummary> GetStatementSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<StatementReportRow>> GetStatementRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Statement FINANCIAL ledger (Phase 8) ─────────────────────
        // One row per statement line item rendered as a ledger document:
        // Date | Document # | Type | Description | Debit | Credit | Balance.
        Task<StatementLineSummary> GetStatementLineSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<StatementLineRow>> GetStatementLineRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Profitability ────────────────────────────────────────────
        Task<ProfitabilitySummary> GetProfitabilitySummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<ProfitabilityRow>> GetProfitabilityRowsAsync(ReportingFilter filter, CancellationToken ct = default);

        // ── Drill-down helpers ───────────────────────────────────────
        Task<List<GlassSpecReportRow>> GetPiRowsDetailAsync(string proformaId, CancellationToken ct = default);
        Task<QuantityReconciliation> GetQuantityReconciliationAsync(string proformaId, CancellationToken ct = default);
        Task<Dictionary<string, object>> GetDocumentChainAsync(string proformaId, CancellationToken ct = default);

        // ── Filter lookups ───────────────────────────────────────────
        Task<List<string>> GetCompanyOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetCustomerOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetSalesmanOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetProjectOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetGlassTypeOptionsAsync(CancellationToken ct = default);

        // ── Status lookups (distinct statuses actually stored) ────────
        Task<List<string>> GetPiStatusOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetJoStatusOptionsAsync(CancellationToken ct = default);
        Task<List<string>> GetTiStatusOptionsAsync(CancellationToken ct = default);

        // ── VAT Submission Report ─────────────────────────────────────
        Task<VatSubmissionSummary> GetVatSubmissionSummaryAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<PagedResult<VatSubmissionRow>> GetVatSubmissionRowsAsync(ReportingFilter filter, CancellationToken ct = default);
        Task<List<VatSubmissionRow>> GetAllVatSubmissionRowsAsync(ReportingFilter filter, CancellationToken ct = default);
    }

    public sealed partial class ReportingDataService : IReportingDataService
    {
        // ── VOIDED / SOFT-DELETED EXCLUSION (ERP correctness) ─────────────
        // Every document (PI/JO/DN/TI) carries an IsVoid soft-delete flag.
        // The user-facing module LISTS hide voided rows by default
        // (SavedInvoicesListViewModel: query.Where(x => !x.IsVoid)), so a
        // voided PI/JO/DN/TI is, to the operator, "deleted — it no longer
        // exists". The reporting layer historically queried the raw tables
        // with NO void filter, so every operational/analytics report counted
        // and valued documents the user had already voided — the "reports
        // read old data that doesn't exist anymore" bug.
        //
        // These helpers apply the exact same exclusion the module lists use.
        // Applied to OPERATIONAL / ANALYTICS reads (Company, Project,
        // Customer, Salesman, PI, JO, Delivery, TaxInvoice, Profitability,
        // GlassSpecs, DrillDown option lists). Deliberately NOT applied to
        // the VAT return, which is a statutory filing that must remain
        // complete — a voided tax invoice still has to be declared/reversed,
        // so VAT keeps its own (unfiltered) source by design.
        //
        // IsVoid is a plain mapped bool column on every entity, so these
        // translate to SQL and stay server-side (no client evaluation).
        private static IQueryable<SavedProformaInvoice> Live(IQueryable<SavedProformaInvoice> q) => q.Where(x => !x.IsVoid);
        private static IQueryable<SavedJobOrder> Live(IQueryable<SavedJobOrder> q) => q.Where(x => !x.IsVoid);
        private static IQueryable<SavedDeliveryNote> Live(IQueryable<SavedDeliveryNote> q) => q.Where(x => !x.IsVoid);
        private static IQueryable<SavedTaxInvoice> Live(IQueryable<SavedTaxInvoice> q) => q.Where(x => !x.IsVoid);

        private static bool Eq(string? a, string b) =>
            string.Equals((a ?? "").Trim(), b, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// The last tick of the selected day (23:59:59.9999999). Entity date
        /// fields are stamped with DateTime.Now and therefore carry a
        /// time-of-day, so comparing them against a bare DateTo (midnight)
        /// silently dropped the ENTIRE LAST DAY of every filtered range
        /// (found 2026-09-10). Use for every "… <= DateTo" report filter.
        /// </summary>
        internal static DateTime DayEnd(DateTime? d) => d.HasValue
            ? d.Value.Date.AddDays(1).AddTicks(-1)
            : DateTime.MinValue;

        private static bool ContainsCI(string? source, string? term)
        {
            if (string.IsNullOrWhiteSpace(term)) return true;
            if (string.IsNullOrWhiteSpace(source)) return false;
            return source.IndexOf(term.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool Within(DateTime dt, DateTime? from, DateTime? to)
        {
            if (from.HasValue && dt.Date < from.Value.Date) return false;
            if (to.HasValue && dt.Date > to.Value.Date) return false;
            return true;
        }

        // ── Snapshot deserialization helpers (safe, never throws) ─────────────

        private static ProformaInvoiceSnapshot? TryDeserializePiSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}") return null;
            try
            {
                return JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch { return null; }
        }

        private static JobOrderSnapshot? TryDeserializeJoSnapshot(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonSerializer.Deserialize<JobOrderSnapshot>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
            }
            catch { return null; }
        }

        private static List<GlassSpecReportRow> ExtractGlassRowsFromPiSnapshot(
            ProformaInvoiceSnapshot snap, string proformaId, string company, string customer,
            string salesman, DateTime issueDate, string piStatus)
        {
            return SpecificationIntelligenceService.Instance.ExtractGlassRowsFromPi(
                snap, proformaId, company, customer, salesman, issueDate, piStatus);
        }










    }
}
