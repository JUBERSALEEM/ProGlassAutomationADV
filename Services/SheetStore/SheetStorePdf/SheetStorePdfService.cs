using ProGlassAutomation.Models.SheetStore;
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
                            col.Item().Background("#1E88E5").Padding(15).Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("ProGlass Automation")
                                        .FontSize(20).Bold().FontColor("#FFFFFF");
                                    c.Item().Text("Sheet Store — Inventory Report")
                                        .FontSize(12).FontColor("#E3F2FD");
                                });
                                row.ConstantItem(200).AlignRight().Column(c =>
                                {
                                    c.Item().Text($"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}")
                                        .FontSize(9).FontColor("#E3F2FD");
                                    c.Item().Text($"User: {Environment.UserName}")
                                        .FontSize(9).FontColor("#E3F2FD");
                                    c.Item().Text($"{sheets.Count} items")
                                        .FontSize(9).FontColor("#E3F2FD");
                                });
                            });

                            // Filter info
                            col.Item().Background("#F5F5F5").Padding(8).Text(filterInfo)
                                .FontSize(8).Italic().FontColor("#64748B");
                        });

                        // ============ CONTENT ============
                        page.Content().PaddingVertical(10).Column(col =>
                        {
                            // Summary cards
                            col.Item().Row(row =>
                            {
                                void SummaryCard(string label, string value, string color)
                                {
                                    row.RelativeItem().Border(1).BorderColor("#E2E8F0")
                                        .Padding(10).Column(c =>
                                        {
                                            c.Item().Text(label).FontSize(8).Bold().FontColor("#64748B");
                                            c.Item().Text(value).FontSize(14).Bold().FontColor(color);
                                        });
                                }

                                SummaryCard("TOTAL STOCK", $"{totalStock} sheets", "#10B981");
                                SummaryCard("TOTAL USED", $"{totalUsed} sheets", "#EF4444");
                                SummaryCard("BALANCE", $"{totalBalance} sheets", "#F59E0B");
                                SummaryCard("BUYING VALUE", $"{totalBuy:N0} AED", "#991B1B");
                                SummaryCard("SELLING VALUE", $"{totalSell:N0} AED", "#166534");
                                SummaryCard("PROFIT", $"{totalProfit:N0} AED", "#1E40AF");
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
                                        header.Cell().Background("#334155").Padding(5)
                                            .Text(text).FontSize(7).Bold().FontColor("#FFFFFF");
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
                                    var bg = i % 2 == 0 ? "#F8FAFC" : "#FFFFFF";

                                    void DataCell(string text, string color = "#1F2937")
                                    {
                                        table.Cell().Background(bg).Padding(4)
                                            .Text(text).FontSize(8).FontColor(color);
                                    }

                                    DataCell($"{s.Id}");
                                    DataCell(s.Category, "#1E88E5");
                                    DataCell(s.ThicknessDisplay);
                                    DataCell(s.Color);
                                    DataCell(s.SizeDisplay);
                                    DataCell(s.Supplier, "#64748B");
                                    DataCell($"{s.SqmPerSheet:N4}");
                                    DataCell($"{s.PurchasePricePerSqm:N2}");
                                    DataCell($"{s.SellPricePerSqm:N2}");
                                    DataCell($"{s.TotalPurchaseValue:N0}", "#991B1B");
                                    DataCell($"{s.TotalSellValue:N0}", "#166534");
                                    DataCell($"{s.Stock}", "#166534");
                                    DataCell($"{s.Used}", "#991B1B");
                                    DataCell($"{s.Balance}", "#92400E");
                                }
                            });

                            col.Item().PaddingVertical(8);

                            // Totals row
                            col.Item().Background("#EFF6FF").Border(1).BorderColor("#93C5FD")
                                .Padding(10).Row(row =>
                                {
                                    row.RelativeItem().Text("TOTALS").Bold().FontSize(10);
                                    row.ConstantItem(120).Text($"Buy: {totalBuy:N0} AED")
                                        .Bold().FontSize(9).FontColor("#991B1B");
                                    row.ConstantItem(120).Text($"Sell: {totalSell:N0} AED")
                                        .Bold().FontSize(9).FontColor("#166534");
                                    row.ConstantItem(120).Text($"Profit: {totalProfit:N0} AED")
                                        .Bold().FontSize(9).FontColor("#1E40AF");
                                    row.ConstantItem(80).Text($"Stock: {totalStock}")
                                        .Bold().FontSize(9).FontColor("#166534");
                                    row.ConstantItem(80).Text($"Balance: {totalBalance}")
                                        .Bold().FontSize(9).FontColor("#F59E0B");
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