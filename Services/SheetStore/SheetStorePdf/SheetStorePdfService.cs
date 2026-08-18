using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Services.Export.Pdf;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.SheetStore.SheetStorePdf
{
    /// <summary>
    /// Generates professionally formatted PDF inventory reports.
    /// Uses QuestPDF (MIT license).
    /// </summary>
    public class SheetStorePdfService
    {
        static SheetStorePdfService()
        {
            // Required for free/community use
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public Task GenerateReportAsync(string filePath, IList<GlassSheet> sheets,
                                         string filterInfo = "All sheets (no filters)")
        {
            return Task.Run(() =>
            {
                PdfThemeCatalog.ApplySaved();
                var totalBuy = sheets.Sum(s => s.TotalPurchaseValue);
                var totalSell = sheets.Sum(s => s.TotalSellValue);
                var totalProfit = totalSell - totalBuy;
                var totalStock = sheets.Sum(s => s.Stock);
                var totalUsed = sheets.Sum(s => s.Used);
                var totalBalance = sheets.Sum(s => s.Balance);

                Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4.Landscape());
                        page.Margin(30);
                        page.DefaultTextStyle(x => x.FontSize(9));

                        // ============ HEADER ============
                        page.Header().Column(col =>
                        {
                            col.Item().Background(DocumentStyle.ColorAccent).Padding(15).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("ProGlass Automation")
                                        .FontSize(18).Bold().FontColor(DocumentStyle.ColorAccentOn);
                                    c.Item().Text("Sheet Store — Inventory Report")
                                        .FontSize(11).FontColor(DocumentStyle.ColorAccentSoft);
                                });
                                row.ConstantItem(200).AlignRight().Column(c =>
                                {
                                    c.Item().Text($"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}")
                                        .FontSize(9).FontColor(DocumentStyle.ColorAccentSoft);
                                    c.Item().Text($"User: {Environment.UserName}")
                                        .FontSize(9).FontColor(DocumentStyle.ColorAccentSoft);
                                    c.Item().Text($"{sheets.Count} items")
                                        .FontSize(9).FontColor(DocumentStyle.ColorAccentSoft);
                                });
                            });

                            col.Item().Background(DocumentStyle.ColorSurface).Padding(8).Text(filterInfo)
                                .FontSize(8).Italic().FontColor(DocumentStyle.ColorMuted);
                        });

                        // ============ CONTENT ============
                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            // Summary cards
                            col.Item().Row(row =>
                            {
                                void SummaryCard(string label, string value, string color)
                                {
                                    row.RelativeItem().Border(1).BorderColor(DocumentStyle.ColorBorder)
                                        .Background(DocumentStyle.ColorSurface)
                                        .Padding(10).Column(c =>
                                        {
                                            c.Item().Text(label).FontSize(8).Bold().FontColor(DocumentStyle.ColorMuted);
                                            c.Item().Text(value).FontSize(14).Bold().FontColor(color);
                                        });
                                }

                                SummaryCard("TOTAL STOCK", $"{totalStock} sheets", DocumentStyle.ColorSuccess);
                                SummaryCard("TOTAL USED", $"{totalUsed} sheets", DocumentStyle.ColorDanger);
                                SummaryCard("BALANCE", $"{totalBalance} sheets", DocumentStyle.ColorWarning);
                                SummaryCard("BUYING VALUE", $"{totalBuy:N0} AED", DocumentStyle.ColorDanger);
                                SummaryCard("SELLING VALUE", $"{totalSell:N0} AED", DocumentStyle.ColorSuccess);
                                SummaryCard("PROFIT", $"{totalProfit:N0} AED", DocumentStyle.ColorAccent);
                            });

                            col.Item().PaddingVertical(10);

                            // Data table
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(25);   // #
                                    columns.RelativeColumn(2.5f); // Category
                                    columns.ConstantColumn(45);   // Thick
                                    columns.RelativeColumn(1.2f); // Color
                                    columns.RelativeColumn(1.5f); // Size
                                    columns.RelativeColumn(2f);   // Supplier
                                    columns.ConstantColumn(50);   // SQM
                                    columns.ConstantColumn(60);   // Buy/SQM
                                    columns.ConstantColumn(60);   // Sell/SQM
                                    columns.ConstantColumn(70);   // Total Buy
                                    columns.ConstantColumn(70);   // Total Sell
                                    columns.ConstantColumn(40);   // Stock
                                    columns.ConstantColumn(40);   // Used
                                    columns.ConstantColumn(40);   // Balance
                                });

                                // Table header
                                table.Header(header =>
                                {
                                    void HeaderCell(string text)
                                    {
                                        header.Cell().Background(DocumentStyle.ColorTableHeader).Padding(5)
                                            .Text(text).FontSize(7).Bold().FontColor(DocumentStyle.ColorWhite);
                                    }

                                    HeaderCell("#");
                                    HeaderCell("CATEGORY");
                                    HeaderCell("THICK");
                                    HeaderCell("COLOR");
                                    HeaderCell("SIZE");
                                    HeaderCell("SUPPLIER");
                                    HeaderCell("SQM");
                                    HeaderCell("BUY/SQM");
                                    HeaderCell("SELL/SQM");
                                    HeaderCell("TOTAL BUY");
                                    HeaderCell("TOTAL SELL");
                                    HeaderCell("STOCK");
                                    HeaderCell("USED");
                                    HeaderCell("BAL");
                                });

                                // Table rows
                                for (int i = 0; i < sheets.Count; i++)
                                {
                                    var s = sheets[i];
                                    var bg = i % 2 == 0 ? DocumentStyle.ColorTableAlt : DocumentStyle.ColorWhite;

                                    void DataCell(string text, string color = null)
                                    {
                                        table.Cell().Background(bg).Padding(4)
                                            .Text(text).FontSize(8).FontColor(color ?? DocumentStyle.ColorText);
                                    }

                                    DataCell($"{s.Id}");
                                    DataCell(s.Category, DocumentStyle.ColorAccent);
                                    DataCell(s.ThicknessDisplay);
                                    DataCell(s.Color);
                                    DataCell(s.SizeDisplay);
                                    DataCell(s.Supplier, DocumentStyle.ColorMuted);
                                    DataCell($"{s.SqmPerSheet:N4}");
                                    DataCell($"{s.PurchasePricePerSqm:N2}");
                                    DataCell($"{s.SellPricePerSqm:N2}");
                                    DataCell($"{s.TotalPurchaseValue:N0}", DocumentStyle.ColorDanger);
                                    DataCell($"{s.TotalSellValue:N0}", DocumentStyle.ColorSuccess);
                                    DataCell($"{s.Stock}", DocumentStyle.ColorSuccess);
                                    DataCell($"{s.Used}", DocumentStyle.ColorDanger);
                                    DataCell($"{s.Balance}", DocumentStyle.ColorWarning);
                                }
                            });

                            col.Item().PaddingVertical(8);

                            // Totals row
                            col.Item().Background(DocumentStyle.ColorAccentLight)
                                .Border(1).BorderColor(DocumentStyle.ColorBorder)
                                .Padding(10).Row(row =>
                                {
                                    row.RelativeItem().Text("TOTALS").Bold().FontSize(10)
                                        .FontColor(DocumentStyle.ColorText);
                                    row.ConstantItem(120).Text($"Buy: {totalBuy:N0} AED")
                                        .Bold().FontSize(9).FontColor(DocumentStyle.ColorDanger);
                                    row.ConstantItem(120).Text($"Sell: {totalSell:N0} AED")
                                        .Bold().FontSize(9).FontColor(DocumentStyle.ColorSuccess);
                                    row.ConstantItem(120).Text($"Profit: {totalProfit:N0} AED")
                                        .Bold().FontSize(9).FontColor(DocumentStyle.ColorAccent);
                                    row.ConstantItem(80).Text($"Stock: {totalStock}")
                                        .Bold().FontSize(9).FontColor(DocumentStyle.ColorSuccess);
                                    row.ConstantItem(80).Text($"Balance: {totalBalance}")
                                        .Bold().FontSize(9).FontColor(DocumentStyle.ColorWarning);
                                });
                        });

                        // ============ FOOTER ============
                        page.Footer().AlignCenter().Text(text =>
                        {
                            text.Span("ProGlass Automation — Sheet Store Report  •  Page ");
                            text.CurrentPageNumber();
                            text.Span(" of ");
                            text.TotalPages();
                            text.Span($"  •  © {DateTime.Now.Year}");
                        });
                    });
                }).GeneratePdf(filePath);
            });
        }
    }
}