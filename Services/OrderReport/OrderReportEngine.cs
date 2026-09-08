// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/OrderReport/OrderReportEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Report Generator — order fulfilment report engine (2026-09-06)
//
// PURPOSE:
//   Builds ONE consolidated OrderReportSnapshot per Proforma family by
//   composing the exact same cross-database reads the Statement Generator
//   and OrderReconciliationService already use. It adds the pieces the
//   read-only reports don't have:
//     • job-release header block,
//     • per-DN weight rollups (kg),
//     • per-DN / per-TI attachment counts (real data),
//     • "prepared vs attached → missing" detection.
//
//   Never throws: returns a degraded snapshot on error, mirroring
//   OrderReconciliationService.BuildForProformaAsync.
//
//   Stateless per call; repositories fall back to App.* statics so both
//   DI and direct construction work.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.OrderReport;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.Statement.Reconciliation;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Services.Statement.Reconciliation;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.OrderReport
{
    public class OrderReportEngine
    {
        private static readonly JsonSerializerOptions _jsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            AllowTrailingCommas = true
        };

        private readonly IProformaInvoiceRepository _invoices;
        private readonly IJobOrderRepository _jobs;
        private readonly IDeliveryNoteRepository _deliveryNotes;
        private readonly ITaxInvoiceRepository _taxInvoices;
        private readonly IReportingDataService _reporting;
        private readonly OrderReconciliationService _reconciliation;
        private readonly AttachmentCompletenessService _attachments;

        public OrderReportEngine(
            IProformaInvoiceRepository? invoices = null,
            IJobOrderRepository? jobs = null,
            IDeliveryNoteRepository? deliveryNotes = null,
            ITaxInvoiceRepository? taxInvoices = null,
            IReportingDataService? reporting = null,
            AttachmentCompletenessService? attachments = null)
        {
            _invoices = invoices ?? App.Invoices;
            _jobs = jobs ?? App.Jobs;
            _deliveryNotes = deliveryNotes ?? App.DeliveryNotes;
            _taxInvoices = taxInvoices ?? App.TaxInvoices;
            _reporting = reporting ?? App.Reporting;
            _reconciliation = new OrderReconciliationService();
            _attachments = attachments ?? new AttachmentCompletenessService();
        }

        /// <summary>
        /// Builds the consolidated report for a single Proforma Invoice.
        /// </summary>
        public async Task<OrderReportSnapshot> BuildForProformaAsync(
            string proformaId, CancellationToken ct = default)
        {
            var snapshot = new OrderReportSnapshot
            {
                Header = new OrderReportHeader { ProformaId = proformaId ?? "" }
            };

            if (string.IsNullOrWhiteSpace(proformaId)) return snapshot;

            try
            {
                // ── 1. PI header ─────────────────────────────────────────
                var pi = await _invoices.GetByProformaIdAsync(proformaId);
                ProformaInvoiceSnapshot? piSnapshot = null;

                if (pi != null)
                {
                    snapshot.Header.PiStatus = pi.Status ?? "";
                    snapshot.Header.CompanyName = pi.CompanyName ?? "";
                    snapshot.Header.CustomerName = pi.CustomerName ?? "";
                    snapshot.Header.ProjectName = pi.ProjectName ?? "";
                    snapshot.Header.IssueDate = pi.IssueDate;
                    snapshot.Header.ValidUntil = pi.ValidUntil;
                    snapshot.Header.GrandTotal = pi.GrandTotalVatInclusive;

                    if (!string.IsNullOrWhiteSpace(pi.FullSnapshotJson) && pi.FullSnapshotJson != "{}")
                    {
                        try
                        {
                            piSnapshot = JsonSerializer.Deserialize<ProformaInvoiceSnapshot>(
                                pi.FullSnapshotJson, _jsonOpts);
                        }
                        catch (Exception ex)
                        {
                            AppLogService.Debug($"[OrderReport] PI snapshot parse failed {proformaId}: {ex.Message}");
                        }
                    }

                    if (piSnapshot != null)
                    {
                        snapshot.Header.CompanyTrn = piSnapshot.Company?.CompanyTrn ?? "";
                        snapshot.Header.Salesman = piSnapshot.Client?.SalesRepresentative ?? "";
                        snapshot.Header.PreparedBy = piSnapshot.PreparedBy?.PreparedByName ?? "";
                        snapshot.Step9 = BuildStep9Analysis(piSnapshot);
                    }
                }

                // ── 2. Job order (release) data ──────────────────────────
                var jobOrders = await _jobs.GetBySourceProformaIdAsync(proformaId);
                var jobOrderIds = jobOrders
                    .Select(jo => jo.JobOrderId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToList();

                if (jobOrderIds.Count > 0)
                {
                    var first = jobOrders.FirstOrDefault(jo => !string.IsNullOrWhiteSpace(jo.JobOrderId));
                    snapshot.Header.JobOrderIds = string.Join(",", jobOrderIds);
                    if (first != null)
                    {
                        snapshot.Header.JobOrderStatus = first.Status ?? "";
                        snapshot.Header.JobOrderPriority = first.Priority ?? "";
                        snapshot.Header.ReleaseDate = first.DeliveryDate;
                        snapshot.Header.ConvertedAt =
                            first.CreatedAt == default ? (DateTime?)null : first.CreatedAt;
                    }
                }

                // ── 3. PI specification rows (same as the Proforma Invoice) ──
                var specRows = await _reporting.GetPiRowsDetailAsync(proformaId, ct);
                snapshot.Header.TotalQty = specRows.Sum(r => r.Qty);
                snapshot.Header.TotalSqm = Math.Round(specRows.Sum(r => r.TotalSqm), 3);

                // ── 4. Reconciliation (reuses the proven engine) ──────────
                var reconciliation = await _reconciliation.BuildForProformaAsync(proformaId);
                snapshot.Reconciliation = reconciliation;

                var reconciledByKey = reconciliation.Rows
                    .GroupBy(r => r.CompositeKey, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

                foreach (var row in specRows)
                {
                    string key = $"{row.GroupNumber}|{row.SerialNumber}";
                    reconciledByKey.TryGetValue(key, out var rec);

                    snapshot.SpecRows.Add(new OrderReportLineItem
                    {
                        GroupNumber = row.GroupNumber,
                        GroupName = row.GroupName,
                        SerialNumber = row.SerialNumber,
                        GlassRef = row.GlassRef,
                        SpecName = row.SpecName,
                        GlassType = row.GlassType,
                        OuterColor = row.OuterColor,
                        InnerColor = row.InnerColor,
                        W1 = row.W1,
                        H1 = row.H1,
                        W2 = row.W2,
                        H2 = row.H2,
                        Qty = row.Qty,
                        Sqm1 = row.Sqm1,
                        TotalSqm = row.TotalSqm,
                        Rate = row.Rate,
                        Amount = row.Amount,
                        DeliveredQty = rec?.DeliveredQty ?? row.DeliveredQty,
                        BalanceQty = rec?.BalanceQty ?? row.BalanceQty,
                        InvoicedQty = rec?.InvoicedQty ?? row.InvoicedQty,
                        DeliveryStatus = row.DeliveryStatus,
                        InvoiceStatus = row.InvoiceStatus
                    });
                }

                // ── 5. Delivery Notes (confirmed only) + weight map ──────
                var weightPerPiece = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
                var preparedDnIds = new List<string>();
                var dnListItems = new List<DeliveryNoteListItem>();

                foreach (var joId in jobOrderIds)
                {
                    var dns = await _deliveryNotes.GetByJobOrderIdAsync(joId);
                    dnListItems.AddRange(dns);
                }

                foreach (var dnListItem in dnListItems)
                {
                    // Master / Dummy DNs are internal trackers — never
                    // "prepared" documents (same rule as reconciliation).
                    if (dnListItem.IsMasterDn ||
                        string.Equals(dnListItem.DeliveryType, "Dummy", StringComparison.OrdinalIgnoreCase))
                        continue;

                    preparedDnIds.Add(dnListItem.DeliveryNoteId);

                    SavedDeliveryNote? dn = null;
                    try
                    {
                        dn = await _deliveryNotes.GetByDeliveryNoteIdAsync(dnListItem.DeliveryNoteId);
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug($"[OrderReport] DN load failed {dnListItem.DeliveryNoteId}: {ex.Message}");
                    }

                    var section = new OrderReportDeliveryNote
                    {
                        DeliveryNoteId = dnListItem.DeliveryNoteId,
                        SequenceNumber = dnListItem.SequenceNumber,
                        Status = dnListItem.Status ?? "",
                        DeliveryType = dnListItem.DeliveryType ?? "Original",
                        IsFoc = dnListItem.IsFoc,
                        DeliveryDate = dnListItem.DeliveryDate,
                        DeliveryTime = dnListItem.DeliveryTime ?? "",
                        DriverName = dnListItem.DriverName ?? "",
                        VehicleNumber = dnListItem.VehicleNumber ?? "",
                        ReceiverName = dn?.ReceiverName ?? "",
                        Notes = dn?.Notes ?? "",
                        TotalDeliveredQty = dnListItem.TotalDeliveredQty,
                        TotalBalanceQty = dnListItem.TotalBalanceQty
                    };

                    if (dn?.Items != null)
                    {
                        foreach (var item in dn.Items)
                        {
                            string key = $"{item.SpecGroupNumber}|{item.SerialNumber}";
                            if (item.WeightPerPiece > 0 &&
                                !weightPerPiece.ContainsKey(key))
                            {
                                weightPerPiece[key] = item.WeightPerPiece;
                            }

                            section.Items.Add(new OrderReportDeliveryNoteItem
                            {
                                SpecGroupNumber = item.SpecGroupNumber,
                                SpecGroupName = item.SpecGroupName ?? "",
                                SerialNumber = item.SerialNumber,
                                GlassRef = item.GlassRef ?? "",
                                W1 = item.W1,
                                H1 = item.H1,
                                W2 = item.W2,
                                H2 = item.H2,
                                OrderedQty = item.OrderedQty,
                                PreviouslyDeliveredQty = item.PreviouslyDeliveredQty,
                                DeliveredQty = item.DeliveredQty,
                                BalanceQty = item.ComputedBalanceQty,
                                WeightPerPiece = item.WeightPerPiece
                            });
                        }
                        section.TotalWeightKg = section.Items.Sum(i => i.TotalWeight);
                    }

                    snapshot.DeliveryNotes.Add(section);
                }

                // ── 6. Tax Invoices ──────────────────────────────────────
                var tiListItems = await _taxInvoices.GetByProformaIdAsync(proformaId);
                foreach (var tiListItem in tiListItems)
                {
                    SavedTaxInvoice? ti = null;
                    try
                    {
                        ti = await _taxInvoices.GetByInvoiceNumberAsync(tiListItem.InvoiceNumber);
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug($"[OrderReport] TI load failed {tiListItem.InvoiceNumber}: {ex.Message}");
                    }

                    var section = new OrderReportTaxInvoice
                    {
                        InvoiceNumber = tiListItem.InvoiceNumber,
                        InvoiceType = tiListItem.InvoiceType ?? "Full",
                        SequenceNumber = tiListItem.SequenceNumber,
                        Status = tiListItem.Status ?? "",
                        DeliveryNoteReference = tiListItem.DeliveryNoteReference ?? "",
                        IssueDate = tiListItem.IssueDate,
                        DueDate = tiListItem.DueDate,
                        Subtotal = tiListItem.Subtotal,
                        DiscountAmount = tiListItem.DiscountAmount,
                        VatAmount = tiListItem.VatAmount,
                        GrandTotal = tiListItem.GrandTotal,
                        TotalQty = tiListItem.TotalQty,
                        TotalSqm = tiListItem.TotalSqm
                    };

                    if (ti?.Items != null)
                    {
                        foreach (var item in ti.Items)
                        {
                            section.Items.Add(new OrderReportTaxInvoiceItem
                            {
                                SpecGroupNumber = item.SpecGroupNumber,
                                SpecGroupName = item.SpecGroupName ?? "",
                                GlassType = item.GlassType ?? "",
                                SerialNumber = item.SerialNumber,
                                GlassRef = item.GlassRef ?? "",
                                Qty = item.Qty,
                                Sqm = item.Sqm,
                                TotalSqm = item.TotalSqm,
                                Rate = item.Rate,
                                Value = item.Value
                            });
                        }
                    }

                    snapshot.TaxInvoices.Add(section);
                }

                // ── 7. Attachment completeness ───────────────────────────
                snapshot.Attachments = await _attachments.ComputeAsync(
                    proformaId,
                    preparedDnIds,
                    tiListItems.Select(t => t.InvoiceNumber).ToList());

                // Attach per-DN counts onto the DN sections.
                var dnCounts = preparedDnIds.Count > 0
                    ? await _deliveryNotes.GetAttachmentCountsAsync(preparedDnIds)
                    : new Dictionary<string, int>();
                foreach (var dn in snapshot.DeliveryNotes)
                    dn.AttachmentCount = dnCounts.TryGetValue(dn.DeliveryNoteId, out int c) ? c : 0;

                // ── 8. Totals (qty, sqm, weight, value) ───────────────────
                snapshot.Totals = BuildTotals(snapshot, reconciliation, weightPerPiece);

                // ── 9. Warnings ───────────────────────────────────────────
                BuildWarnings(snapshot);

                AppLogService.Debug(
                    $"[OrderReport] Built {proformaId}: " +
                    $"{snapshot.SpecRows.Count} rows, " +
                    $"{snapshot.DeliveryNotes.Count} DNs, " +
                    $"{snapshot.TaxInvoices.Count} TIs, " +
                    $"missing={snapshot.Attachments.DeliveryNotesMissing}");
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[OrderReport] ERROR for {proformaId}: {ex.Message}");
            }

            return snapshot;
        }

        /// <summary>Builds the multi-PI report (one snapshot per PI, ordered).</summary>
        public async Task<List<OrderReportSnapshot>> BuildBatchAsync(
            IEnumerable<string> proformaIds, CancellationToken ct = default)
        {
            var unique = (proformaIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var result = new List<OrderReportSnapshot>(unique.Count);
            foreach (var id in unique)
            {
                ct.ThrowIfCancellationRequested();
                result.Add(await BuildForProformaAsync(id, ct));
            }
            return result;
        }

        // ═════════════════════════════════════════════════════════════════
        //  PRIVATE
        // ═════════════════════════════════════════════════════════════════

        private static OrderReportStep9Analysis BuildStep9Analysis(ProformaInvoiceSnapshot pi)
        {
            var result = new OrderReportStep9Analysis();
            var pnl = pi?.ProfitLoss;
            if (pnl == null) return result;

            result.NetExVat = pnl.NetExVat;
            result.TotalGlassCost = pnl.TotalGlassCost;
            result.TotalProductionCost = pnl.TotalProductionCost;
            result.Profit = pnl.Profit;
            result.MarginPercent = pnl.MarginPercent;
            result.OverallMarginPercent = pnl.OverallMarginPercent;
            result.WastagePercent = pnl.WastagePercent;
            result.CostAtSellingPrice = pnl.CostAtSellingPrice;
            result.ComputedAtUtc = pnl.ComputedAtUtc;
            result.GlassPriceStamp = pnl.GlassPriceStamp ?? "";
            result.TargetTotalGivenValue = pnl.TargetTotalGivenValue;
            result.TargetTotalRequiredValue = pnl.TargetTotalRequiredValue;
            result.TargetTotalDifferenceValue = pnl.TargetTotalDifferenceValue;
            result.TargetAverageBasePricePerSqm = pnl.TargetAverageBasePricePerSqm;
            result.TargetExtraProfitMarginPercent = pnl.TargetExtraProfitMarginPercent;
            result.TargetMarginAgainstTargetPercent = pnl.TargetMarginAgainstTargetPercent;
            result.TargetStatus = pnl.TargetStatus ?? "";
            result.TargetUInsertTreatmentSummary = pnl.TargetUInsertTreatmentSummary ?? "";
            result.TargetCalculationVersion = pnl.TargetCalculationVersion ?? "";

            foreach (var row in pnl.Rows ?? new List<ProfitLossRowSnapshot>())
            {
                string kind = row.Kind ?? "";
                string section = kind.Equals("Glass", StringComparison.OrdinalIgnoreCase)
                    ? "STEP 9 · SHEET MATERIAL COST"
                    : kind.Equals("Spacer", StringComparison.OrdinalIgnoreCase)
                        ? "STEP 9 · SPACER / PRODUCTION SERVICES"
                        : "STEP 9 · PRODUCTION / ADD-ONS";

                result.CostRows.Add(new OrderReportStep9CostRow
                {
                    Section = section,
                    SpecNumber = row.SpecNumber,
                    SpecName = row.SpecName ?? "",
                    Kind = kind,
                    Description = row.Description ?? "",
                    SheetWidthMm = row.SheetWidthMm,
                    SheetHeightMm = row.SheetHeightMm,
                    SheetSqm = row.SheetSqm,
                    Quantity = row.Quantity,
                    PricePerSqm = row.PricePerSqm,
                    UnitLabel = row.UnitLabel ?? "SQM",
                    Amount = row.Amount,
                    SheetSource = row.SheetSource ?? ""
                });
            }

            foreach (var target in pnl.SpecTargets ?? new List<SpecTargetSnapshot>())
            {
                result.TargetRows.Add(new OrderReportStep9TargetRow
                {
                    SpecNumber = target.SpecNumber,
                    SpecName = target.SpecName ?? "",
                    GlassTypeCode = target.GlassTypeCode ?? "",
                    BuildType = target.BuildType ?? "",
                    AreaSqm = target.AreaSqm,
                    GivenPricePerSqm = target.GivenPricePerSqm,
                    BasePricePerSqm = target.BasePricePerSqm,
                    OuterGlassRatePerSqm = target.OuterGlassRatePerSqm,
                    InnerGlassRatePerSqm = target.InnerGlassRatePerSqm,
                    ThirdGlassRatePerSqm = target.ThirdGlassRatePerSqm,
                    AspRatePerSqm = target.AspRatePerSqm,
                    ProcessingRatePerSqm = target.ProcessingRatePerSqm,
                    FinalRequiredTargetPerSqm = target.FinalRequiredTargetPerSqm,
                    DifferencePerSqm = target.DifferencePerSqm,
                    ExtraProfitMarginPercent = target.ExtraProfitMarginPercent,
                    MarginAgainstTargetPercent = target.MarginAgainstTargetPercent,
                    Status = target.Status ?? "",
                    FormulaText = target.FormulaText ?? "",
                    Warnings = target.Warnings ?? new List<string>()
                });
            }

            if (!result.HasData)
            {
                result.Warnings.Add("Step 9 P&L was not saved with this PI; open PI Step 9 and recalculate before generating financial reports.");
                return result;
            }

            result.Recommendations.Add(BuildStep9Recommendation(result));
            if (result.TargetRows.Any(r => r.Warnings.Count > 0))
                result.Warnings.Add("One or more Step 9 target specifications have warnings; review target cards before approval.");
            if (result.CostRows.Any(r => r.SheetSource.IndexOf("MISSING", StringComparison.OrdinalIgnoreCase) >= 0))
                result.Warnings.Add("One or more Step 9 material rows are missing an authoritative Step 4 selected sheet.");
            if (pnl.TargetWarnings != null)
                result.Warnings.AddRange(pnl.TargetWarnings.Where(w => !string.IsNullOrWhiteSpace(w)));

            return result;
        }

        private static string BuildStep9Recommendation(OrderReportStep9Analysis step9)
        {
            if (step9 == null || !step9.HasData)
                return "Run Step 9 before approval so material, spacer and target margins are available.";

            if (step9.Profit < 0m)
                return $"Loss warning: Step 9 shows AED {Math.Abs(step9.Profit):N2} loss. Recheck given price, sheet selection, wastage and production add-ons.";

            if (step9.TargetMarginAgainstTargetPercent.HasValue && step9.TargetMarginAgainstTargetPercent.Value < 0m)
                return $"Below target: margin against target is {step9.TargetMarginAgainstTargetPercent.Value:0.##}%. Review base price or production charges before release.";

            return $"Step 9 appears commercially acceptable: profit AED {step9.Profit:N2}, margin {step9.MarginPercent:0.#}%, target status {step9.TargetStatus}.";
        }

        private static OrderReportTotals BuildTotals(
            OrderReportSnapshot snapshot,
            OrderReconciliationSummary reconciliation,
            IReadOnlyDictionary<string, decimal> weightPerPiece)
        {
            var totals = new OrderReportTotals();

            foreach (var row in reconciliation.Rows)
            {
                totals.OrderedQty += row.OrderedQty;
                totals.OrderedSqm += row.TotalSqm;
                totals.DeliveredQty += row.DeliveredQty;
                totals.BalanceQty += row.BalanceQty;
                totals.InvoicedQty += row.InvoicedQty;

                decimal wpp = weightPerPiece.TryGetValue(row.CompositeKey, out decimal w) ? w : 0m;
                totals.OrderedWeightKg += wpp * row.OrderedQty;
                totals.DeliveredWeightKg += wpp * row.DeliveredQty;
                totals.BalanceWeightKg += wpp * row.BalanceQty;
            }

            totals.DeliveredSqm = snapshot.DeliveryNotes.Sum(d =>
                d.Items.Sum(i => (decimal)i.DeliveredQty *
                    ProGlassAutomation.Utilities.SqmCalculator.CalculatePanel(i.W1, i.H1)));

            totals.OrderedValue = snapshot.Header.GrandTotal;

            foreach (var ti in snapshot.TaxInvoices)
            {
                if (IsActiveInvoice(ti.Status))
                {
                    totals.InvoicedValue += ti.GrandTotal;
                    totals.InvoicedVat += ti.VatAmount;
                }
            }

            return totals;
        }

        private static void BuildWarnings(OrderReportSnapshot snapshot)
        {
            var list = snapshot.Warnings;

            if (snapshot.Attachments.HasMissingDocuments)
            {
                list.Add(new OrderReportWarning
                {
                    Severity = "Warning",
                    Title = "Missing delivery-note attachments",
                    Detail = snapshot.Attachments.MissingDocumentsDisplay,
                    ActionKind = "OpenProforma",
                    TargetId = snapshot.Header.ProformaId
                });
            }

            if (snapshot.Reconciliation?.HasOverInvoicing == true)
            {
                list.Add(new OrderReportWarning
                {
                    Severity = "Error",
                    Title = "Over-invoiced",
                    Detail = $"{snapshot.Reconciliation.OverInvoicedCount} line(s) invoiced more than delivered.",
                    ActionKind = "OpenProforma",
                    TargetId = snapshot.Header.ProformaId
                });
            }

            if (snapshot.Reconciliation?.HasUnderInvoicing == true)
            {
                list.Add(new OrderReportWarning
                {
                    Severity = "Warning",
                    Title = "Under-invoiced (revenue leakage)",
                    Detail = $"{snapshot.Reconciliation.UnderInvoicedCount} line(s) delivered but not fully invoiced.",
                    ActionKind = "OpenJobOrder",
                    TargetId = snapshot.Reconciliation.JobOrderIds
                });
            }
        }

        private static bool IsActiveInvoice(string? status)
        {
            if (string.IsNullOrWhiteSpace(status)) return false;
            return status.Trim().Equals("Issued", StringComparison.OrdinalIgnoreCase) ||
                   status.Trim().Equals("Paid", StringComparison.OrdinalIgnoreCase) ||
                   status.Trim().Equals("PartiallyPaid", StringComparison.OrdinalIgnoreCase) ||
                   status.Trim().Equals("Overdue", StringComparison.OrdinalIgnoreCase);
        }
    }
}
