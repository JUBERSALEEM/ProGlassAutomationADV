using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.ProformaInvoice.Step6OtherCharges;
using ProGlassAutomation.Services.Export.Core;
using ProGlassAutomation.Services.Export.FileSystem;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using ProGlassAutomation.Utilities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;

using System.Globalization;
namespace ProGlassAutomation.Services.ProformaInvoice.Export
{
    /// <summary>
    /// IPdfLayout implementation for Proforma Invoice.
    ///
    /// BUG-21 [CRITICAL]: Removed duplicated ComputeSpecMetrics,
    ///         ComputePolishLm, CalcPolishSingle, ComputeWastageQty.
    ///         Now delegates to ProformaChargeMetricsCalculator.
    /// BUG-22: Uses SqmCalculator.CalculatePanel() (single source of truth).
    /// BUG-23: DELIVERY hardcoded to 1 TRIP — documented technical debt,
    ///         proper fix in Chat 8 (PER_ROUND source).
    /// BUG-24 [HIGH]: IsVatApplicable sourced from snapshot.Summary,
    ///         not derived from VatAmount > 0 (broke on FOC invoices).
    /// BUG-26: FooterDisclaimer taken from snapshot only.
    /// BUG-29: File size reduced via shared calculator.
    /// </summary>
    public class ProformaInvoicePdfLayout : IPdfLayout
    {
        private static string ACCENT => DocumentStyle.ColorAccent;
        private static string DARK => DocumentStyle.ColorText;
        private static string MUTED => DocumentStyle.ColorMuted;
        private static string LINE => DocumentStyle.ColorBorder;
        private static string LIGHT_BG => DocumentStyle.ColorSurface;
        private static string GREEN => DocumentStyle.ColorSuccess;
        private static string RED => DocumentStyle.ColorDanger;

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
        public string Reference => ExportFileNamer.FirstFilled(
            _entity.ProjectName,
            _snapshot.Project?.ProjectIdentifierName,
            _snapshot.Project?.ProjectReference);
        public DateTime DocumentDate => _entity.IssueDate;

        // ── IPdfLayout ───────────────────────────────────────────────────────

        public PageLayout PageLayout => PageLayout.A4Portrait;

        public LetterheadInfo? Letterhead
        {
            get
            {
                var info = new LetterheadInfo
                {
                    CompanyName = _snapshot.Company?.CompanyName ?? "",
                    CompanyLocation = _snapshot.Company?.CompanyLocation ?? "",
                    CompanyTrn = _snapshot.Company?.CompanyTrn ?? "",
                    DocumentTitle = DocumentTitle,
                    DocumentId = DocumentId,
                    SubtitleLine = _ctx.ProformaStatus,
                    LogoPath = _snapshot.Company?.LogoPath ?? "",
                    LogoLayout = _snapshot.Company?.LogoLayout
                        ?? ProGlassAutomation.Models.MasterData.CompanyLogoLayout.None,
                    LogoWidthMm = _snapshot.Company?.LogoWidthMm ?? 22,
                    LogoHeightMm = _snapshot.Company?.LogoHeightMm ?? 26
                };
                info.Meta.Add(("Date", _ctx.IssueDate));
                info.Meta.Add(("Valid until", _ctx.ValidUntil));
                info.Meta.Add(("Status", _ctx.ProformaStatus));
                return info;
            }
        }

        public void RenderContent(ColumnDescriptor content)
        {
            content.Item().Row(row =>
            {
                row.RelativeItem().Element(c => RenderClientBlock(c));
                row.ConstantItem(12);
                row.RelativeItem().Element(c => RenderProjectBlock(c));
            });

            foreach (var group in _ctx.SpecGroups)
                content.Item().Element(c => RenderSpecTable(c, group));

            if (_ctx.SpecGroups.Count > 0)
                content.Item().Element(c => RenderGrandSpecSummary(c));

            if (_ctx.OtherCharges.Any())
                content.Item().Element(c => RenderOtherCharges(c));

            content.Item().Element(c => RenderFinancialSummary(c));

            if (_ctx.TermClauses.Any())
                content.Item().Element(c => RenderTermsAndConditions(c));

            if (!string.IsNullOrWhiteSpace(_ctx.FooterDisclaimer)
                || !string.IsNullOrWhiteSpace(_ctx.TermsText))
            {
                content.Item().Element(PdfChrome.Hairline);
                content.Item().Column(footer =>
                {
                    if (!string.IsNullOrWhiteSpace(_ctx.FooterDisclaimer))
                        footer.Item()
                            .Text(_ctx.FooterDisclaimer)
                            .FontSize(8).Italic().FontColor(MUTED);

                    if (!string.IsNullOrWhiteSpace(_ctx.TermsText))
                        footer.Item()
                            .Text($"Terms: {_ctx.TermsText}")
                            .FontSize(8).Italic().FontColor(MUTED);
                });
            }

            content.Item().Element(c => RenderSignatureBlocks(c));
        }

        // ════════════════════════════════════════════════════════════════════
        //  SECTION RENDERERS
        // ════════════════════════════════════════════════════════════════════

        private void RenderClientBlock(IContainer container)
        {
            PdfChrome.InfoCard(container, "Client / Bill To", col =>
            {
                PdfChrome.LabelValue(col, "Customer", _ctx.CustomerName);
                PdfChrome.LabelValue(col, "TRN / VAT", _ctx.CustomerTrn);
                PdfChrome.LabelValue(col, "Attention", _ctx.AttentionName);
                PdfChrome.LabelValue(col, "Contact", _ctx.ContactPhone);
                PdfChrome.LabelValue(col, "Address", _ctx.BillingAddress);
                PdfChrome.LabelValue(col, "Client ref", _ctx.CustomerReference);
            });
        }

        private void RenderProjectBlock(IContainer container)
        {
            PdfChrome.InfoCard(container, "Project", col =>
            {
                PdfChrome.LabelValue(col, "Project Name", _ctx.ProjectName);
                PdfChrome.LabelValue(col, "Project Reference", _ctx.ProjectReference);
                PdfChrome.LabelValue(col, "Contract No", _ctx.ProjectContractNo);
                PdfChrome.LabelValue(col, "Site", _ctx.SiteDelivery);
                PdfChrome.LabelValue(col, "LPO / PO", _ctx.LpoNumber);
                PdfChrome.LabelValue(col, "Salesman", _ctx.SalesRepresentative);
                PdfChrome.LabelValue(col, "Valid until", _ctx.ValidUntil);
            });
        }

        private void RenderSpecTable(IContainer container, PiSpecGroup group)
        {
            container.PaddingBottom(10).Column(col =>
            {
                string specLabel = !string.IsNullOrWhiteSpace(group.GroupName)
                    ? group.GroupName
                    : $"Spec Group {group.GroupNumber}";

                col.Item().Element(c =>
                    PdfChrome.GroupBanner(c, $"Specification {group.GroupNumber}: {specLabel}"));

                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(c =>
                    {
                        c.ConstantColumn(24);
                        c.RelativeColumn(1.5f);
                        c.ConstantColumn(32);
                        c.ConstantColumn(32);
                        c.ConstantColumn(32);
                        c.ConstantColumn(32);
                        c.ConstantColumn(32);
                        c.RelativeColumn(0.75f);
                        c.RelativeColumn(0.80f);
                        c.RelativeColumn(0.70f);
                        c.RelativeColumn(0.85f);
                    });

                    table.Header(header =>
                    {
                        var headers = new[]
                        {
                            "#", "GLASS REF", "W1", "H1", "W2", "H2",
                            "QTY", "SQM", "TOTAL SQM", "RATE", "VALUE"
                        };
                        var colors = new[]
                        {
                            ExportColumnPalette.Identity, ExportColumnPalette.Identity,
                            ExportColumnPalette.Pane1, ExportColumnPalette.Pane1,
                            ExportColumnPalette.Pane2, ExportColumnPalette.Pane2,
                            ExportColumnPalette.Qty,
                            ExportColumnPalette.Area, ExportColumnPalette.Area,
                            ExportColumnPalette.Money, ExportColumnPalette.Money
                        };
                        for (int i = 0; i < headers.Length; i++)
                        {
                            bool right = i >= 2;
                            bool center = i == 0 || i == 6;
                            PdfChrome.HeaderCell(header.Cell(), headers[i],
                                center: center, right: right && !center,
                                background: colors[i]);
                        }
                    });

                    bool alt = false;
                    foreach (var row in group.Rows)
                    {
                        AddCell(table, row.Serial.ToString(), alt, center: true);
                        AddCell(table, row.GlassRef, alt);
                        AddCell(table, row.W1.ToString(), alt, right: true,
                            background: ExportColumnPalette.Pane1Fill);
                        AddCell(table, row.H1.ToString(), alt, right: true,
                            background: ExportColumnPalette.Pane1Fill);
                        AddCell(table, row.W2.ToString(), alt, right: true,
                            background: ExportColumnPalette.Pane2Fill);
                        AddCell(table, row.H2.ToString(), alt, right: true,
                            background: ExportColumnPalette.Pane2Fill);
                        AddCell(table, row.Qty.ToString(), alt, center: true,
                            background: ExportColumnPalette.QtyFill);
                        AddCell(table, row.UnitArea.ToString("0.000", CultureInfo.InvariantCulture), alt, right: true,
                            background: ExportColumnPalette.AreaFill);
                        AddCell(table, row.TotalArea.ToString("0.000", CultureInfo.InvariantCulture), alt, right: true,
                            background: ExportColumnPalette.AreaFill);
                        AddCell(table, row.Rate.ToString("N2", CultureInfo.InvariantCulture), alt, right: true);
                        AddCell(table, row.Total.ToString("N2", CultureInfo.InvariantCulture), alt, right: true, bold: true);
                        alt = !alt;
                    }
                });

                col.Item().Element(c =>
                    PdfChrome.SubtotalBar(c,
                        $"Qty {group.TotalQty}  ·  TOTAL SQM {group.TotalArea:0.000}",
                        $"AED {group.TotalValue:N2}"));
            });
        }

        private void RenderGrandSpecSummary(IContainer container)
        {
            int specCount = _ctx.SpecGroups.Count;
            int totalQty = _ctx.SpecGroups.Sum(g => g.TotalQty);
            decimal totalArea = _ctx.SpecGroups.Sum(g => g.TotalArea);
            decimal totalValue = _ctx.SpecGroups.Sum(g => g.TotalValue);

            PdfChrome.GrandTotal(container,
                $"Glass  ·  {specCount} spec{(specCount != 1 ? "s" : "")}  ·  {totalQty} pcs  ·  TOTAL SQM {totalArea:0.000}",
                $"AED {totalValue:N2}");
        }

        private void RenderOtherCharges(IContainer container)
        {
            if (!_ctx.OtherCharges.Any()) return;

            container.PaddingTop(4).Column(col =>
            {
                col.Item().Element(c =>
                    PdfChrome.Section(c, "Other charges"));

                col.Item().Table(hdr =>
                {
                    hdr.ColumnsDefinition(c =>
                    {
                        c.RelativeColumn(2f);
                        c.RelativeColumn(1.2f);
                        c.RelativeColumn(1f);
                        c.RelativeColumn(1f);
                    });
                    PdfChrome.HeaderCell(hdr.Cell(), "DESCRIPTION", center: false);
                    PdfChrome.HeaderCell(hdr.Cell(), "QTY / SQM");
                    PdfChrome.HeaderCell(hdr.Cell(), "RATE", right: true, center: false);
                    PdfChrome.HeaderCell(hdr.Cell(), "AMOUNT", right: true, center: false);
                });

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
                                r.RelativeItem(2).Text(charge.Name)
                                    .FontSize(9).SemiBold();
                                r.RelativeItem(1.2f).Text(charge.QtyLabel)
                                    .FontSize(9).FontColor(MUTED);
                                r.RelativeItem(1).Text(charge.RateLabel)
                                    .FontSize(9).FontColor(MUTED);
                                r.RelativeItem(1).AlignRight()
                                    .Text($"AED {charge.Amount:N2}")
                                    .FontSize(10).Bold();
                            });
                        }
                    });
            });
        }

        private void RenderFinancialSummary(IContainer container)
        {
            container
                .PaddingTop(6)
                .Row(outer =>
                {
                    outer.RelativeItem(1);
                    outer.RelativeItem(1).Column(col =>
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

                    col.Item().PaddingTop(6).Element(c =>
                        PdfChrome.GrandTotal(c,
                            "Total estimate (VAT inc)",
                            $"{_ctx.GrandTotal:N2} AED"));
                    });
                });
        }

        private void RenderTermsAndConditions(IContainer container)
        {
            container.PaddingTop(8).Column(col =>
            {
                col.Item().Element(c =>
                    PdfChrome.Section(c, "Terms & conditions"));

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

        private void RenderSignatureBlocks(IContainer container)
        {
            PdfChrome.Signatures(container,
                new SignatureSlot
                {
                    Title = "Prepared by",
                    Body = col =>
                    {
                        col.Item().PaddingTop(4)
                            .Text(string.IsNullOrWhiteSpace(_ctx.PreparedByName)
                                ? "—" : _ctx.PreparedByName)
                            .FontSize(10).SemiBold().FontColor(DARK);
                        if (!string.IsNullOrWhiteSpace(_ctx.PreparedByDept))
                            col.Item().PaddingTop(2)
                                .Text(_ctx.PreparedByDept)
                                .FontSize(8).FontColor(MUTED);
                        col.Item().PaddingTop(2)
                            .Text(string.IsNullOrWhiteSpace(_ctx.PreparedByStatus)
                                ? "Draft" : _ctx.PreparedByStatus)
                            .FontSize(8).FontColor(GREEN);
                    }
                },
                new SignatureSlot { Title = "Client approval" });
        }

        // ════════════════════════════════════════════════════════════════════
        //  SNAPSHOT → RENDER CONTEXT BUILDER
        //  BUG-21: uses ProformaChargeMetricsCalculator (no more duplication)
        //  BUG-22: uses SqmCalculator for panel area
        //  BUG-24: IsVatApplicable from snapshot.Summary (not VatAmount > 0)
        //  BUG-26: FooterDisclaimer from snapshot only
        // ════════════════════════════════════════════════════════════════════

        private static PiRenderContext BuildRenderContext(
            SavedProformaInvoice entity,
            ProformaInvoiceSnapshot snapshot)
        {
            // BUG-24 [HIGH]: use snapshot.Summary.IsVatApplicable directly
            // Was: entity.VatAmount > 0 — broke on FOC invoices (VAT applicable
            // but amount = 0 for zero-value line items)
            bool isVatApplicable = snapshot.Summary?.IsVatApplicable ?? true;

            var ctx = new PiRenderContext
            {
                CompanyName = snapshot.Company?.CompanyName ?? "",
                CompanyTrn = snapshot.Company?.CompanyTrn ?? "",
                CompanyLocation = snapshot.Company?.CompanyLocation ?? "",
                ProformaId = snapshot.Company?.ProformaId ?? entity.ProformaId,
                IssueDate = snapshot.Company?.IssueDate.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "",
                ValidUntil = snapshot.Company?.ValidUntil.ToString("dd MMM yyyy", CultureInfo.InvariantCulture) ?? "",
                ProformaStatus = snapshot.Company?.ProformaStatus ?? entity.Status,

                ProjectName = snapshot.Project?.ProjectIdentifierName
                    ?? entity.ProjectName,
                ProjectReference = snapshot.Project?.ProjectReference ?? "",
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
                IsVatApplicable = isVatApplicable,          // BUG-24 fix
                VatAmount = entity.VatAmount,
                GrandTotal = entity.GrandTotalVatInclusive,
                VatPercent = snapshot.Summary?.VatPercent ?? Reporting.VatCalculator.VAT_RATE_PERCENT,

                PreparedByName = snapshot.PreparedBy?.PreparedByName ?? "",
                PreparedByDept = snapshot.PreparedBy?.PreparedByDept ?? "",
                PreparedByStatus = snapshot.PreparedBy?.PreparedByStatus ?? "",
                TermsText = snapshot.PreparedBy?.TermsText ?? "",
                // BUG-26: sourced from snapshot only, no hardcoded fallback
                FooterDisclaimer = snapshot.PreparedBy?.FooterDisclaimer ?? ""
            };

            // ── Spec groups ─────────────────────────────────────────────────
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
                            // BUG-22: shared SqmCalculator (0.5 m² floor)
                            decimal unitArea = SqmCalculator.CalculatePanel(r.W1, r.H1);
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

            // ── Other charges — uses shared calculator ──────────────────────
            if (snapshot.OtherCharges != null)
            {
                var templatesByKey = PredefinedChargeTemplates.GetAll()
                    .ToDictionary(t => t.Key, t => t);

                // Predefined charges
                foreach (var c in snapshot.OtherCharges.PredefinedCharges
                    .Where(c => c.IsActive))
                {
                    templatesByKey.TryGetValue(c.TemplateKey ?? "", out var template);
                    string friendlyName = template?.ChargeName ?? c.TemplateKey ?? "";
                    string unitLabel = template?.Unit.ToString().ToUpper() ?? "QTY";

                    decimal metricQty;

                    // BUG-23: DELIVERY quick-hack preserved — documented tech debt.
                    // Proper fix in Chat 8 (PER_ROUND source + editable qty).
                    if (c.TemplateKey == "DELIVERY")
                    {
                        metricQty = 1m;
                        unitLabel = "TRIP";
                    }
                    else if (c.TemplateKey == "POLISH"
                          || c.TemplateKey == "POLISH_MATCHED_LAM")
                    {
                        if (c.PolishSubType == "Custom")
                        {
                            metricQty = c.CustomPolishQty;
                            if (!string.IsNullOrWhiteSpace(c.CustomPolishDescription))
                                friendlyName = $"Polish — {c.CustomPolishDescription}";
                        }
                        else
                        {
                            var polishGroups = ProformaChargeMetricsCalculator.FilterSpecs(
                                snapshot.SpecGroups, c.CheckedSpecGroupNumbers);
                            metricQty = ProformaChargeMetricsCalculator
                                .ComputePolishLm(
                                    polishGroups,
                                    c.PolishSubType,
                                    matchedSameGlassOnly: c.TemplateKey == "POLISH_MATCHED_LAM");
                            if (c.TemplateKey == "POLISH_MATCHED_LAM")
                                friendlyName = "Polish — same-glass laminate (2×)";
                            else if (!string.IsNullOrWhiteSpace(c.PolishSubType))
                                friendlyName = $"Polish — {c.PolishSubType}";
                        }
                    }
                    else
                    {
                        string sourceKey = ProformaChargeMetricsCalculator.FirstChargeSourceKey(
                            c.SelectedMetricKey,
                            c.SelectedSourceKey,
                            template?.DefaultSourceKey);
                        metricQty = ProformaChargeMetricsCalculator
                            .ComputeTargetedMetric(
                                snapshot.SpecGroups,
                                sourceKey,
                                c.CheckedSpecGroupNumbers);
                    }

                    decimal amount = Math.Round(metricQty * c.Rate, 2);

                    ctx.OtherCharges.Add(new PiChargeLine
                    {
                        Name = friendlyName,
                        QtyLabel = metricQty > 0
                                        ? $"{metricQty:0.###} {unitLabel}"
                                        : "—",
                        RateLabel = $"AED {c.Rate:N2}",
                        Amount = amount
                    });
                }

                // Wastages — uses shared calculator
                foreach (var w in snapshot.OtherCharges.Wastages
                    .Where(w => w.IsActive))
                {
                    // BUG-21: shared calculator
                    decimal wastageQty = ProformaChargeMetricsCalculator
                        .ComputeWastageQty(snapshot.SpecGroups, w.CheckedSourceKeys);
                    decimal wastageAmount = Math.Round(wastageQty * w.Rate, 2);

                    ctx.OtherCharges.Add(new PiChargeLine
                    {
                        Name = $"Wastage — {w.ColorName}",
                        QtyLabel = wastageQty > 0
                                        ? $"{wastageQty:0.###} SQM"
                                        : "—",
                        RateLabel = $"AED {w.Rate:N2}",
                        Amount = wastageAmount
                    });
                }

                // Custom charges
                foreach (var c in snapshot.OtherCharges.CustomCharges
                    .Where(c => c.IsActive))
                {
                    decimal amount = Math.Round(c.Qty * c.Rate, 2);
                    string unitStr = string.IsNullOrWhiteSpace(c.Unit)
                                          ? "QTY" : c.Unit.ToUpper();

                    ctx.OtherCharges.Add(new PiChargeLine
                    {
                        Name = c.ChargeName ?? "",
                        QtyLabel = c.Qty > 0
                                        ? $"{c.Qty:0.###} {unitStr}"
                                        : "—",
                        RateLabel = $"AED {c.Rate:N2}",
                        Amount = amount
                    });
                }
            }

            // ── Terms & conditions ──────────────────────────────────────────
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
        //  TABLE CELL HELPERS
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
            string? color = null)
        {
            col.Item().PaddingBottom(3).Row(r =>
            {
                var labelText = r.RelativeItem().Text(label).FontSize(size);
                if (bold) labelText.Bold();
                if (color != null) labelText.FontColor(color);

                var valueText = r.AutoItem().Text(value).FontSize(size);
                if (bold) valueText.Bold();
                if (color != null) valueText.FontColor(color);
            });
        }

        private static void AddCell(
            TableDescriptor table, string text, bool alt,
            bool center = false, bool right = false, bool bold = false,
            string? background = null)
        {
            PdfChrome.DataCell(table.Cell(), text,
                alt: alt, center: center, right: right, bold: bold,
                background: background);
        }

        // ════════════════════════════════════════════════════════════════════
        //  PRIVATE RENDER CONTEXT DTOs
        //  BUG-21: SpecMetrics moved to ProformaChargeMetricsCalculator.cs
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
            public string ProjectReference { get; set; } = "";
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
            public decimal VatPercent { get; set; } = Reporting.VatCalculator.VAT_RATE_PERCENT;
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