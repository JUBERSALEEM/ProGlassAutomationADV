// ═══════════════════════════════════════════════════════════════════════
// FILE: PiPreviewParityRenderer.cs
// PROJECT: ProGlassAutomation
// MODULE: Proforma Invoice export — WYSIWYG paper renderer (2026-09-11)
// ────────────────────────────────────────────────────────────────────────
// THE ONE DOCUMENT DESIGN.
//
// This file is a faithful QuestPDF port of the Step 7 on-screen paper
// (Views/ProformaInvoice/Step7PrintPreview/PrintableInvoice/
// PrintableInvoiceView.xaml). Same section order, same labels, same column
// geometry, same logo-placement rule, same bands and cards — driven by the
// SAME PdfTheme/PdfMetrics tokens the preview consumes through
// PrintTokenService. The result: the exported PDF is the preview, printed.
//
// WHY IT REPLACED THE FIVE PDF ARCHITECTURES
// ──────────────────────────────────────────
// Before this renderer, the export pipeline drew one of five completely
// different page architectures (letterheads, section chrome, totals bars,
// signature blocks) keyed off PdfLayoutKind, while the preview showed a
// single composition. Four of the five exports could therefore NEVER look
// like the screen — and the layouts carried details operators flagged as
// unprofessional: a rainbow multi-colour spec header, subtotal bars that
// ignored column alignment, and charge headers floating above misaligned
// rows. One design + five professional STYLE variants (palette, rhythm,
// density, rules) is what finally makes "what you see is what prints".
//
// UNIT CONTRACT
// ─────────────
// The preview paper is 794 DIP wide; A4 is 595.276 pt; 1 pt = 3/4 DIP.
// Every fixed preview measurement is converted with Px() below, so the
// paper geometry is identical in both pipelines.
//
// MONEY SAFETY
// ────────────
// No Math.Round in this file — every amount arrives pre-rounded from the
// entity/snapshot. All numeric and date formatting is pinned to
// InvariantCulture (release gate: ExportCultureInvarianceTests).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Services.Export.Pdf;
using ProGlassAutomation.Services.Export.Pdf.Renderers;
using ProGlassAutomation.Services.Export.PrintPreview;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace ProGlassAutomation.Services.ProformaInvoice.Export
{
    // ════════════════════════════════════════════════════════════════
    //  DOCUMENT MODEL (built by ProformaInvoicePdfLayout)
    // ════════════════════════════════════════════════════════════════

    internal sealed class PiDoc
    {
        public string CompanyName { get; set; } = "";
        public string CompanyLocation { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
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
        public decimal VatPercent { get; set; }
        public decimal VatAmount { get; set; }
        public decimal GrandTotal { get; set; }

        public string PreparedByName { get; set; } = "";
        public string PreparedByDept { get; set; } = "";
        public string PreparedByStatus { get; set; } = "";
        public string TermsText { get; set; } = "";
        public string FooterDisclaimer { get; set; } = "";

        public List<PiSpecGroupModel> SpecGroups { get; } = new();
        public List<PiChargeModel> OtherCharges { get; } = new();
        public List<PiClauseModel> TermClauses { get; } = new();
    }

    internal sealed class PiSpecGroupModel
    {
        public int GroupNumber { get; set; }
        public string GroupName { get; set; } = "";
        public List<PiGlassRowModel> Rows { get; } = new();
        public int TotalQty { get; set; }
        public decimal TotalArea { get; set; }
        public decimal TotalValue { get; set; }
    }

    internal sealed class PiGlassRowModel
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

    internal sealed class PiChargeModel
    {
        public string Name { get; set; } = "";
        public string QtyLabel { get; set; } = "";
        public string RateLabel { get; set; } = "";
        public decimal Amount { get; set; }
    }

    internal sealed class PiClauseModel
    {
        public int Order { get; set; }
        public string Title { get; set; } = "";
        public string Body { get; set; } = "";
        public List<string> SubLines { get; } = new();
    }

    // ════════════════════════════════════════════════════════════════
    //  SHARED TOKEN HELPERS (letterhead + body read the same values)
    // ════════════════════════════════════════════════════════════════

    internal static class PiPrintTokens
    {
        public static PdfTheme T => DocumentStyle.Current;
        public static PdfMetrics M => DocumentStyle.Metrics;

        /// <summary>Preview DIPs → print points (1 pt = 3/4 DIP).</summary>
        public static float Px(double dip) => (float)(dip * 0.75);

        public static string Money(decimal v) =>
            "AED " + v.ToString("N2", CultureInfo.InvariantCulture);

        public static string MoneySuffix(decimal v) =>
            v.ToString("N2", CultureInfo.InvariantCulture) + " AED";

        public static string Sqm(decimal v) =>
            v.ToString("0.000", CultureInfo.InvariantCulture);

        /// <summary>Print.BorderStrong = Mix(Border, Text, 0.45) — the same
        /// formula PrintTokenService publishes to the preview.</summary>
        public static string BorderStrong() => Mix(T.Border, T.Text, 0.45);

        /// <summary>Print.SurfaceAlt = Mix(Surface, White, 0.5).</summary>
        public static string SurfaceAlt() => Mix(T.Surface, T.White, 0.5);

        /// <summary>Print.OnAccentMuted = Mix(AccentOn, Accent, 0.25).</summary>
        public static string OnAccentMuted() => Mix(T.AccentOn, T.Accent, 0.25);

        /// <summary>
        /// The shared label column for one card. The preview uses an Auto
        /// grid column (widest label wins, all values align); QuestPDF rows
        /// have no shared grid, so the width is measured from the card's own
        /// labels and capped at the theme's LabelColumn.
        /// </summary>
        public static float LabelWidth(params string[] labels)
        {
            int longest = labels.Max(l => l?.Length ?? 0);
            float estimated = longest * M.LabelFont * 0.52f + Px(6);
            return Math.Min(Math.Max(estimated, 42f), M.LabelColumn);
        }

        // One colour-blend implementation for both pipelines (screen tokens
        // + printed page). Delegates to PrintTokenService so no rounding
        // call site is added to this money-namespace file.
        private static string Mix(string a, string b, double t) =>
            PrintTokenService.Mix(a, b, t);
    }

    // ════════════════════════════════════════════════════════════════
    //  DOCUMENT STRINGS — mirrored from Themes/Languages/en-US.xaml so
    //  the paper reads exactly like the preview. The PDF is a legal
    //  customer document: English source strings, invariant numbers.
    // ════════════════════════════════════════════════════════════════

    internal static class PiStr
    {
        public const string DocumentTitle = "Proforma Invoice";
        public const string CompanyTagline =
            "Premium Glass Glazing, High-Yield Manufacturing & Logistics Spec";
        public const string CompanyWebsite = "• www.proglass-automation.com";

        public const string ClientCardTitle = "CLIENT / BILL TO";
        public const string ProjectCardTitle = "PROJECT ESTIMATE METRIC";

        public const string LblCustomerName = "CUSTOMER NAME";
        public const string LblTrnVat = "TRN/VAT Number:";
        public const string LblAttention = "Attention Name:";
        public const string LblContactPhone = "Contact Phone:";
        public const string LblBillingAddress = "BILLING ADDRESS";
        public const string LblClientReference = "Client Reference:";
        public const string LblProjectName = "PROJECT NAME";
        public const string LblProjectReference = "PROJECT REFERENCE";
        public const string LblContractNo = "CONTRACT NO";
        public const string LblSiteDelivery = "Site Delivery:";
        public const string LblLpo = "LPO / PO Reference:";
        public const string LblSalesman = "Assigned Salesman:";
        public const string LblValidUntil = "VALID UNTIL";

        public const string ColGlassRef = "GLASS REF / DESCRIPTION";
        public const string ColTotalSqm = "TOTAL SQM";
        public const string ColRate = "RATE (AED)";
        public const string ColValue = "VALUE (AED)";
        public const string SpecSubtotal = "SPEC SUB-TOTAL";

        public const string GrandSpecTotal = "GRAND SPEC TOTAL";
        public const string Specs = "Specs";
        public const string QtyLabel = "•  QTY:";
        public const string TotalSqmLabel = "•  TOTAL SQM:";
        public const string ValueLabel = "VALUE: AED";

        public const string OtherChargesTitle = "SUPPLEMENTARY / FABRICATION OTHER CHARGES";
        public const string OtherChargesSubtotal = "OTHER CHARGES SUBTOTAL";

        public const string GlassMaterialsTotal = "Glass Materials Total:";
        public const string OtherChargesTotalLabel = "Other Processing/Delivery Total:";
        public const string NetSubtotal = "Estimated Net Subtotal:";
        public const string Discount = "DISCOUNT";
        public const string GrandTotalLabel = "TOTAL EST VALUE (VAT INC):";

        public const string TermsTitle = "TERMS & CONDITIONS";
        public const string TermsEmpty =
            "No terms configured. Add clauses in the Terms editor above.";

        public const string PreparedTitle = "PREPARED BY (ERP AUTHENTICATED)";
        public const string ClientApprovalTitle = "CLIENT APPROVAL SIGN & MOB NUMBER";
        public const string ApprovalSign = "Approval Sign:";
        public const string MobileNumber = "Mobile Number:";
        public const string AuthorizedStamp = "Authorized Name & Stamp";
    }

    // ════════════════════════════════════════════════════════════════
    //  LETTERHEAD — the preview's masthead / company header, exactly
    // ════════════════════════════════════════════════════════════════

    /// <summary>
    /// Page-1 letterhead for the PI document, mirroring the preview:
    ///   • Top-panel logo  → full-width accent masthead (logo chip centred,
    ///     identity left, document reference right) followed by the
    ///     reference box.
    ///   • Inline logo     → company identity (logo chip beside it) on the
    ///     left, bordered reference box on the right, then the 2 DIP accent
    ///     rule the preview draws under the header.
    /// The logo-placement rule is the SAME rule the preview applies
    /// (CompanySetupViewModel.LogoIsTopPanel → PrintTokenService.IsTopPanelLogo),
    /// so the mark lands in the same slot on screen and on paper.
    /// </summary>
    internal sealed class PiPreviewLetterhead : IPdfLetterheadStyle
    {
        private static PdfTheme T => PiPrintTokens.T;
        private static PdfMetrics M => PiPrintTokens.M;
        private static float Px(double dip) => PiPrintTokens.Px(dip);

        public void Render(IContainer container, LetterheadInfo info)
        {
            bool topPanel = EffectiveIsTopPanel(info);

            container.Column(col =>
            {
                if (topPanel)
                {
                    col.Item().Element(c => Masthead(c, info));
                    // Preview keeps the reference box visible in top-panel
                    // mode too — the identity column is simply empty.
                    col.Item().PaddingTop(M.BlockGap).Row(row =>
                    {
                        row.RelativeItem(1f);
                        row.ConstantItem(M.GutterGap);
                        row.RelativeItem(0.48f)
                            .Element(c => ReferenceBox(c, info.DocumentId, info.IssueDateText));
                    });
                }
                else
                {
                    col.Item().Element(c => CompanyHeader(c, info));
                }

                // The 2 DIP accent rule under the header block.
                col.Item().PaddingTop(M.BlockGap)
                    .Height(Px(2)).Background(T.Accent);
            });
        }

        public void RenderContinuation(IContainer container, LetterheadInfo info)
        {
            // Slim identity strip for page 2+: enough to file the page,
            // never enough to compete with the content.
            container.Column(col =>
            {
                col.Item().PaddingBottom(M.StackGap).Row(row =>
                {
                    row.RelativeItem().Text(info.CompanyName ?? "")
                        .FontSize(M.MetaValueFont).SemiBold().FontColor(T.Accent);
                    row.RelativeItem().AlignRight()
                        .Text($"{PiStr.DocumentTitle}  ·  {info.DocumentId}")
                        .FontSize(M.MetaValueFont).FontColor(T.Muted);
                });
                col.Item().Height(Math.Max(0.75f, M.HairlineWeight))
                    .Background(T.Border);
            });
        }

        // ── Top-panel masthead ────────────────────────────────────────

        private static void Masthead(IContainer container, LetterheadInfo info)
        {
            byte[]? logo = PdfChrome.LoadLogoBytes(info);

            container.Background(T.Accent)
                .PaddingHorizontal(M.MastheadPadH).PaddingVertical(M.MastheadPadV)
                .Column(col =>
                {
                    if (logo != null)
                    {
                        col.Item().AlignCenter()
                            .Background(T.White)
                            .Border(M.CardBorderWeight).BorderColor(T.Border)
                            .Padding(M.RowGap)
                            .Width(TopPanelLogoW(info), Unit.Millimetre)
                            .Height(TopPanelLogoH(info), Unit.Millimetre)
                            .Image(logo).FitArea();
                    }

                    col.Item().PaddingTop(logo != null ? M.BlockGap : 0).Row(row =>
                    {
                        row.RelativeItem(1.55f).Column(left =>
                        {
                            left.Item().Text(info.CompanyName ?? "")
                                .FontSize(T.TitleSize).Bold().FontColor(T.AccentOn);

                            var sub = info.CompanyLocation ?? "";
                            if (!string.IsNullOrWhiteSpace(info.CompanyTrn))
                                sub = string.IsNullOrWhiteSpace(sub)
                                    ? $"TRN {info.CompanyTrn}"
                                    : $"{sub}  ·  TRN {info.CompanyTrn}";
                            if (!string.IsNullOrWhiteSpace(sub))
                                left.Item().PaddingTop(M.StackGap).Text(sub)
                                    .FontSize(M.MetaValueFont).FontColor(T.AccentSoft);
                        });

                        row.ConstantItem(M.GutterGap);

                        row.RelativeItem().AlignRight().Column(right =>
                        {
                            right.Item().AlignRight()
                                .Text(PiStr.DocumentTitle)
                                .FontSize(M.KickerFont).Bold().FontColor(T.AccentOn);
                            right.Item().AlignRight().PaddingTop(M.StackGap)
                                .Text(info.DocumentId ?? "")
                                .FontSize(T.IdSize).Bold().FontColor(T.Highlight);
                        });
                    });
                });
        }

        // ── Standard company header + reference box ───────────────────

        private static void CompanyHeader(IContainer container, LetterheadInfo info)
        {
            byte[]? logo = PdfChrome.LoadLogoBytes(info);

            container.Row(row =>
            {
                // Left: [logo chip] + company identity.
                row.RelativeItem(1f).AlignMiddle().Row(left =>
                {
                    if (logo != null)
                    {
                        left.AutoItem().PaddingRight(M.GutterGap).AlignMiddle()
                            .Element(c => LogoChip(c, info, logo));
                    }

                    left.RelativeItem().Column(id =>
                    {
                        id.Item().Text(info.CompanyName ?? "")
                            .FontSize(T.TitleSize).Bold().FontColor(T.Accent);
                        id.Item().PaddingTop(M.StackGap)
                            .Text(PiStr.CompanyTagline)
                            .FontSize(M.MetaValueFont).FontColor(T.Muted);

                        string locationLine = string.IsNullOrWhiteSpace(info.CompanyLocation)
                            ? PiStr.CompanyWebsite
                            : $"{info.CompanyLocation} {PiStr.CompanyWebsite}";
                        id.Item().PaddingTop(M.StackGap)
                            .Text(locationLine)
                            .FontSize(M.MetaValueFont).FontColor(T.Muted);

                        id.Item().PaddingTop(M.StackGap).Row(trn =>
                        {
                            trn.AutoItem().PaddingRight(M.StackGap).Text("TRN:")
                                .FontSize(M.MetaValueFont).SemiBold().FontColor(T.Muted);
                            // RelativeItem (not Auto): a long TRN wraps
                            // instead of overflowing the row — auto items
                            // that exceed their row throw a QuestPDF layout
                            // conflict.
                            trn.RelativeItem()
                                .Text(string.IsNullOrWhiteSpace(info.CompanyTrn)
                                    ? "—" : info.CompanyTrn)
                                .FontSize(M.MetaValueFont).SemiBold().FontColor(T.Text);
                        });
                    });
                });

                row.ConstantItem(M.GutterGap);

                // Right: bordered reference box (title, Ref, Date).
                row.RelativeItem(0.48f).AlignMiddle()
                    .Element(c => ReferenceBox(c, info.DocumentId, info.IssueDateText));
            });
        }

        private static void ReferenceBox(
            IContainer container, string documentId, string issueDate)
        {
            container
                .Background(T.Surface)
                .Border(M.CardBorderWeight).BorderColor(PiPrintTokens.BorderStrong())
                .Padding(M.CardPad)
                .Column(col =>
                {
                    col.Item().AlignCenter()
                        .Text(PiStr.DocumentTitle)
                        .FontSize(M.SectionFont).Bold().FontColor(T.Text);

                    col.Item().PaddingVertical(M.RowGap)
                        .Height(Math.Max(0.75f, M.HairlineWeight))
                        .Background(PiPrintTokens.BorderStrong());

                    RefRow(col, "Ref No:", string.IsNullOrWhiteSpace(documentId)
                        ? "—" : documentId, top: 0);
                    RefRow(col, "Date:", string.IsNullOrWhiteSpace(issueDate)
                        ? "—" : issueDate, top: M.RowGap);
                });
        }

        private static void RefRow(ColumnDescriptor col, string label, string value, float top)
        {
            col.Item().PaddingTop(top).Row(r =>
            {
                r.AutoItem().PaddingRight(M.RowGap)
                    .Text(label)
                    .FontSize(M.LabelFont).FontColor(T.Muted);
                r.RelativeItem()
                    .Text(value)
                    .FontSize(M.ValueFont).SemiBold().FontColor(T.Text);
            });
        }

        private static void LogoChip(IContainer container, LetterheadInfo info, byte[] logo)
        {
            container
                .Background(T.White)
                .Border(M.CardBorderWeight).BorderColor(T.Border)
                .Padding(M.RowGap)
                .Width(InlineLogoW(info), Unit.Millimetre)
                .Height(InlineLogoH(info), Unit.Millimetre)
                .Image(logo).FitArea();
        }

        // ── Logo placement rule (mirrors the preview exactly) ─────────

        private static bool EffectiveIsTopPanel(LetterheadInfo info)
        {
            if (PdfChrome.LoadLogoBytes(info) == null) return false;

            // CompanySetupViewModel.LogoIsTopPanel:
            //   HasLogo && (explicit TopPanel choice || mark too wide for a
            //   portrait chip).
            return info.LogoLayout == CompanyLogoLayout.TopPanel
                || PrintTokenService.IsTopPanelLogo(info.LogoWidthMm, info.LogoHeightMm);
        }

        // Same clamps as PdfChrome so the printed chip matches the shared
        // letterhead chip for identical company records.
        private static float InlineLogoW(LetterheadInfo info) =>
            Clamp(info.LogoWidthMm > 0 ? info.LogoWidthMm : 22,
                PrintTokenService.LogoMinWidthMm, PrintTokenService.LogoMaxWidthMm);

        private static float InlineLogoH(LetterheadInfo info) =>
            Clamp(info.LogoHeightMm > 0 ? info.LogoHeightMm : 26,
                PrintTokenService.LogoMinHeightMm, PrintTokenService.LogoMaxHeightMm);

        private static float TopPanelLogoW(LetterheadInfo info)
        {
            // A fixed Width larger than its parent is a hard QuestPDF layout
            // conflict ("conflicting size constraints"), so the chip is
            // clamped to the ACTUAL band width: page width minus margins,
            // masthead padding and the chip's own chrome. With airy themes
            // (20 mm margins) a 180 mm mark would otherwise overflow.
            float mastheadPadMm = (float)(M.MastheadPadH * 25.4 / 72.0);
            float safeMax = 210f - (2f * T.MarginMm) - (2f * mastheadPadMm) - 8f;
            float hi = Math.Max(40f, Math.Min(180f, safeMax));
            return Clamp(info.LogoWidthMm > 0 ? info.LogoWidthMm : 160, 40, hi);
        }

        private static float TopPanelLogoH(LetterheadInfo info) =>
            Clamp(info.LogoHeightMm > 0 ? info.LogoHeightMm : 24, 10, 32);

        private static float Clamp(double v, double lo, double hi) =>
            (float)Math.Min(hi, Math.Max(lo, v));
    }

    // ════════════════════════════════════════════════════════════════
    //  BODY — the preview's paper, section by section
    // ════════════════════════════════════════════════════════════════

    internal static class PiPreviewParityRenderer
    {
        private static PdfTheme T => PiPrintTokens.T;
        private static PdfMetrics M => PiPrintTokens.M;
        private static float Px(double dip) => PiPrintTokens.Px(dip);
        private static string Money(decimal v) => PiPrintTokens.Money(v);
        private static string MoneySuffix(decimal v) => PiPrintTokens.MoneySuffix(v);
        private static string Sqm(decimal v) => PiPrintTokens.Sqm(v);

        public static void RenderBody(ColumnDescriptor content, PiDoc doc)
        {
            // 1 ── Client + Project cards
            content.Item().Element(c => ClientProjectRow(c, doc));

            // 2 ── Section divider (the preview's 1px rule before specs)
            content.Item().Element(c =>
                c.Height(Math.Max(0.75f, M.HairlineWeight)).Background(T.Border));

            // 3 ── Specification tables
            foreach (var group in doc.SpecGroups)
                content.Item().Element(c => SpecSection(c, group));

            // 4 ── Grand spec band
            if (doc.SpecGroups.Count > 0)
                content.Item().Element(c => GrandSpecBand(c, doc));

            // 5 ── Other charges panel
            if (doc.OtherCharges.Count > 0)
                content.Item().Element(c => OtherChargesSection(c, doc));

            // 6 ── Financial summary waterfall
            content.Item().Element(c => FinancialSummary(c, doc));

            // 7 ── Terms & conditions
            content.Item().Element(c => TermsSection(c, doc));

            // 8 ── Disclaimer + terms line
            if (!string.IsNullOrWhiteSpace(doc.FooterDisclaimer)
                || !string.IsNullOrWhiteSpace(doc.TermsText))
            {
                content.Item().Element(c => DisclaimerSection(c, doc));
            }

            // 9 ── Signature cards
            content.Item().Element(c => SignatureSection(c, doc));
        }

        // ────────────────────────────────────────────────────────────────
        //  CLIENT + PROJECT CARDS
        // ────────────────────────────────────────────────────────────────

        private static void ClientProjectRow(IContainer container, PiDoc doc)
        {
            container.Row(row =>
            {
                row.RelativeItem().Element(c => Card(c, PiStr.ClientCardTitle, col =>
                {
                    float labelW = PiPrintTokens.LabelWidth(
                        PiStr.LblCustomerName, PiStr.LblTrnVat, PiStr.LblAttention,
                        PiStr.LblContactPhone, PiStr.LblBillingAddress,
                        PiStr.LblClientReference);

                    LblVal(col, labelW, PiStr.LblCustomerName, doc.CustomerName);
                    LblVal(col, labelW, PiStr.LblTrnVat, doc.CustomerTrn);
                    LblVal(col, labelW, PiStr.LblAttention, doc.AttentionName);
                    LblVal(col, labelW, PiStr.LblContactPhone, doc.ContactPhone);
                    LblVal(col, labelW, PiStr.LblBillingAddress, doc.BillingAddress);
                    LblVal(col, labelW, PiStr.LblClientReference, doc.CustomerReference, last: true);
                }));

                row.ConstantItem(Px(16));

                row.RelativeItem().Element(c => Card(c, PiStr.ProjectCardTitle, col =>
                {
                    float labelW = PiPrintTokens.LabelWidth(
                        PiStr.LblProjectName, PiStr.LblProjectReference,
                        PiStr.LblContractNo, PiStr.LblSiteDelivery, PiStr.LblLpo,
                        PiStr.LblSalesman, PiStr.LblValidUntil);

                    LblVal(col, labelW, PiStr.LblProjectName, doc.ProjectName);
                    LblVal(col, labelW, PiStr.LblProjectReference, doc.ProjectReference);
                    LblVal(col, labelW, PiStr.LblContractNo, doc.ProjectContractNo);
                    LblVal(col, labelW, PiStr.LblSiteDelivery, doc.SiteDelivery);
                    LblVal(col, labelW, PiStr.LblLpo, doc.LpoNumber);
                    LblVal(col, labelW, PiStr.LblSalesman, doc.SalesRepresentative);
                    ValidUntilRow(col, labelW, doc);
                }));
            });
        }

        /// <summary>
        /// Bordered card with an inset accent title banner — the preview's
        /// card model (banner sits inside the card padding, gap below it).
        /// </summary>
        private static void Card(IContainer container, string title, Action<ColumnDescriptor> body)
        {
            container.Border(M.CardBorderWeight).BorderColor(T.Border)
                .Padding(M.CardPad)
                .Column(col =>
                {
                    col.Item()
                        .Background(T.Accent)
                        .PaddingHorizontal(M.GroupPadH).PaddingVertical(M.GroupPadV)
                        .Text(title)
                        .FontSize(M.MetaValueFont).Bold().FontColor(T.AccentOn);

                    col.Item().PaddingTop(Px(6));
                    body(col);
                });
        }

        private static void LblVal(
            ColumnDescriptor col, float labelWidth,
            string label, string? value, bool last = false)
        {
            col.Item().PaddingBottom(last ? 0 : M.RowGap).Row(r =>
            {
                r.ConstantItem(labelWidth)
                    .Text(label)
                    .FontSize(M.LabelFont).FontColor(T.Muted);
                r.RelativeItem()
                    .Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                    .FontSize(M.ValueFont).SemiBold().FontColor(T.Text);
            });
        }

        private static void ValidUntilRow(ColumnDescriptor col, float labelWidth, PiDoc doc)
        {
            col.Item().Row(r =>
            {
                r.ConstantItem(labelWidth)
                    .Text(PiStr.LblValidUntil)
                    .FontSize(M.LabelFont).FontColor(T.Muted);
                r.RelativeItem().Row(v =>
                {
                    v.AutoItem().PaddingRight(M.RowGap)
                        .Text(string.IsNullOrWhiteSpace(doc.ValidUntil) ? "—" : doc.ValidUntil)
                        .FontSize(M.ValueFont).SemiBold().FontColor(T.Text);
                    v.AutoItem().PaddingRight(M.RowGap)
                        .Text("•")
                        .FontSize(M.LabelFont).FontColor(T.Muted);
                    // Relative: a long status wraps instead of overflowing
                    // the row (AutoItems that exceed their row raise a
                    // QuestPDF sizing conflict).
                    v.RelativeItem()
                        .Text(string.IsNullOrWhiteSpace(doc.ProformaStatus)
                            ? "Draft" : doc.ProformaStatus)
                        .FontSize(M.ValueFont).SemiBold()
                        .FontColor(StatusColor(doc.ProformaStatus));
                });
            });
        }

        /// <summary>Status colour map — mirrors ProformaStatusValueStyle.</summary>
        private static string StatusColor(string? status)
        {
            switch ((status ?? "").Trim().ToLowerInvariant())
            {
                case "approved": return T.Success;
                case "sent": return T.Accent;      // Print.Info == accent
                case "rejected": return T.Danger;
                case "expired": return T.Warning;
                default: return T.Muted;
            }
        }

        // ────────────────────────────────────────────────────────────────
        //  SPECIFICATION TABLES
        //  Uniform themed header (the old rainbow per-column palette is
        //  gone), zebra rows, and a sub-total row whose QTY / TOTAL SQM /
        //  VALUE cells sit UNDER their columns — exactly like the preview.
        // ────────────────────────────────────────────────────────────────

        private static void SpecSection(IContainer container, PiSpecGroupModel group)
        {
            container.Column(col =>
            {
                // Group banner (preview: accent band "Specification N: name").
                col.Item()
                    .Background(T.Accent)
                    .PaddingHorizontal(M.GroupPadH).PaddingVertical(M.GroupPadV)
                    .Text($"Specification {group.GroupNumber.ToString(CultureInfo.InvariantCulture)}: {group.GroupName}")
                    .FontSize(M.GroupFont).Bold().FontColor(T.AccentOn);

                col.Item().Table(table =>
                {
                    DefineSpecColumns(table);

                    table.Header(header =>
                    {
                        SpecHeaderCell(header.Cell(), "#", center: true);
                        SpecHeaderCell(header.Cell(), PiStr.ColGlassRef, left: true);
                        SpecHeaderCell(header.Cell(), "W1", right: true);
                        SpecHeaderCell(header.Cell(), "H1", right: true);
                        SpecHeaderCell(header.Cell(), "W2", right: true);
                        SpecHeaderCell(header.Cell(), "H2", right: true);
                        SpecHeaderCell(header.Cell(), "QTY", center: true);
                        SpecHeaderCell(header.Cell(), "SQM", right: true);
                        SpecHeaderCell(header.Cell(), PiStr.ColTotalSqm, right: true);
                        SpecHeaderCell(header.Cell(), PiStr.ColRate, right: true);
                        SpecHeaderCell(header.Cell(), PiStr.ColValue, right: true);
                    });

                    bool alt = false;
                    foreach (var row in group.Rows)
                    {
                        SpecCell(table, row.Serial.ToString(CultureInfo.InvariantCulture), alt, center: true);
                        SpecCell(table, row.GlassRef, alt);
                        SpecCell(table, row.W1.ToString(CultureInfo.InvariantCulture), alt, right: true);
                        SpecCell(table, row.H1.ToString(CultureInfo.InvariantCulture), alt, right: true);
                        SpecCell(table, row.W2.ToString(CultureInfo.InvariantCulture), alt, right: true);
                        SpecCell(table, row.H2.ToString(CultureInfo.InvariantCulture), alt, right: true);
                        SpecCell(table, row.Qty.ToString(CultureInfo.InvariantCulture), alt, center: true);
                        SpecCell(table, Sqm(row.UnitArea), alt, right: true);
                        SpecCell(table, Sqm(row.TotalArea), alt, right: true);
                        SpecCell(table, Money(row.Rate), alt, right: true);
                        SpecCell(table, Money(row.Total), alt, right: true, bold: true);
                        alt = !alt;
                    }

                    // SPEC SUB-TOTAL — spans the identity columns; totals sit
                    // under QTY / TOTAL SQM / VALUE (preview TOTALS-01).
                    table.Cell().ColumnSpan(6)
                        .Element(c => SubtotalChrome(c, align: 0)
                            .Text(PiStr.SpecSubtotal)
                            .FontSize(M.MetaValueFont).Bold().FontColor(T.Accent));
                    table.Cell().Element(c => SubtotalChrome(c, align: 1)
                        .Text(group.TotalQty.ToString(CultureInfo.InvariantCulture))
                        .FontSize(M.SubtotalValueFont).Bold().FontColor(T.Text));
                    table.Cell().Element(c => SubtotalChrome(c, align: 2)
                        .Text(" ").FontSize(M.TableFont));                       // SQM blank
                    table.Cell().Element(c => SubtotalChrome(c, align: 2)
                        .Text(Sqm(group.TotalArea))
                        .FontSize(M.SubtotalValueFont).Bold().FontColor(T.Text));
                    table.Cell().Element(c => SubtotalChrome(c, align: 2)
                        .Text(" ").FontSize(M.TableFont));                       // RATE blank
                    table.Cell().Element(c => SubtotalChrome(c, align: 2)
                        .Text(Money(group.TotalValue))
                        .FontSize(M.SubtotalValueFont).Bold().FontColor(T.Success));
                });
            });
        }

        private static void DefineSpecColumns(TableDescriptor table)
        {
            table.ColumnsDefinition(c =>
            {
                c.ConstantColumn(Px(28));      // #
                c.RelativeColumn(1.4f);        // GLASS REF / DESCRIPTION
                c.ConstantColumn(Px(32));      // W1
                c.ConstantColumn(Px(32));      // H1
                c.ConstantColumn(Px(32));      // W2
                c.ConstantColumn(Px(32));      // H2
                c.ConstantColumn(Px(36));      // QTY
                c.RelativeColumn(0.70f);       // SQM
                c.RelativeColumn(0.85f);       // TOTAL SQM
                c.RelativeColumn(0.70f);       // RATE
                c.RelativeColumn(0.90f);       // VALUE
            });
        }

        private static void SpecHeaderCell(
            IContainer cell, string text,
            bool left = false, bool right = false, bool center = false)
        {
            IContainer painted = cell
                .Background(T.TableHeader)
                .Border(T.TableGrid ? M.CardBorderWeight : 0)
                .BorderColor(T.AccentDark)
                .PaddingHorizontal(T.CellPadH).PaddingVertical(T.CellPadV);

            if (right) painted = painted.AlignRight();
            else if (center) painted = painted.AlignCenter();
            // left stays default

            painted.Text(text ?? "")
                .FontSize(M.HeaderFont).Bold().FontColor(T.AccentOn);
        }

        private static void SpecCell(
            TableDescriptor table, string text, bool alt,
            bool center = false, bool right = false, bool bold = false)
        {
            var bg = (T.Zebra && alt) ? T.TableAlt : T.White;
            IContainer painted = table.Cell().Background(bg);

            if (T.TableGrid)
                painted = painted.Border(M.CardBorderWeight).BorderColor(T.Border);
            else
                painted = painted.BorderBottom(M.HairlineWeight * 0.6f).BorderColor(T.Border);

            painted = painted
                .PaddingHorizontal(T.CellPadH).PaddingVertical(T.CellPadV);

            if (right) painted = painted.AlignRight();
            else if (center) painted = painted.AlignCenter();

            var txt = painted.Text(text ?? "").FontSize(M.TableFont);
            if (bold) txt.Bold();
            txt.FontColor(T.Text);
        }

        /// <summary>Shared chrome for sub-total row cells.</summary>
        private static IContainer SubtotalChrome(IContainer cell, int align)
        {
            IContainer painted = cell
                .Background(T.Surface)
                .BorderBottom(M.CardBorderWeight).BorderColor(T.Border)
                .PaddingHorizontal(T.CellPadH).PaddingVertical(T.CellPadV);

            return align switch
            {
                1 => painted.AlignCenter(),
                2 => painted.AlignRight(),
                _ => painted.AlignLeft()
            };
        }

        // ────────────────────────────────────────────────────────────────
        //  GRAND SPEC BAND (preview TOTALS-03)
        // ────────────────────────────────────────────────────────────────

        private static void GrandSpecBand(IContainer container, PiDoc doc)
        {
            int specs = doc.SpecGroups.Count;
            int qty = doc.SpecGroups.Sum(g => g.TotalQty);
            decimal area = doc.SpecGroups.Sum(g => g.TotalArea);
            decimal value = doc.SpecGroups.Sum(g => g.TotalValue);

            // ERR FIX (2026-09-11): this band was a Row of AutoItems — when
            // the stat cluster's combined width exceeded the page (long
            // labels + large values), QuestPDF raised "conflicting size
            // constraints". Spans inside RelativeItems can never demand
            // more width than exists; they wrap instead.
            string onAccentMuted = PiPrintTokens.OnAccentMuted();

            container.Background(T.Accent).Padding(M.CardPad).Row(row =>
            {
                row.RelativeItem(1.6f).AlignMiddle().Text(left =>
                {
                    left.DefaultTextStyle(x => x.FontSize(M.GroupFont).FontColor(T.AccentOn));
                    left.Span(PiStr.GrandSpecTotal).Bold();
                    left.Span("  •  ").FontColor(onAccentMuted);
                    left.Span($"{specs.ToString(CultureInfo.InvariantCulture)} {PiStr.Specs}").SemiBold();
                    left.Span("   ").FontColor(onAccentMuted);
                    left.Span(PiStr.QtyLabel).FontColor(onAccentMuted);
                    left.Span(" " + qty.ToString(CultureInfo.InvariantCulture)).Bold();
                    left.Span("   ").FontColor(onAccentMuted);
                    left.Span(PiStr.TotalSqmLabel).FontColor(onAccentMuted);
                    left.Span(" " + Sqm(area)).Bold();
                });

                row.RelativeItem(1f).AlignRight().AlignMiddle().Text(right =>
                {
                    right.DefaultTextStyle(x => x.FontSize(M.GroupFont).FontColor(T.AccentOn));
                    right.Span(PiStr.ValueLabel).FontColor(onAccentMuted);
                    right.Span("  ");
                    right.Span(Money(value)).Bold().FontSize(M.GrandValueFont);
                });
            });
        }

        // ────────────────────────────────────────────────────────────────
        //  OTHER CHARGES (banner + panel + aligned rows + subtotal)
        // ────────────────────────────────────────────────────────────────

        private static void OtherChargesSection(IContainer container, PiDoc doc)
        {
            container.Column(col =>
            {
                col.Item()
                    .Background(T.Accent)
                    .PaddingHorizontal(M.GroupPadH).PaddingVertical(M.GroupPadV)
                    .Text(PiStr.OtherChargesTitle)
                    .FontSize(M.GroupFont).Bold().FontColor(T.AccentOn);

                col.Item()
                    .Background(PiPrintTokens.SurfaceAlt())
                    .BorderLeft(M.CardBorderWeight).BorderRight(M.CardBorderWeight)
                    .BorderBottom(M.CardBorderWeight).BorderColor(T.Border)
                    .Padding(M.CardPad)
                    .Column(panel =>
                    {
                        foreach (var charge in doc.OtherCharges)
                        {
                            panel.Item().PaddingVertical(M.StackGap)
                                .Element(c => ChargeRow(c, charge));
                        }

                        // OTHER CHARGES SUBTOTAL under the amount column.
                        panel.Item().PaddingTop(M.RowGap)
                            .BorderTop(Math.Max(0.75f, M.HairlineWeight))
                            .BorderColor(PiPrintTokens.BorderStrong())
                            .PaddingTop(M.RowGap)
                            .Row(r =>
                            {
                                r.RelativeItem(4.2f).AlignMiddle()
                                    .Text(PiStr.OtherChargesSubtotal)
                                    .FontSize(M.MetaValueFont).Bold().FontColor(T.Accent);
                                r.RelativeItem(1f).AlignRight()
                                    .Text(Money(doc.OtherChargesTotal))
                                    .FontSize(M.GrandValueFont).Bold().FontColor(T.Success);
                            });
                    });
            });
        }

        private static void ChargeRow(IContainer container, PiChargeModel charge)
        {
            // Columns mirror the preview grid: 2* / 1.2* / 1* / 1*.
            container.Row(r =>
            {
                r.RelativeItem(2f)
                    .Text(charge.Name)
                    .FontSize(M.ValueFont).SemiBold().FontColor(T.Text);

                r.RelativeItem(1.2f).Row(q =>
                {
                    q.AutoItem().PaddingRight(M.StackGap)
                        .Text("Qty")
                        .FontSize(M.LabelFont).FontColor(T.Muted);
                    q.RelativeItem()
                        .Text(charge.QtyLabel)
                        .FontSize(M.ValueFont).SemiBold().FontColor(T.Text);
                });

                r.RelativeItem(1f).Row(rt =>
                {
                    rt.AutoItem().PaddingRight(M.StackGap)
                        .Text("Rate:")
                        .FontSize(M.LabelFont).FontColor(T.Muted);
                    rt.RelativeItem()
                        .Text(charge.RateLabel)
                        .FontSize(M.ValueFont).SemiBold().FontColor(T.Text);
                });

                r.RelativeItem(1f).AlignRight()
                    .Text(Money(charge.Amount))
                    .FontSize(M.GroupFont).Bold().FontColor(T.Text);
            });
        }

        // ────────────────────────────────────────────────────────────────
        //  FINANCIAL SUMMARY WATERFALL (right-aligned, as on the preview)
        // ────────────────────────────────────────────────────────────────

        private static void FinancialSummary(IContainer container, PiDoc doc)
        {
            container.BorderTop(Math.Max(0.75f, M.HairlineWeight)).BorderColor(T.Border)
                .PaddingTop(M.BlockGap)
                .Row(outer =>
                {
                    outer.RelativeItem();
                    outer.AutoItem().MinWidth(Px(340)).Column(col =>
                    {
                        SummaryRow(col, PiStr.GlassMaterialsTotal, MoneySuffix(doc.GlassMaterialsTotal));
                        SummaryRow(col, PiStr.OtherChargesTotalLabel, MoneySuffix(doc.OtherChargesTotal));

                        col.Item().PaddingVertical(M.StackGap)
                            .Height(Math.Max(0.75f, M.HairlineWeight)).Background(T.Border);

                        SummaryRow(col, PiStr.NetSubtotal, MoneySuffix(doc.NetSubtotal),
                            bold: true, size: M.GroupFont);

                        if (doc.DiscountAmount > 0)
                            SummaryRow(col, PiStr.Discount,
                                "- " + Money(doc.DiscountAmount), color: T.Danger);

                        if (doc.IsVatApplicable)
                            SummaryRow(col,
                                $"Standard UAE VAT ({doc.VatPercent.ToString("0.##", CultureInfo.InvariantCulture)}%):",
                                MoneySuffix(doc.VatAmount));

                        col.Item().PaddingTop(M.RowGap)
                            .Background(T.Accent)
                            .PaddingHorizontal(M.TotalsPadH).PaddingVertical(M.TotalsPadV)
                            .Row(g =>
                            {
                                g.RelativeItem().AlignMiddle()
                                    .Text(PiStr.GrandTotalLabel)
                                    .FontSize(M.SectionFont).Bold().FontColor(T.AccentOn);
                                g.AutoItem().AlignMiddle()
                                    .Text(MoneySuffix(doc.GrandTotal))
                                    .FontSize(M.SectionFont).Bold().FontColor(T.AccentOn);
                            });
                    });
                });
        }

        private static void SummaryRow(
            ColumnDescriptor col, string label, string value,
            bool bold = false, float size = 0, string? color = null)
        {
            if (size <= 0) size = M.ValueFont;
            col.Item().PaddingBottom(M.RowGap).Row(r =>
            {
                var l = r.RelativeItem().Text(label).FontSize(size);
                l = bold ? l.Bold() : l.SemiBold();
                l.FontColor(color ?? T.Text);

                var v = r.AutoItem().PaddingLeft(M.GutterGap).Text(value).FontSize(size);
                v = bold ? v.Bold() : v.SemiBold();
                v.FontColor(color ?? T.Text);
            });
        }

        // ────────────────────────────────────────────────────────────────
        //  TERMS & CONDITIONS
        // ────────────────────────────────────────────────────────────────

        private static void TermsSection(IContainer container, PiDoc doc)
        {
            container.Column(col =>
            {
                col.Item()
                    .Background(T.Accent)
                    .PaddingHorizontal(M.GroupPadH).PaddingVertical(M.GroupPadV)
                    .Text(PiStr.TermsTitle)
                    .FontSize(M.GroupFont).Bold().FontColor(T.AccentOn);

                col.Item()
                    .Background(PiPrintTokens.SurfaceAlt())
                    .BorderLeft(M.CardBorderWeight).BorderRight(M.CardBorderWeight)
                    .BorderBottom(M.CardBorderWeight).BorderColor(T.Border)
                    .Padding(M.CardPad)
                    .Column(panel =>
                    {
                        if (doc.TermClauses.Count == 0)
                        {
                            panel.Item().AlignCenter()
                                .Text(PiStr.TermsEmpty)
                                .FontSize(M.MetaValueFont).Italic().FontColor(T.Muted);
                            return;
                        }

                        foreach (var clause in doc.TermClauses)
                        {
                            panel.Item().PaddingBottom(M.RowGap).Column(cl =>
                            {
                                cl.Item()
                                    .Text($"{clause.Order.ToString(CultureInfo.InvariantCulture)}. {clause.Title}")
                                    .FontSize(M.GroupFont).Bold().FontColor(T.Text);

                                if (!string.IsNullOrWhiteSpace(clause.Body))
                                    cl.Item().PaddingLeft(Px(12))
                                        .Text(clause.Body)
                                        .FontSize(M.TableFont).FontColor(T.Text);

                                foreach (var line in clause.SubLines)
                                {
                                    if (string.IsNullOrWhiteSpace(line)) continue;
                                    cl.Item().PaddingLeft(Px(24)).PaddingTop(M.StackGap).Row(b =>
                                    {
                                        b.AutoItem().PaddingRight(M.StackGap)
                                            .Text("•")
                                            .FontSize(M.MetaValueFont).Bold().FontColor(T.Accent);
                                        b.RelativeItem()
                                            .Text(line)
                                            .FontSize(M.MetaValueFont).FontColor(T.Text);
                                    });
                                }
                            });
                        }
                    });
            });
        }

        // ────────────────────────────────────────────────────────────────
        //  DISCLAIMER + TERMS LINE
        // ────────────────────────────────────────────────────────────────

        private static void DisclaimerSection(IContainer container, PiDoc doc)
        {
            container.BorderTop(Math.Max(0.75f, M.HairlineWeight)).BorderColor(T.Border)
                .PaddingTop(M.BlockGap)
                .Column(col =>
                {
                    if (!string.IsNullOrWhiteSpace(doc.FooterDisclaimer))
                        col.Item()
                            .Text(doc.FooterDisclaimer)
                            .FontSize(M.MetaValueFont).Italic().FontColor(T.Muted);

                    if (!string.IsNullOrWhiteSpace(doc.TermsText))
                        col.Item().PaddingTop(M.StackGap)
                            .Text($"Terms: {doc.TermsText}")
                            .FontSize(M.MetaValueFont).Italic().FontColor(T.Muted);
                });
        }

        // ────────────────────────────────────────────────────────────────
        //  SIGNATURE CARDS
        // ────────────────────────────────────────────────────────────────

        private static void SignatureSection(IContainer container, PiDoc doc)
        {
            container.Row(row =>
            {
                row.RelativeItem().Element(c => SignatureCard(c, PiStr.PreparedTitle, col =>
                {
                    col.Item().PaddingTop(M.RowGap)
                        .Text(string.IsNullOrWhiteSpace(doc.PreparedByName)
                            ? "—" : doc.PreparedByName)
                        .FontSize(M.GroupFont).SemiBold().FontColor(T.Text);

                    if (!string.IsNullOrWhiteSpace(doc.PreparedByDept))
                        col.Item().PaddingTop(M.StackGap)
                            .Text(doc.PreparedByDept)
                            .FontSize(M.MetaValueFont).FontColor(T.Muted);

                    col.Item().PaddingTop(M.StackGap).Row(s =>
                    {
                        s.AutoItem().PaddingRight(M.StackGap)
                            .Text("Status:")
                            .FontSize(M.MetaValueFont).FontColor(T.Muted);
                        // Relative so a long status wraps, never overflows.
                        s.RelativeItem()
                            .Text(string.IsNullOrWhiteSpace(doc.PreparedByStatus)
                                ? "Draft" : doc.PreparedByStatus)
                            .FontSize(M.MetaValueFont).Bold()
                            .FontColor(StatusColor(doc.PreparedByStatus));
                    });
                }));

                row.ConstantItem(Px(16));

                row.RelativeItem().Element(c => SignatureCard(c, PiStr.ClientApprovalTitle, col =>
                {
                    SignatureLine(col, PiStr.ApprovalSign);
                    SignatureLine(col, PiStr.MobileNumber);
                    col.Item().PaddingTop(M.RowGap)
                        .Text(PiStr.AuthorizedStamp)
                        .FontSize(M.MetaValueFont).Italic().FontColor(T.Muted);
                }));
            });
        }

        private static void SignatureCard(
            IContainer container, string title, Action<ColumnDescriptor> body)
        {
            container.Border(M.CardBorderWeight).BorderColor(T.Border)
                .Padding(M.CardPad)
                .Column(col =>
                {
                    col.Item()
                        .Background(T.Accent)
                        .PaddingHorizontal(M.GroupPadH).PaddingVertical(M.GroupPadV)
                        .Text(title)
                        .FontSize(M.MetaValueFont).Bold().FontColor(T.AccentOn);

                    col.Item().PaddingTop(Px(6));
                    body(col);
                });
        }

        private static void SignatureLine(ColumnDescriptor col, string label)
        {
            col.Item().PaddingTop(M.RowGap).Row(r =>
            {
                r.AutoItem().PaddingRight(M.RowGap).AlignBottom()
                    .Text(label)
                    .FontSize(M.MetaValueFont).FontColor(T.Muted);
                r.RelativeItem().AlignBottom().PaddingBottom(M.StackGap)
                    .Height(Math.Max(0.75f, M.HairlineWeight))
                    .Background(PiPrintTokens.BorderStrong());
            });
        }
    }
}
