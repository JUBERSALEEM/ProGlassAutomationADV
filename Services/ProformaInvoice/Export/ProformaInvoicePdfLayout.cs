using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.Export
{
    /// <summary>
    /// Phase 9.5b — IPdfLayout implementation for Proforma Invoice.
    /// Replaces ProformaInvoicePdfService (~800 lines) by delegating
    /// orchestration to PdfExportEngine while owning only the content
    /// rendering via RenderContent().
    ///
    /// 11-column spec table:
    ///   # | GLASS REF | W1 | H1 | W2 | H2 | QTY | AREA | TOTAL AREA | RATE | TOTAL
    ///
    /// Grand summary bar after all spec tables (Option A):
    ///   GRAND TOTAL: N Specs • QTY: X • AREA: X.XXX sqm    VALUE: AED X,XXX.XX
    ///
    /// STRANGLER PATTERN Round 1: old services still compile alongside this.
    ///
    /// CHAT 7 CHANGE (Bug 4):
    ///   RenderOtherCharges() — added column header row with "QTY/SQM"
    ///   WHY: Other Charges items can be Qty/Sqm/Lm — header reflects all units
    ///   NOT CHANGED: Spec table "QTY" column (genuine glass panel quantity)
    ///   NOT CHANGED: BuildRenderContext() — snapshot properties preserved exactly
    /// </summary>
    public class ProformaInvoicePdfLayout : IPdfLayout
    {
        // ── Brand colours (same as legacy ProformaInvoicePdfService) ────────
        private const string ACCENT = "#4F46E5";
        private const string DARK = "#0F172A";
        private const string MUTED = "#475569";
        private const string LINE = "#CBD5E1";
        private const string LIGHT_BG = "#F1F5F9";
        private const string TABLE_HEADER = "#1E293B";
        private const string GREEN = "#047857";
        private const string RED = "#B91C1C";

        private readonly SavedProformaInvoice _entity;
        private readonly ProformaInvoiceSnapshot _snapshot;
        private readonly PiRenderContext _ctx;

        public ProformaInvoicePdfLayout(
            SavedProformaInvoice entity,
            ProformaInvoiceSnapshot snapshot)
        {
            _entity = entity ?? throw new ArgumentNullException(nameof(entity));
            _snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            _ctx = BuildRenderContext(entity, snapshot);
        }

        // ── IDocumentLayout ──────────────────────────────────────────────────

        public string DocumentPrefix => "PI";
        public string DocumentId => _entity.ProformaId;
        public string DocumentTitle => "PROFORMA INVOICE";
        public string CompanyName => _entity.CustomerName;
        public string Reference => _entity.ProjectName;
        public DateTime DocumentDate => _entity.IssueDate;

        // ── IPdfLayout ───────────────────────────────────────────────────────

        public PageLayout PageLayout => PageLayout.A4Portrait;

        /// <summary>
        /// Letterhead uses snapshot company data — no App.Config fallback
        /// since AppConfig does not store company details.
        /// Snapshot is always populated from wizard Step 1.
        /// </summary>
        public LetterheadInfo? Letterhead => new LetterheadInfo
        {
            CompanyName = _snapshot.Company?.CompanyName ?? "",
            CompanyLocation = _snapshot.Company?.CompanyLocation ?? "",
            CompanyTrn = _snapshot.Company?.CompanyTrn ?? "",
            DocumentTitle = DocumentTitle,
            DocumentId = DocumentId
        };

        public void RenderContent(ColumnDescriptor content)
        {
            // ── PI header badge ──────────────────────────────────────────
            content.Item().Element(c => RenderPiBadgeRow(c));

            // ── Horizontal rule ──────────────────────────────────────────
            content.Item().PaddingTop(6).PaddingBottom(6)
                .LineHorizontal(1).LineColor(LINE);

            // ── Client + Project two-column block ────────────────────────
            content.Item().Row(row =>
            {
                row.RelativeItem().Element(c => RenderClientBlock(c));
                row.ConstantItem(16);
                row.RelativeItem().Element(c => RenderProjectBlock(c));
            });

            content.Item().PaddingTop(8).PaddingBottom(8)
                .LineHorizontal(1).LineColor(LINE);

            // ── Spec group tables ────────────────────────────────────────
            foreach (var group in _ctx.SpecGroups)
                content.Item().Element(c => RenderSpecTable(c, group));

            // ── Grand spec summary bar ───────────────────────────────────
            if (_ctx.SpecGroups.Count > 0)
                content.Item().Element(c => RenderGrandSpecSummary(c));

            // ── Other charges ────────────────────────────────────────────
            content.Item().Element(c => RenderOtherCharges(c));

            // ── Financial summary ────────────────────────────────────────
            content.Item().Element(c => RenderFinancialSummary(c));

            // ── Terms & Conditions ───────────────────────────────────────
            if (_ctx.TermClauses.Any())
                content.Item().Element(c => RenderTermsAndConditions(c));

            // ── Footer disclaimer + terms text ───────────────────────────
            content.Item().PaddingTop(10)
                .BorderTop(1).BorderColor(LINE).PaddingTop(6)
                .Column(footer =>
                {
                    footer.Item()
                        .Text(_ctx.FooterDisclaimer)
                        .FontSize(8).Italic().FontColor(MUTED);

                    if (!string.IsNullOrWhiteSpace(_ctx.TermsText))
                        footer.Item()
                            .Text($"Terms: {_ctx.TermsText}")
                            .FontSize(8).Italic().FontColor(MUTED);
                });

            // ── Signature blocks ─────────────────────────────────────────
            content.Item().Element(c => RenderSignatureBlocks(c));
        }

        // ════════════════════════════════════════════════════════════════════
        //  SECTION RENDERERS
        // ════════════════════════════════════════════════════════════════════

        private void RenderPiBadgeRow(IContainer container)
        {
            container.AlignRight().Width(200)
                .Border(1).BorderColor(LINE)
                .Background(LIGHT_BG)
                .Padding(10)
                .Column(badge =>
                {
                    badge.Item().AlignCenter()
                        .Text("PROFORMA INVOICE")
                        .FontSize(12).Bold().FontColor(DARK);

                    badge.Item().PaddingTop(6)
                        .Text($"Ref: {_ctx.ProformaId}")
                        .FontSize(9).SemiBold();

                    badge.Item()
                        .Text($"Date: {_ctx.IssueDate}")
                        .FontSize(9).SemiBold();

                    if (!string.IsNullOrWhiteSpace(_ctx.ProformaStatus))
                        badge.Item()
                            .Text($"Status: {_ctx.ProformaStatus}")
                            .FontSize(9).FontColor(MUTED);
                });
        }

        private void RenderClientBlock(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("CLIENT / BILL TO:")
                    .FontSize(10).Bold().FontColor(DARK);

                AddLabelValue(col, "Customer Name:", _ctx.CustomerName);
                AddLabelValue(col, "TRN/VAT Number:", _ctx.CustomerTrn);
                AddLabelValue(col, "Attention Name:", _ctx.AttentionName);
                AddLabelValue(col, "Contact Details:", _ctx.ContactPhone);
                AddLabelValue(col, "Billing Address:", _ctx.BillingAddress);
                AddLabelValue(col, "Client Reference:", _ctx.CustomerReference);
            });
        }

        private void RenderProjectBlock(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().Text("PROJECT ESTIMATE METRIC:")
                    .FontSize(10).Bold().FontColor(DARK);

                AddLabelValue(col, "Project Designation:", _ctx.ProjectName);
                AddLabelValue(col, "Project Code / ID:", _ctx.ProjectContractNo);
                AddLabelValue(col, "Site Delivery:", _ctx.SiteDelivery);
                AddLabelValue(col, "LPO / PO Reference:", _ctx.LpoNumber);
                AddLabelValue(col, "Assigned Salesman:", _ctx.SalesRepresentative);
                AddLabelValue(col, "Valid Until:",
                    $"{_ctx.ValidUntil} • Status: {_ctx.ProformaStatus}");
            });
        }

        // ────────────────────────────────────────────────────────────────────
        //  11-column spec table — PRESERVED EXACTLY
        //  ⚠️ "QTY" header here = genuine glass panel quantity — DO NOT change
        //  # | GLASS REF | W1 | H1 | W2 | H2 | QTY | AREA | TOTAL AREA | RATE | TOTAL
        // ────────────────────────────────────────────────────────────────────
        private void RenderSpecTable(IContainer container, PiSpecGroup group)
        {
            container.PaddingBottom(10).Column(col =>
            {
                string specLabel = !string.IsNullOrWhiteSpace(group.GroupName)
                    ? group.GroupName
                    : $"Spec Group {group.GroupNumber}";

                col.Item()
                    .Background(ACCENT)
                    .PaddingHorizontal(8).PaddingVertical(5)
                    .Text($"SPECIFICATION {group.GroupNumber}: {specLabel}")
                    .FontSize(10).Bold().FontColor(Colors.White);

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(24);        // #
                        c.RelativeColumn(1.5f);      // Glass Ref
                        c.ConstantColumn(32);        // W1
                        c.ConstantColumn(32);        // H1
                        c.ConstantColumn(32);        // W2
                        c.ConstantColumn(32);        // H2
                        c.ConstantColumn(32);        // Qty ← glass panel qty, not charges qty
                        c.RelativeColumn(0.75f);     // Area (unit)
                        c.RelativeColumn(0.80f);     // Total Area
                        c.RelativeColumn(0.70f);     // Rate
                        c.RelativeColumn(0.85f);     // Total
                    });

                    table.Header(header =>
                    {
                        // ⚠️ "QTY" = glass panel quantity — preserved, not changed to QTY/SQM
                        var headers = new[]
                        {
                            "#", "GLASS REF", "W1", "H1", "W2", "H2",
                            "QTY", "AREA", "TOTAL AREA", "RATE", "TOTAL"
                        };
                        foreach (var h in headers)
                        {
                            header.Cell()
                                .Background(TABLE_HEADER)
                                .PaddingHorizontal(4).PaddingVertical(4)
                                .Text(h).FontSize(8).Bold()
                                .FontColor(Colors.White);
                        }
                    });

                    foreach (var row in group.Rows)
                    {
                        AddCell(table, row.Serial.ToString());
                        AddCell(table, row.GlassRef);
                        AddCellRight(table, row.W1.ToString());
                        AddCellRight(table, row.H1.ToString());
                        AddCellRight(table, row.W2.ToString());
                        AddCellRight(table, row.H2.ToString());
                        AddCellCenter(table, row.Qty.ToString());
                        AddCellRight(table, row.UnitArea.ToString("0.000"));
                        AddCellRight(table, row.TotalArea.ToString("0.000"));
                        AddCellRight(table, row.Rate.ToString("N2"));
                        AddCellRight(table, row.Total.ToString("N2"), bold: true);
                    }
                });

                col.Item()
                    .Background(LIGHT_BG)
                    .BorderBottom(1).BorderColor(LINE)
                    .PaddingHorizontal(6).PaddingVertical(5)
                    .Row(r =>
                    {
                        r.RelativeItem()
                            .Text($"SPEC SUB-TOTALS:  QTY: {group.TotalQty}  •  " +
                                  $"AREA: {group.TotalArea:0.000} sqm")
                            .FontSize(9).Bold().FontColor(ACCENT);

                        r.AutoItem()
                            .Text($"VALUE: AED {group.TotalValue:N2}")
                            .FontSize(10).Bold().FontColor(GREEN);
                    });
            });
        }

        private void RenderGrandSpecSummary(IContainer container)
        {
            int specCount = _ctx.SpecGroups.Count;
            int totalQty = _ctx.SpecGroups.Sum(g => g.TotalQty);
            decimal totalArea = _ctx.SpecGroups.Sum(g => g.TotalArea);
            decimal totalValue = _ctx.SpecGroups.Sum(g => g.TotalValue);

            container.PaddingBottom(6)
                .Background(ACCENT)
                .PaddingHorizontal(8).PaddingVertical(6)
                .Row(r =>
                {
                    r.RelativeItem()
                        .Text($"GRAND TOTAL:  {specCount} Spec{(specCount != 1 ? "s" : "")}  •  " +
                              $"QTY: {totalQty}  •  AREA: {totalArea:0.000} sqm")
                        .FontSize(10).Bold().FontColor(Colors.White);

                    r.AutoItem()
                        .Text($"VALUE: AED {totalValue:N2}")
                        .FontSize(11).Bold().FontColor(Colors.White);
                });
        }

        // ════════════════════════════════════════════════════════════════════
        //  OTHER CHARGES — BUG 4 FIX
        //
        //  ONLY CHANGE in this file vs original:
        //    + Added TABLE_HEADER column header row before the charge rows
        //    + "QTY/SQM" label — accommodates Qty, Sqm, and Lm charge units
        //
        //  Data rows (Name/QtyLabel/RateLabel/Amount) = PRESERVED EXACTLY
        //  Spec table "QTY" column = NOT TOUCHED
        // ════════════════════════════════════════════════════════════════════
        private void RenderOtherCharges(IContainer container)
        {
            if (!_ctx.OtherCharges.Any()) return;

            container.PaddingTop(4).Column(col =>
            {
                // Section title bar (preserved)
                col.Item()
                    .Background(ACCENT)
                    .PaddingHorizontal(8).PaddingVertical(5)
                    .Text("SUPPLEMENTARY / FABRICATION OTHER CHARGES")
                    .FontSize(10).Bold().FontColor(Colors.White);

                // ── Bug 4: Column header row ─────────────────────────────────
                // Added in Chat 7. QTY/SQM instead of QTY because Other Charges
                // can be Qty (Holes), Sqm (Sandblasting), or Lm (Edge Grinding).
                // The data rows below are preserved exactly — only header added.
                col.Item()
                    .Background(TABLE_HEADER)
                    .PaddingHorizontal(8).PaddingVertical(4)
                    .Row(hdr =>
                    {
                        hdr.RelativeItem(2f)
                            .Text("DESCRIPTION")
                            .FontSize(8).Bold().FontColor(Colors.White);

                        hdr.RelativeItem(1.2f)
                            .Text("QTY/SQM")
                            .FontSize(8).Bold().FontColor(Colors.White);

                        hdr.RelativeItem(1f)
                            .Text("RATE")
                            .FontSize(8).Bold().FontColor(Colors.White);

                        hdr.RelativeItem(1f).AlignRight()
                            .Text("AMOUNT")
                            .FontSize(8).Bold().FontColor(Colors.White);
                    });

                // Charge data rows — PRESERVED EXACTLY from original
                col.Item()
                    .Background(LIGHT_BG)
                    .Border(1).BorderColor(LINE)
                    .Padding(8)
                    .Column(charges =>
                    {
                        foreach (var charge in _ctx.OtherCharges)
                        {
                            charges.Item().PaddingVertical(2).Row(r =>
                            {
                                r.RelativeItem(2)
                                    .Text(charge.Name)
                                    .FontSize(9).SemiBold();

                                r.RelativeItem(1.2f)
                                    .Text(charge.QtyLabel)
                                    .FontSize(9).FontColor(MUTED);

                                r.RelativeItem(1)
                                    .Text(charge.RateLabel)
                                    .FontSize(9).FontColor(MUTED);

                                r.RelativeItem(1).AlignRight()
                                    .Text($"AED {charge.Amount:N2}")
                                    .FontSize(10).Bold();
                            });
                        }
                    });
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  FINANCIAL SUMMARY — PRESERVED EXACTLY
        // ════════════════════════════════════════════════════════════════════
        private void RenderFinancialSummary(IContainer container)
        {
            container
                .PaddingTop(10)
                .BorderTop(1).BorderColor(LINE)
                .PaddingTop(10)
                .AlignRight().Width(320)
                .Column(col =>
                {
                    AddSummaryRow(col,
                        "Glass Materials Total:",
                        $"{_ctx.GlassMaterialsTotal:N2} AED");

                    AddSummaryRow(col,
                        "Other Processing/Delivery Total:",
                        $"{_ctx.OtherChargesTotal:N2} AED");

                    col.Item().PaddingVertical(3)
                        .LineHorizontal(1).LineColor(LINE);

                    AddSummaryRow(col,
                        "Estimated Net Subtotal:",
                        $"{_ctx.NetSubtotal:N2} AED",
                        bold: true, size: 10);

                    if (_ctx.DiscountAmount > 0)
                        AddSummaryRow(col,
                            "Discount:",
                            $"- {_ctx.DiscountAmount:N2} AED",
                            color: RED);

                    if (_ctx.IsVatApplicable)
                        AddSummaryRow(col,
                            $"Standard UAE VAT ({_ctx.VatPercent:0.##}%):",
                            $"{_ctx.VatAmount:N2} AED");

                    col.Item()
                        .PaddingTop(6)
                        .Background(ACCENT)
                        .PaddingHorizontal(12).PaddingVertical(8)
                        .Row(r =>
                        {
                            r.RelativeItem()
                                .Text("TOTAL EST VALUE (VAT INC):")
                                .FontSize(12).Bold().FontColor(Colors.White);

                            r.AutoItem()
                                .Text($"{_ctx.GrandTotal:N2} AED")
                                .FontSize(13).Bold().FontColor(Colors.White);
                        });
                });
        }

        // ════════════════════════════════════════════════════════════════════
        //  TERMS & CONDITIONS — PRESERVED EXACTLY
        // ════════════════════════════════════════════════════════════════════
        private void RenderTermsAndConditions(IContainer container)
        {
            container.PaddingTop(14).Column(col =>
            {
                col.Item()
                    .Background(ACCENT)
                    .PaddingHorizontal(8).PaddingVertical(5)
                    .Text("TERMS & CONDITIONS")
                    .FontSize(10).Bold().FontColor(Colors.White);

                col.Item()
                    .Background(LIGHT_BG)
                    .Border(1).BorderColor(LINE)
                    .Padding(10)
                    .Column(terms =>
                    {
                        foreach (var clause in _ctx.TermClauses)
                        {
                            terms.Item().PaddingBottom(6).Column(clauseCol =>
                            {
                                clauseCol.Item()
                                    .Text($"{clause.Order}. {clause.Title?.ToUpper()}")
                                    .FontSize(9).Bold().FontColor(DARK);

                                if (!string.IsNullOrWhiteSpace(clause.Body))
                                    clauseCol.Item().PaddingLeft(12)
                                        .Text(clause.Body)
                                        .FontSize(9).FontColor(DARK);

                                if (clause.SubLines != null)
                                {
                                    foreach (var line in clause.SubLines)
                                    {
                                        if (string.IsNullOrWhiteSpace(line)) continue;

                                        clauseCol.Item().PaddingLeft(24).Row(r =>
                                        {
                                            r.AutoItem()
                                                .Text("• ")
                                                .FontSize(9).Bold().FontColor(ACCENT);

                                            r.RelativeItem()
                                                .Text(line)
                                                .FontSize(9).FontColor(DARK);
                                        });
                                    }
                                }
                            });
                        }
                    });
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  SIGNATURE BLOCKS — PRESERVED EXACTLY
        // ════════════════════════════════════════════════════════════════════
        private void RenderSignatureBlocks(IContainer container)
        {
            container.PaddingTop(12).Row(row =>
            {
                row.RelativeItem()
                    .Border(1).BorderColor(LINE)
                    .Padding(10)
                    .Column(col =>
                    {
                        col.Item()
                            .Text("PREPARED BY (ERP AUTHENTICATED)")
                            .FontSize(9).Bold().FontColor(ACCENT);

                        col.Item().PaddingTop(4)
                            .Text(string.IsNullOrWhiteSpace(_ctx.PreparedByName)
                                ? "—" : _ctx.PreparedByName)
                            .FontSize(10).SemiBold().FontColor(DARK);

                        if (!string.IsNullOrWhiteSpace(_ctx.PreparedByDept))
                            col.Item().PaddingTop(2)
                                .Text(_ctx.PreparedByDept)
                                .FontSize(9).FontColor(MUTED);

                        col.Item().PaddingTop(3).Row(r =>
                        {
                            r.AutoItem()
                                .Text("Status: ")
                                .FontSize(9).FontColor(MUTED);

                            r.AutoItem()
                                .Text(string.IsNullOrWhiteSpace(_ctx.PreparedByStatus)
                                    ? "Draft" : _ctx.PreparedByStatus)
                                .FontSize(9).Bold().FontColor(GREEN);
                        });
                    });

                row.ConstantItem(16);

                row.RelativeItem()
                    .Border(1).BorderColor(LINE)
                    .Padding(10)
                    .Column(col =>
                    {
                        col.Item()
                            .Text("CLIENT APPROVAL SIGN & MOB NUMBER")
                            .FontSize(9).Bold().FontColor(ACCENT);

                        col.Item().PaddingTop(4).Row(r =>
                        {
                            r.AutoItem()
                                .Text("Approval Sign:")
                                .FontSize(9).FontColor(MUTED);

                            r.RelativeItem().PaddingLeft(6)
                                .BorderBottom(1).BorderColor(LINE)
                                .Height(1);
                        });

                        col.Item().PaddingTop(4).Row(r =>
                        {
                            r.AutoItem()
                                .Text("Mobile Number:")
                                .FontSize(9).FontColor(MUTED);

                            r.RelativeItem().PaddingLeft(6)
                                .BorderBottom(1).BorderColor(LINE)
                                .Height(1);
                        });

                        col.Item().PaddingTop(4)
                            .Text("Authorized Name & Stamp")
                            .FontSize(9).FontColor(MUTED);
                    });
            });
        }

        // ════════════════════════════════════════════════════════════════════
        //  SNAPSHOT → RENDER CONTEXT BUILDER — PRESERVED EXACTLY
        //  No changes here — snapshot property names kept as original
        // ════════════════════════════════════════════════════════════════════
        private static PiRenderContext BuildRenderContext(
            SavedProformaInvoice entity,
            ProformaInvoiceSnapshot snapshot)
        {
            var ctx = new PiRenderContext
            {
                CompanyName = snapshot.Company?.CompanyName ?? "",
                CompanyTrn = snapshot.Company?.CompanyTrn ?? "",
                CompanyLocation = snapshot.Company?.CompanyLocation ?? "",
                ProformaId = snapshot.Company?.ProformaId ?? entity.ProformaId,
                IssueDate = snapshot.Company?.IssueDate.ToString("yyyy-MM-dd") ?? "",
                ValidUntil = snapshot.Company?.ValidUntil.ToString("yyyy-MM-dd") ?? "",
                ProformaStatus = snapshot.Company?.ProformaStatus ?? entity.Status,

                ProjectName = snapshot.Project?.ProjectIdentifierName ?? entity.ProjectName,
                ProjectContractNo = snapshot.Project?.ProjectContractNo ?? "",
                SiteDelivery = snapshot.Project?.SiteDeliveryLocation ?? "",
                LpoNumber = snapshot.Project?.LpoNumber ?? "",

                CustomerName = snapshot.Client?.CustomerName ?? entity.CustomerName,
                CustomerTrn = snapshot.Client?.CustomerTrn ?? "",
                AttentionName = snapshot.Client?.AttentionExecutiveName ?? "",
                ContactPhone = snapshot.Client?.ClientContactPhone ?? "",
                BillingAddress = snapshot.Client?.BillingAddress ?? "",
                CustomerReference = snapshot.Client?.CustomerReferenceCode ?? "",
                SalesRepresentative = snapshot.Client?.SalesRepresentative ?? "",

                GlassMaterialsTotal = entity.GlassMaterialsSubtotal,
                OtherChargesTotal = entity.OtherChargesSubtotal,
                NetSubtotal = entity.GlassMaterialsSubtotal
                                        + entity.OtherChargesSubtotal
                                        - entity.DiscountAmount,
                DiscountAmount = entity.DiscountAmount,
                IsVatApplicable = entity.VatAmount > 0,
                VatAmount = entity.VatAmount,
                GrandTotal = entity.GrandTotalVatInclusive,
                VatPercent = snapshot.Summary?.VatPercent ?? 5m,

                PreparedByName = snapshot.PreparedBy?.PreparedByName ?? "",
                PreparedByDept = snapshot.PreparedBy?.PreparedByDept ?? "",
                PreparedByStatus = snapshot.PreparedBy?.PreparedByStatus ?? "",
                TermsText = snapshot.PreparedBy?.TermsText ?? "",
                FooterDisclaimer = snapshot.PreparedBy?.FooterDisclaimer
                                        ?? "Confidential Document generated automatically by ProGlass Automation ERP platform."
            };

            // ── Spec groups (preserved exactly) ─────────────────────────────
            if (snapshot.SpecGroups != null)
            {
                foreach (var g in snapshot.SpecGroups)
                {
                    var group = new PiSpecGroup
                    {
                        GroupNumber = g.GroupNumber,
                        GroupName = !string.IsNullOrWhiteSpace(g.SelectedSpecName)
                                          ? g.SelectedSpecName
                                          : (!string.IsNullOrWhiteSpace(g.GroupName)
                                              ? g.GroupName
                                              : $"Spec Group {g.GroupNumber}")
                    };

                    if (g.ItemRows != null)
                    {
                        foreach (var r in g.ItemRows)
                        {
                            decimal unitArea = Math.Max(
                                Math.Round((decimal)r.W1 * r.H1 / 1_000_000m, 3),
                                0.5m);
                            decimal totalArea = Math.Round(unitArea * r.Qty, 3);
                            decimal total = Math.Round(totalArea * r.Rate, 2);

                            group.Rows.Add(new PiGlassRow
                            {
                                Serial = r.SerialNumber,
                                GlassRef = r.GlassRef ?? "",
                                W1 = r.W1,
                                H1 = r.H1,
                                W2 = r.W2,
                                H2 = r.H2,
                                Qty = r.Qty,
                                UnitArea = unitArea,
                                TotalArea = totalArea,
                                Rate = r.Rate,
                                Total = total
                            });

                            group.TotalQty += r.Qty;
                            group.TotalArea += totalArea;
                            group.TotalValue += total;
                        }
                    }

                    ctx.SpecGroups.Add(group);
                }
            }

            // ── Other charges (preserved exactly — snapshot props unchanged) ──
            if (snapshot.OtherCharges != null)
            {
                foreach (var c in snapshot.OtherCharges.PredefinedCharges
                    .Where(c => c.IsActive))
                {
                    ctx.OtherCharges.Add(new PiChargeLine
                    {
                        Name = c.TemplateKey ?? "",
                        QtyLabel = "",
                        RateLabel = $"Rate: AED {c.Rate:N2}",
                        Amount = 0m
                    });
                }

                foreach (var w in snapshot.OtherCharges.Wastages
                    .Where(w => w.IsActive))
                {
                    ctx.OtherCharges.Add(new PiChargeLine
                    {
                        Name = $"Wastage — {w.ColorName}",
                        QtyLabel = "",
                        RateLabel = $"Rate: AED {w.Rate:N2}",
                        Amount = 0m
                    });
                }

                foreach (var c in snapshot.OtherCharges.CustomCharges
                    .Where(c => c.IsActive))
                {
                    ctx.OtherCharges.Add(new PiChargeLine
                    {
                        Name = c.ChargeName ?? "",
                        QtyLabel = $"Qty: {c.Qty:0.##} {c.Unit}",
                        RateLabel = $"Rate: AED {c.Rate:N2}",
                        Amount = Math.Round(c.Qty * c.Rate, 2)
                    });
                }
            }

            // ── Terms & conditions (preserved exactly) ───────────────────────
            if (snapshot.TermClauses != null)
            {
                foreach (var c in snapshot.TermClauses
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Order))
                {
                    var clause = new PiTermClause
                    {
                        Order = c.Order,
                        Title = c.Title ?? "",
                        Body = c.Body ?? ""
                    };

                    if (c.SubLines != null)
                        foreach (var l in c.SubLines.Where(l =>
                            !string.IsNullOrWhiteSpace(l)))
                            clause.SubLines.Add(l);

                    ctx.TermClauses.Add(clause);
                }
            }

            return ctx;
        }

        // ════════════════════════════════════════════════════════════════════
        //  TABLE CELL HELPERS — PRESERVED EXACTLY
        // ════════════════════════════════════════════════════════════════════

        private static void AddLabelValue(
            ColumnDescriptor col, string label, string value)
        {
            col.Item().PaddingTop(1).Row(r =>
            {
                r.AutoItem()
                    .Text($"{label} ")
                    .FontSize(9).FontColor(MUTED);

                r.RelativeItem()
                    .Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                    .FontSize(9).SemiBold().FontColor(DARK);
            });
        }

        private static void AddSummaryRow(
            ColumnDescriptor col,
            string label,
            string value,
            bool bold = false,
            int size = 9,
            string color = null)
        {
            col.Item().PaddingBottom(3).Row(r =>
            {
                var labelText = r.RelativeItem()
                    .Text(label).FontSize(size);
                if (bold) labelText.Bold();
                if (color != null) labelText.FontColor(color);

                var valueText = r.AutoItem()
                    .Text(value).FontSize(size);
                if (bold) valueText.Bold();
                if (color != null) valueText.FontColor(color);
            });
        }

        private static void AddCell(TableDescriptor table, string text)
        {
            table.Cell()
                .BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(4).PaddingVertical(3)
                .Text(text).FontSize(8);
        }

        private static void AddCellCenter(TableDescriptor table, string text)
        {
            table.Cell()
                .BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(4).PaddingVertical(3)
                .AlignCenter()
                .Text(text).FontSize(8);
        }

        private static void AddCellRight(
            TableDescriptor table, string text, bool bold = false)
        {
            var cell = table.Cell()
                .BorderBottom(1).BorderColor(LINE)
                .PaddingHorizontal(4).PaddingVertical(3)
                .AlignRight()
                .Text(text).FontSize(8);

            if (bold) cell.Bold();
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE RENDER CONTEXT DTOs — PRESERVED EXACTLY
        // ════════════════════════════════════════════════════════════════════

        private sealed class PiRenderContext
        {
            public string CompanyName { get; set; } = "";
            public string CompanyTrn { get; set; } = "";
            public string CompanyLocation { get; set; } = "";
            public string ProformaId { get; set; } = "";
            public string IssueDate { get; set; } = "";
            public string ValidUntil { get; set; } = "";
            public string ProformaStatus { get; set; } = "";
            public string ProjectName { get; set; } = "";
            public string ProjectContractNo { get; set; } = "";
            public string SiteDelivery { get; set; } = "";
            public string LpoNumber { get; set; } = "";
            public string CustomerName { get; set; } = "";
            public string CustomerTrn { get; set; } = "";
            public string AttentionName { get; set; } = "";
            public string ContactPhone { get; set; } = "";
            public string BillingAddress { get; set; } = "";
            public string CustomerReference { get; set; } = "";
            public string SalesRepresentative { get; set; } = "";
            public decimal GlassMaterialsTotal { get; set; }
            public decimal OtherChargesTotal { get; set; }
            public decimal NetSubtotal { get; set; }
            public decimal DiscountAmount { get; set; }
            public bool IsVatApplicable { get; set; }
            public decimal VatPercent { get; set; } = 5m;
            public decimal VatAmount { get; set; }
            public decimal GrandTotal { get; set; }
            public string PreparedByName { get; set; } = "";
            public string PreparedByDept { get; set; } = "";
            public string PreparedByStatus { get; set; } = "";
            public string TermsText { get; set; } = "";
            public string FooterDisclaimer { get; set; } = "";

            public List<PiSpecGroup> SpecGroups { get; } = new();
            public List<PiChargeLine> OtherCharges { get; } = new();
            public List<PiTermClause> TermClauses { get; } = new();
        }

        private sealed class PiSpecGroup
        {
            public int GroupNumber { get; set; }
            public string GroupName { get; set; } = "";
            public List<PiGlassRow> Rows { get; } = new();
            public int TotalQty { get; set; }
            public decimal TotalArea { get; set; }
            public decimal TotalValue { get; set; }
        }

        private sealed class PiGlassRow
        {
            public int Serial { get; set; }
            public string GlassRef { get; set; } = "";
            public int W1 { get; set; }
            public int H1 { get; set; }
            public int W2 { get; set; }
            public int H2 { get; set; }
            public int Qty { get; set; }
            public decimal UnitArea { get; set; }
            public decimal TotalArea { get; set; }
            public decimal Rate { get; set; }
            public decimal Total { get; set; }
        }

        private sealed class PiChargeLine
        {
            public string Name { get; set; } = "";
            public string QtyLabel { get; set; } = "";
            public string RateLabel { get; set; } = "";
            public decimal Amount { get; set; }
        }

        private sealed class PiTermClause
        {
            public int Order { get; set; }
            public string Title { get; set; } = "";
            public string Body { get; set; } = "";
            public List<string> SubLines { get; } = new();
        }
    }
}