using ClosedXML.Excel;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;   // ← ADDED for template lookup
using ProGlassAutomation.Services.Export.Excel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.Export
{
    /// <summary>
    /// Phase 9.5b — IExcelLayout implementation for Proforma Invoice.
    /// Replaces InvoiceExcelExportService via shared ExcelExportEngine.
    ///
    /// 13-column Glass Panels sheet:
    ///   Spec # | Spec Name | # | Glass Ref | W1 | H1 | W2 | H2 |
    ///   Qty | Area (sqm) | Total Area (sqm) | Rate (AED) | Total (AED)
    ///
    /// CHAT 7 CHANGES:
    ///   Bug 4:  BuildChargesSheet() header "Quantity" → "QTY/SQM"
    ///
    /// CHAT 7 FOLLOW-UP FIX:
    ///   Bug 4b: BuildOtherChargeLines() — proper qty + amount calculation
    ///           for predefined charges (was showing empty qty + 0 amount)
    ///           Now looks up template friendly name + unit + computes
    ///           metric qty from SelectedSourceKey (with fallback to
    ///           template default) — same pattern as PDF layout
    ///
    /// STRANGLER PATTERN Round 1 — legacy InvoiceExcelExportService
    /// still compiles alongside this.
    /// </summary>
    public class ProformaInvoiceExcelLayout : IExcelLayout
    {
        // Brand colours matching PDF layout and legacy service
        private const string ACCENT = "#4F46E5";
        private const string MUTED = "#475569";
        private const string LIGHT_BG = "#F1F5F9";
        private const string TABLE_HEADER = "#1E293B";
        private const string GREEN = "#047857";
        private const string ALT_ROW = "#F8FAFC";

        private readonly SavedProformaInvoice _entity;
        private readonly ProformaInvoiceSnapshot _snapshot;

        public ProformaInvoiceExcelLayout(
            SavedProformaInvoice entity,
            ProformaInvoiceSnapshot snapshot)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        // ── IDocumentLayout ──────────────────────────────────────────────────

        public string DocumentPrefix => "PI";
        public string DocumentId => _entity.ProformaId;
        public string DocumentTitle => "PROFORMA INVOICE";
        public string CompanyName => _entity.CustomerName;
        public string Reference => _entity.ProjectName;
        public DateTime DocumentDate => _entity.IssueDate;

        // ── IExcelLayout ─────────────────────────────────────────────────────

        public void RenderWorkbook(IXLWorkbook workbook)
        {
            BuildSummarySheet(workbook.Worksheets.Add("Invoice Summary"));
            BuildGlassSheet(workbook.Worksheets.Add("Glass Panels"));

            var otherCharges = BuildOtherChargeLines();
            if (otherCharges.Any())
                BuildChargesSheet(
                    workbook.Worksheets.Add("Other Charges"),
                    otherCharges);

            var terms = GetActiveTerms();
            if (terms.Any())
                BuildTermsSheet(
                    workbook.Worksheets.Add("Terms & Conditions"),
                    terms);
        }

        // ════════════════════════════════════════════════════════════════════
        //  SHEET 1 — Invoice Summary (preserved exactly)
        // ════════════════════════════════════════════════════════════════════

        private void BuildSummarySheet(IXLWorksheet ws)
        {
            ws.Cell("A1").Value = "PROFORMA INVOICE";
            ws.Cell("A1").Style.Font.Bold = true;
            ws.Cell("A1").Style.Font.FontSize = 16;
            ws.Cell("A1").Style.Font.FontColor = XLColor.FromHtml(ACCENT);
            ws.Range("A1:D1").Merge();

            int row = 3;

            AddSection(ws, ref row, "COMPANY DETAILS");
            AddField(ws, ref row, "Company Name",
                _snapshot.Company?.CompanyName ?? "");
            AddField(ws, ref row, "Company TRN",
                _snapshot.Company?.CompanyTrn ?? "");
            AddField(ws, ref row, "Location",
                _snapshot.Company?.CompanyLocation ?? "");
            AddField(ws, ref row, "Proforma ID",
                _snapshot.Company?.ProformaId ?? _entity.ProformaId);
            AddField(ws, ref row, "Issue Date",
                _snapshot.Company?.IssueDate.ToString("yyyy-MM-dd") ?? "");
            AddField(ws, ref row, "Valid Until",
                _snapshot.Company?.ValidUntil.ToString("yyyy-MM-dd") ?? "");
            AddField(ws, ref row, "Status",
                _snapshot.Company?.ProformaStatus ?? _entity.Status);
            row++;

            AddSection(ws, ref row, "CLIENT DETAILS");
            AddField(ws, ref row, "Customer Name",
                _snapshot.Client?.CustomerName ?? _entity.CustomerName);
            AddField(ws, ref row, "Customer TRN",
                _snapshot.Client?.CustomerTrn ?? "");
            AddField(ws, ref row, "Attention Name",
                _snapshot.Client?.AttentionExecutiveName ?? "");
            AddField(ws, ref row, "Contact Phone",
                _snapshot.Client?.ClientContactPhone ?? "");
            AddField(ws, ref row, "Billing Address",
                _snapshot.Client?.BillingAddress ?? "");
            AddField(ws, ref row, "Customer Reference",
                _snapshot.Client?.CustomerReferenceCode ?? "");
            AddField(ws, ref row, "Sales Representative",
                _snapshot.Client?.SalesRepresentative ?? "");
            row++;

            AddSection(ws, ref row, "PROJECT DETAILS");
            AddField(ws, ref row, "Project Name",
                _snapshot.Project?.ProjectIdentifierName ?? _entity.ProjectName);
            AddField(ws, ref row, "Contract No",
                _snapshot.Project?.ProjectContractNo ?? "");
            AddField(ws, ref row, "Site Delivery",
                _snapshot.Project?.SiteDeliveryLocation ?? "");
            AddField(ws, ref row, "LPO Number",
                _snapshot.Project?.LpoNumber ?? "");
            row++;

            AddSection(ws, ref row, "FINANCIAL SUMMARY");
            AddField(ws, ref row, "Glass Materials Total",
                $"AED {_entity.GlassMaterialsSubtotal:N2}");
            AddField(ws, ref row, "Other Charges Total",
                $"AED {_entity.OtherChargesSubtotal:N2}");

            decimal netSubtotal = _entity.GlassMaterialsSubtotal
                                  + _entity.OtherChargesSubtotal
                                  - _entity.DiscountAmount;
            AddField(ws, ref row, "Net Subtotal",
                $"AED {netSubtotal:N2}");

            if (_entity.DiscountAmount > 0)
                AddField(ws, ref row, "Discount",
                    $"- AED {_entity.DiscountAmount:N2}");

            if (_entity.VatAmount > 0)
            {
                decimal vatPct = _snapshot.Summary?.VatPercent ?? 5m;
                AddField(ws, ref row, $"VAT ({vatPct:0.##}%)",
                    $"AED {_entity.VatAmount:N2}");
            }

            AddField(ws, ref row, "GRAND TOTAL (VAT INC)",
                $"AED {_entity.GrandTotalVatInclusive:N2}");
            row++;

            AddSection(ws, ref row, "PREPARED BY");
            AddField(ws, ref row, "Name",
                _snapshot.PreparedBy?.PreparedByName ?? "");
            AddField(ws, ref row, "Department",
                _snapshot.PreparedBy?.PreparedByDept ?? "");
            AddField(ws, ref row, "Status",
                _snapshot.PreparedBy?.PreparedByStatus ?? "");

            ws.Column("A").Width = 25;
            ws.Column("B").Width = 40;
        }

        // ════════════════════════════════════════════════════════════════════
        //  SHEET 2 — Glass Panels (preserved exactly — "Qty" = panel qty)
        // ════════════════════════════════════════════════════════════════════

        private void BuildGlassSheet(IXLWorksheet ws)
        {
            var headers = new[]
            {
                "Spec #", "Spec Name", "#", "Glass Ref",
                "W1", "H1", "W2", "H2",
                "Qty", "Area (sqm)", "Total Area (sqm)",
                "Rate (AED)", "Total (AED)"
            };
            int totalCols = headers.Length;

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml(TABLE_HEADER);
                cell.Style.Alignment.Horizontal =
                    XLAlignmentHorizontalValues.Center;
            }

            int dataRow = 2;
            int allQty = 0;
            decimal allArea = 0m;
            decimal allValue = 0m;

            if (_snapshot.SpecGroups == null)
            {
                ws.Columns().AdjustToContents();
                return;
            }

            foreach (var g in _snapshot.SpecGroups)
            {
                string groupLabel =
                    !string.IsNullOrWhiteSpace(g.SelectedSpecName)
                        ? g.SelectedSpecName
                        : (!string.IsNullOrWhiteSpace(g.GroupName)
                            ? g.GroupName
                            : $"Spec Group {g.GroupNumber}");

                int groupQty = 0;
                decimal groupArea = 0m;
                decimal groupValue = 0m;

                if (g.ItemRows != null)
                {
                    foreach (var item in g.ItemRows)
                    {
                        decimal unitArea = Math.Max(
                            Math.Round((decimal)item.W1 * item.H1
                                / 1_000_000m, 3),
                            0.5m);
                        decimal totalArea = Math.Round(unitArea * item.Qty, 3);
                        decimal total = Math.Round(totalArea * item.Rate, 2);

                        ws.Cell(dataRow, 1).Value = g.GroupNumber;
                        ws.Cell(dataRow, 2).Value = groupLabel;
                        ws.Cell(dataRow, 3).Value = item.SerialNumber;
                        ws.Cell(dataRow, 4).Value = item.GlassRef ?? "";
                        ws.Cell(dataRow, 5).Value = item.W1;
                        ws.Cell(dataRow, 6).Value = item.H1;
                        ws.Cell(dataRow, 7).Value = item.W2;
                        ws.Cell(dataRow, 8).Value = item.H2;
                        ws.Cell(dataRow, 9).Value = item.Qty;
                        ws.Cell(dataRow, 10).Value = unitArea;
                        ws.Cell(dataRow, 11).Value = totalArea;
                        ws.Cell(dataRow, 12).Value = item.Rate;
                        ws.Cell(dataRow, 13).Value = total;

                        ws.Cell(dataRow, 10).Style
                            .NumberFormat.Format = "#,##0.000";
                        ws.Cell(dataRow, 11).Style
                            .NumberFormat.Format = "#,##0.000";
                        ws.Cell(dataRow, 12).Style
                            .NumberFormat.Format = "#,##0.00";
                        ws.Cell(dataRow, 13).Style
                            .NumberFormat.Format = "#,##0.00";

                        if (dataRow % 2 == 0)
                            for (int c = 1; c <= totalCols; c++)
                                ws.Cell(dataRow, c).Style.Fill
                                    .BackgroundColor =
                                    XLColor.FromHtml(ALT_ROW);

                        groupQty += item.Qty;
                        groupArea += totalArea;
                        groupValue += total;
                        dataRow++;
                    }
                }

                ws.Cell(dataRow, 2).Value = $"SPEC {g.GroupNumber} SUBTOTAL";
                ws.Cell(dataRow, 2).Style.Font.Bold = true;
                ws.Cell(dataRow, 2).Style.Font.FontColor =
                    XLColor.FromHtml(ACCENT);
                ws.Cell(dataRow, 9).Value = groupQty;
                ws.Cell(dataRow, 9).Style.Font.Bold = true;
                ws.Cell(dataRow, 11).Value = groupArea;
                ws.Cell(dataRow, 11).Style.Font.Bold = true;
                ws.Cell(dataRow, 11).Style.NumberFormat.Format = "#,##0.000";
                ws.Cell(dataRow, 13).Value = groupValue;
                ws.Cell(dataRow, 13).Style.Font.Bold = true;
                ws.Cell(dataRow, 13).Style.Font.FontColor =
                    XLColor.FromHtml(GREEN);
                ws.Cell(dataRow, 13).Style.NumberFormat.Format = "#,##0.00";

                for (int c = 1; c <= totalCols; c++)
                    ws.Cell(dataRow, c).Style.Fill.BackgroundColor =
                        XLColor.FromHtml(LIGHT_BG);
                dataRow++;

                allQty += groupQty;
                allArea += groupArea;
                allValue += groupValue;
            }

            ws.Cell(dataRow, 2).Value = "GRAND TOTAL";
            ws.Cell(dataRow, 2).Style.Font.Bold = true;
            ws.Cell(dataRow, 2).Style.Font.FontSize = 12;
            ws.Cell(dataRow, 9).Value = allQty;
            ws.Cell(dataRow, 9).Style.Font.Bold = true;
            ws.Cell(dataRow, 11).Value = allArea;
            ws.Cell(dataRow, 11).Style.Font.Bold = true;
            ws.Cell(dataRow, 11).Style.NumberFormat.Format = "#,##0.000";
            ws.Cell(dataRow, 13).Value = allValue;
            ws.Cell(dataRow, 13).Style.Font.Bold = true;
            ws.Cell(dataRow, 13).Style.Font.FontSize = 12;
            ws.Cell(dataRow, 13).Style.NumberFormat.Format = "#,##0.00";

            for (int c = 1; c <= totalCols; c++)
            {
                ws.Cell(dataRow, c).Style.Fill.BackgroundColor =
                    XLColor.FromHtml(ACCENT);
                ws.Cell(dataRow, c).Style.Font.FontColor = XLColor.White;
            }

            ws.Columns().AdjustToContents();
        }

        // ════════════════════════════════════════════════════════════════════
        //  SHEET 3 — Other Charges
        //  Chat 7 Bug 4: "Quantity" → "QTY/SQM" header
        // ════════════════════════════════════════════════════════════════════

        private void BuildChargesSheet(
            IXLWorksheet ws,
            List<(string Name, string Qty, string Rate, decimal Amount)> charges)
        {
            var headers = new[]
            {
                "Charge Name",
                "QTY/SQM",       // ← Bug 4 (Chat 7): was "Quantity"
                "Rate (AED)",
                "Amount (AED)"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Fill.BackgroundColor =
                    XLColor.FromHtml(TABLE_HEADER);
            }

            int row = 2;
            foreach (var (name, qty, rate, amount) in charges)
            {
                ws.Cell(row, 1).Value = name;
                ws.Cell(row, 2).Value = qty;
                ws.Cell(row, 3).Value = rate;
                ws.Cell(row, 4).Value = amount;
                ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";

                if (row % 2 == 0)
                    for (int c = 1; c <= 4; c++)
                        ws.Cell(row, c).Style.Fill.BackgroundColor =
                            XLColor.FromHtml(ALT_ROW);
                row++;
            }

            ws.Cell(row, 1).Value = "TOTAL OTHER CHARGES";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 4).Value = _entity.OtherChargesSubtotal;
            ws.Cell(row, 4).Style.Font.Bold = true;
            ws.Cell(row, 4).Style.Font.FontColor = XLColor.FromHtml(GREEN);
            ws.Cell(row, 4).Style.NumberFormat.Format = "#,##0.00";

            ws.Columns().AdjustToContents();
        }

        // ════════════════════════════════════════════════════════════════════
        //  SHEET 4 — Terms & Conditions (preserved exactly)
        // ════════════════════════════════════════════════════════════════════

        private void BuildTermsSheet(
            IXLWorksheet ws,
            List<TermClauseSnapshot> terms)
        {
            ws.Cell("A1").Value = "#";
            ws.Cell("B1").Value = "Title";
            ws.Cell("C1").Value = "Body";
            ws.Range("A1:C1").Style.Font.Bold = true;
            ws.Range("A1:C1").Style.Fill.BackgroundColor =
                XLColor.FromHtml(ACCENT);
            ws.Range("A1:C1").Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var clause in terms)
            {
                ws.Cell(row, 1).Value = clause.Order;
                ws.Cell(row, 2).Value = clause.Title ?? "";

                string body = clause.Body ?? "";
                if (clause.SubLines != null && clause.SubLines.Count > 0)
                    body += "\n" + string.Join("\n",
                        clause.SubLines.Select(l => $"• {l}"));

                ws.Cell(row, 3).Value = body;
                ws.Cell(row, 3).Style.Alignment.WrapText = true;
                row++;
            }

            ws.Column("A").Width = 5;
            ws.Column("B").Width = 25;
            ws.Column("C").Width = 60;
        }

        // ════════════════════════════════════════════════════════════════════
        //  HELPERS
        //  Bug 4b FIX: BuildOtherChargeLines now computes real qty + amount
        //              using same pattern as PDF layout
        // ════════════════════════════════════════════════════════════════════

        private List<(string Name, string Qty, string Rate, decimal Amount)>
            BuildOtherChargeLines()
        {
            var lines = new List<(string, string, string, decimal)>();

            if (_snapshot.OtherCharges == null) return lines;

            // Pre-load templates for name/unit lookup by TemplateKey
            var templatesByKey = PredefinedChargeTemplates.GetAll()
                .ToDictionary(t => t.Key, t => t);

            // Aggregate metrics from all spec groups (for source key lookup)
            var metrics = ComputeSpecMetrics(_snapshot.SpecGroups);

            // ── Predefined charges (Argon, Silicon, U-Insert, etc.) ──────────
            foreach (var c in _snapshot.OtherCharges.PredefinedCharges
    .Where(c => c.IsActive))
            {
                templatesByKey.TryGetValue(c.TemplateKey ?? "", out var template);
                string friendlyName = template?.ChargeName ?? c.TemplateKey ?? "";
                string unitLabel = template?.Unit.ToString().ToUpper() ?? "QTY";

                decimal metricQty;

                // ═══════════════ Bug 4c (Chat 7) — QUICK HACK ═══════════════
                // DELIVERY is per-invoice, not per-piece — always render 1 × Rate.
                // Proper fix (PER_ROUND source) planned for Chat 8.
                // ══════════════════════════════════════════════════════════════
                if (c.TemplateKey == "DELIVERY")
                {
                    metricQty = 1m;
                    unitLabel = "TRIP";
                }
                else if (c.TemplateKey == "POLISH")
                {
                    if (c.PolishSubType == "Custom")
                    {
                        metricQty = c.CustomPolishQty;
                        if (!string.IsNullOrWhiteSpace(c.CustomPolishDescription))
                            friendlyName = $"Polish — {c.CustomPolishDescription}";
                    }
                    else
                    {
                        metricQty = ComputePolishLm(_snapshot.SpecGroups, c.PolishSubType);
                        if (!string.IsNullOrWhiteSpace(c.PolishSubType))
                            friendlyName = $"Polish — {c.PolishSubType}";
                    }
                }
                else
                {
                    string sourceKey = !string.IsNullOrWhiteSpace(c.SelectedSourceKey)
                        ? c.SelectedSourceKey
                        : (template?.DefaultSourceKey ?? "");
                    metricQty = LookupMetric(metrics, sourceKey);
                }

                decimal amount = Math.Round(metricQty * c.Rate, 2);
                string qtyStr = metricQty > 0
                    ? $"{metricQty:0.###} {unitLabel}"
                    : "—";

                lines.Add((
                    friendlyName,
                    qtyStr,
                    $"AED {c.Rate:N2}",
                    amount));
            }

            // ── Wastage rows (color-aggregated) ──────────────────────────────
            foreach (var w in _snapshot.OtherCharges.Wastages
                .Where(w => w.IsActive))
            {
                decimal wastageQty = ComputeWastageQty(
                    _snapshot.SpecGroups, w.CheckedSourceKeys);
                decimal wastageAmount = Math.Round(wastageQty * w.Rate, 2);

                lines.Add((
                    $"Wastage — {w.ColorName}",
                    wastageQty > 0 ? $"{wastageQty:0.###} SQM" : "—",
                    $"AED {w.Rate:N2}",
                    wastageAmount));
            }

            // ── Custom charges (user manual entries) ─────────────────────────
            foreach (var c in _snapshot.OtherCharges.CustomCharges
                .Where(c => c.IsActive))
            {
                decimal amount = Math.Round(c.Qty * c.Rate, 2);
                string unitStr = string.IsNullOrWhiteSpace(c.Unit)
                    ? "QTY" : c.Unit.ToUpper();

                lines.Add((
                    c.ChargeName ?? "",
                    c.Qty > 0 ? $"{c.Qty:0.###} {unitStr}" : "—",
                    $"AED {c.Rate:N2}",
                    amount));
            }

            return lines;
        }

        private List<TermClauseSnapshot> GetActiveTerms()
        {
            if (_snapshot.TermClauses == null)
                return new List<TermClauseSnapshot>();

            return _snapshot.TermClauses
                .Where(c => c.IsActive)
                .OrderBy(c => c.Order)
                .ToList();
        }

        private static void AddSection(
            IXLWorksheet ws, ref int row, string title)
        {
            ws.Cell(row, 1).Value = title;
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 11;
            ws.Cell(row, 1).Style.Font.FontColor =
                XLColor.FromHtml(ACCENT);
            ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor =
                XLColor.FromHtml(LIGHT_BG);
            row++;
        }

        private static void AddField(
            IXLWorksheet ws, ref int row, string label, string value)
        {
            ws.Cell(row, 1).Value = label;
            ws.Cell(row, 1).Style.Font.FontColor =
                XLColor.FromHtml(MUTED);
            ws.Cell(row, 2).Value =
                string.IsNullOrWhiteSpace(value) ? "—" : value;
            ws.Cell(row, 2).Style.Font.Bold = true;
            row++;
        }

        // ════════════════════════════════════════════════════════════════════
        //  METRIC COMPUTATION HELPERS (Bug 4b — shared logic with PDF layout)
        //  Replicates OtherChargesViewModel + MetricDiscoveryHubViewModel
        // ════════════════════════════════════════════════════════════════════

        private static SpecMetrics ComputeSpecMetrics(List<SpecGroupSnapshot> specGroups)
        {
            var m = new SpecMetrics();
            if (specGroups == null) return m;

            foreach (var g in specGroups)
            {
                if (g.ItemRows == null) continue;

                bool isDoubleGlass = g.SelectedGlassType == "DGU"
                                     || g.SelectedGlassType == "LAM"
                                     || g.SelectedGlassType == "DGU+LAM";
                int factor = g.SelectedGlassType == "LAM" ? 4 : 2;

                foreach (var r in g.ItemRows)
                {
                    if (r.Qty <= 0) continue;

                    if (r.W1 > 0 && r.H1 > 0)
                    {
                        decimal sqm1 = Math.Max(
                            Math.Round((decimal)r.W1 * r.H1 / 1_000_000m, 3), 0.5m);
                        m.TotalSqm1 += Math.Round(sqm1 * r.Qty, 3);

                        m.TotalLm1 += Math.Round(
                            ((decimal)r.W1 + r.H1) / 1000m * factor * r.Qty, 3);
                    }

                    if (r.W2 > 0 && r.H2 > 0)
                    {
                        decimal sqm2 = Math.Max(
                            Math.Round((decimal)r.W2 * r.H2 / 1_000_000m, 3), 0.5m);
                        m.TotalSqm2 += Math.Round(sqm2 * r.Qty, 3);

                        m.TotalLm2 += Math.Round(
                            ((decimal)r.W2 + r.H2) / 1000m * factor * r.Qty, 3);
                    }

                    m.TotalPieces += r.Qty;

                    if (isDoubleGlass)
                    {
                        m.DoubleGlassPieces += r.Qty * 2;
                        if (r.W1 > 0 && r.H1 > 0)
                        {
                            decimal sqm1 = Math.Max(
                                Math.Round((decimal)r.W1 * r.H1 / 1_000_000m, 3), 0.5m);
                            m.DguCavityArea += Math.Round(sqm1 * r.Qty, 3);
                        }
                    }
                    else
                    {
                        m.DoubleGlassPieces += r.Qty;
                    }
                }
            }

            m.CombinedLm = Math.Round(m.TotalLm1 + m.TotalLm2, 3);
            m.CombinedSqm = Math.Round(m.TotalSqm1 + m.TotalSqm2, 3);
            return m;
        }

        private static decimal LookupMetric(SpecMetrics m, string sourceKey)
        {
            return sourceKey switch
            {
                "LM1" => m.TotalLm1,
                "LM2" => m.TotalLm2,
                "LM_COMBINED" => m.CombinedLm,
                "SQM1" => m.TotalSqm1,
                "SQM2" => m.TotalSqm2,
                "SQM_COMBINED" => m.CombinedSqm,
                "DGU_CAVITY" => m.DguCavityArea,
                "TOTAL_PIECES" => m.TotalPieces,
                "DOUBLE_GLASS" => m.DoubleGlassPieces,
                _ => 0m
            };
        }

        private static decimal ComputePolishLm(
            List<SpecGroupSnapshot> specGroups, string polishSubType)
        {
            if (specGroups == null || string.IsNullOrWhiteSpace(polishSubType))
                return 0m;

            decimal totalLm = 0m;

            foreach (var g in specGroups)
            {
                if (g.ItemRows == null) continue;

                // Bug 4b typo FIX: was "SelelectedGlassType" — now correct
                bool isDoubleGlass = g.SelectedGlassType == "DGU"
                                     || g.SelectedGlassType == "LAM"
                                     || g.SelectedGlassType == "DGU+LAM";

                foreach (var r in g.ItemRows)
                {
                    if (r.Qty <= 0) continue;

                    decimal lm = 0m;
                    if (r.W1 > 0 && r.H1 > 0)
                        lm += CalcPolishSingle(r.W1, r.H1, polishSubType);
                    if (isDoubleGlass && r.W2 > 0 && r.H2 > 0)
                        lm += CalcPolishSingle(r.W2, r.H2, polishSubType);

                    totalLm += lm * r.Qty;
                }
            }

            return Math.Round(totalLm, 3);
        }

        private static decimal CalcPolishSingle(int w, int h, string subType)
        {
            return subType switch
            {
                "AllSidePolish" => ((decimal)w + h) / 1000m * 2m,
                "AllSidePolishAsPerDwg" => ((decimal)w + h) / 1000m * 2m,
                "BothHeightSide" => (decimal)h / 1000m * 2m,
                "BothWidthSide" => (decimal)w / 1000m * 2m,
                _ => 0m
            };
        }

        private static decimal ComputeWastageQty(
            List<SpecGroupSnapshot> specGroups, List<string> checkedKeys)
        {
            if (specGroups == null || checkedKeys == null || checkedKeys.Count == 0)
                return 0m;

            decimal total = 0m;

            foreach (var key in checkedKeys)
            {
                if (string.IsNullOrWhiteSpace(key)) continue;

                var parts = key.Split('-', 2);
                if (parts.Length < 2) continue;
                if (!int.TryParse(parts[0], out int specNumber)) continue;

                var group = specGroups.FirstOrDefault(g => g.GroupNumber == specNumber);
                if (group?.ItemRows == null) continue;

                bool isOuter = parts[1].Contains("Outer", StringComparison.OrdinalIgnoreCase)
                            || parts[1].Contains("W1", StringComparison.OrdinalIgnoreCase);

                foreach (var r in group.ItemRows)
                {
                    if (r.Qty <= 0) continue;
                    int w = isOuter ? r.W1 : r.W2;
                    int h = isOuter ? r.H1 : r.H2;
                    if (w > 0 && h > 0)
                    {
                        decimal sqm = Math.Max(
                            Math.Round((decimal)w * h / 1_000_000m, 3), 0.5m);
                        total += Math.Round(sqm * r.Qty, 3);
                    }
                }
            }

            return Math.Round(total, 3);
        }

        // ════════════════════════════════════════════════════════════════════
        //  SpecMetrics DTO (Bug 4b — same as PDF layout)
        // ════════════════════════════════════════════════════════════════════

        private sealed class SpecMetrics
        {
            public decimal TotalLm1 { get; set; }
            public decimal TotalLm2 { get; set; }
            public decimal CombinedLm { get; set; }
            public decimal TotalSqm1 { get; set; }
            public decimal TotalSqm2 { get; set; }
            public decimal CombinedSqm { get; set; }
            public decimal DguCavityArea { get; set; }
            public int TotalPieces { get; set; }
            public int DoubleGlassPieces { get; set; }
        }
    }
}