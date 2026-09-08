# Sheet Store Excel Export

<!-- pg-nav -->
> **[Repository root](../../../README.md)** · [All documents](../../../docs/README.md) · [Application workflow](../../../docs/WORKFLOW.md) · [Design system](../../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->
Exports glass sheet inventory as professionally formatted Excel (.xlsx) files.
Uses ClosedXML (MIT license) for Excel generation without requiring
Microsoft Office installed.

---

## Features

- ProGlass branded header row (blue, centered)
- Generation timestamp + filter info + item count
- Column headers (dark, bold, centered)
- Alternating row colors (zebra striping)
- Color-coded values:
  - Stock: green
  - Used: red
  - Balance: amber
  - Total Buy: red
  - Total Sell: green
- Number formatting (decimals, thousands separator)
- Totals row (blue background)
- Profit row (light blue)
- Auto-fit column widths
- Frozen header row (scroll without losing headers)

---

## File Structure

```
Services/SheetStore/SheetStoreExcel/
  SheetStoreExcelService.cs    Export logic
  README.md                    This file
```

---

## API

```csharp
var service = new SheetStoreExcelService();
await service.ExportAsync(
    filePath: "C:\\export.xlsx",
    sheets: filteredSheetsList,
    filterInfo: "Category: Clear Float, Sort: Stock Low to High"
);
```

---

## Dependencies

- ClosedXML 0.102.x (MIT license, ~2MB)

---

## Status

### Completed
- Formatted Excel export with header, data, totals, profit
- Color-coded columns matching app UI
- Auto-fit column widths
- Frozen header row
- Respects current filters (exports what user sees)

### To-Do
- History export to Excel
- Multiple sheets in one workbook (inventory + history)
- Chart sheet (stock levels, value breakdown)
- Template-based export (custom company header)

---

## Design Principles

1. **Professional** — formatted for business use, not raw data dump
2. **What you see is what you get** — exports current filtered view
3. **Self-contained** — no Microsoft Office required
4. **Async** — runs on background thread, no UI freeze

<!-- pg-nav -->
---

*This README documents `Services/SheetStore/SheetStoreExcel/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../../docs/README.md).*
<!-- pg-nav -->
