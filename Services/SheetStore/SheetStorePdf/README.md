# Sheet Store PDF Report

Generates professionally formatted PDF inventory reports.
Uses QuestPDF (MIT license, community edition).

---

## Features

- A4 Landscape orientation
- Blue branded header with company name + report title
- Generation timestamp + username + item count
- Active filter info displayed
- Summary cards row: Stock, Used, Balance, Buy Value, Sell Value, Profit
- Full data table with 14 columns
- Alternating row colors (zebra striping)
- Color-coded values matching app UI
- Totals row with blue background
- Page numbers in footer
- Async generation (no UI freeze)

---

## File Structure

```
Services/SheetStore/SheetStorePdf/
  SheetStorePdfService.cs    PDF generation logic
  README.md                  This file
```

---

## API

```csharp
var service = new SheetStorePdfService();
await service.GenerateReportAsync(
    filePath: "C:\\report.pdf",
    sheets: filteredSheetsList,
    filterInfo: "Category: Clear Float"
);
```

---

## Dependencies

- QuestPDF 2024.x (MIT license, community edition)

---

## Status

### Completed
- Inventory report with header, summary, table, totals, footer
- Color-coded columns matching app UI
- Alternating row colors
- Page numbers
- Respects current filters

### To-Do
- History report PDF
- Chart page (stock levels pie chart)
- Company logo in header (from settings)
- Custom page size option
- Multi-page table continuation header

---

## Design Principles

1. **Professional** — formatted for management presentations
2. **WYSIWYG** — exports current filtered view
3. **Branded** — ProGlass header on every page
4. **Async** — no UI freeze during generation
5. **Self-contained** — no external PDF viewer needed to generate