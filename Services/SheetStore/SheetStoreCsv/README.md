# Sheet Store CSV Import / Export

<!-- pg-nav -->
> **[Repository root](../../../README.md)** · [All documents](../../../docs/README.md) · [Application workflow](../../../docs/WORKFLOW.md) · [Design system](../../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->
Import glass sheets from CSV files and export current view to CSV.
Uses [CsvHelper](https://joshclose.github.io/CsvHelper/) for robust parsing.

---

## Purpose

- **Export**: Save current filtered/sorted table view to a `.csv` file for backup, reporting, or sharing with suppliers
- **Import**: Bulk-add glass sheets from supplier catalogs or migrated data
- **Duplicate handling**: Per-row user decision (Skip / Add to Stock / Update / Cancel)

---

## File Structure

```
Services/SheetStore/SheetStoreCsv/
  SheetStoreCsvService.cs          Import/export logic
  README.md                        This file

ViewModels/SheetStore/SheetStoreCsv/
  DuplicateResolutionViewModel.cs  Dialog for handling duplicates

Views/SheetStore/SheetStoreCsv/
  DuplicateResolutionView.xaml     Duplicate resolution dialog
  DuplicateResolutionView.xaml.cs
```

---

## CSV Format

Standard CSV, first row is headers:

```csv
Category,Thickness,Color,Width,Height,Supplier,PurchasePricePerSqm,SellPricePerSqm,Stock,Used
Clear Float,6,Clear,3210,2250,Guardian Glass,100.00,102.00,50,0
Clear Float,8,Grey,3210,2250,Supplier 2,105.00,110.00,57,0
```

### Column Order (via GlassSheetCsvMap)

| # | Column | Type | Notes |
|---|---|---|---|
| 0 | Category | string | e.g., "Clear Float" |
| 1 | Thickness | int | mm (3, 4, 5, 6, 8, 10, 12, 15, 19) |
| 2 | Color | string | e.g., "Clear", "HD Grey" |
| 3 | Width | int | mm |
| 4 | Height | int | mm |
| 5 | Supplier | string | Vendor name |
| 6 | PurchasePricePerSqm | decimal | AED per SQM |
| 7 | SellPricePerSqm | decimal | AED per SQM |
| 8 | Stock | int | Total bought |
| 9 | Used | int | Total consumed |

### Notes
- Values with commas must be wrapped in double quotes: `"Guardian, Inc."`
- Encoding: UTF-8
- Invariant culture (`.` for decimal, no thousand separators)
- Missing columns are ignored (defaults used)

---

## Duplicate Handling

A "duplicate" is a sheet matching the DB unique constraint:
**Category + Thickness + Color + Width + Height + Supplier**

When import encounters a duplicate, `DuplicateResolutionView` opens:

| Action | Effect |
|---|---|
| **Skip** | Ignore this CSV row; keep existing DB row unchanged |
| **Add to Stock** | Buy operation: adds CSV `Stock` to existing DB `Stock`, keeps existing prices |
| **Update All** | Overwrites all fields with CSV values (prices, stock, used) |
| **Cancel Import** | Stops the entire import immediately |

**"Apply to remaining duplicates"** checkbox: if checked, the chosen action
applies to ALL subsequent duplicates without asking again.

---

## Repository API

```csharp
public class SheetStoreCsvService
{
    Task ExportAsync(string filePath, IEnumerable<GlassSheet> sheets);
    Task<List<GlassSheet>> ImportAsync(string filePath);
}
```

Import is a **pure read** — the service doesn't touch the DB. The caller
(SheetStoreViewModel) handles DB writes so duplicate resolution can be
interactive and each write is history-logged.

---

## Import Flow (in SheetStoreViewModel)

```
1. User picks CSV file
2. csvService.ImportAsync(path) -> List<GlassSheet>
3. For each incoming sheet:
   a. Check if duplicate exists (in _allSheets)
   b. If duplicate:
      - If "apply to all" is set -> use remembered action
      - Else -> show DuplicateResolutionView dialog
   c. Execute chosen action:
      - Skip     -> counter++
      - Add      -> BuyStockAsync(existingId, csvStock) + log history
      - Update   -> UpdateAsync(csvValues) + log history
      - Cancel   -> break loop
   d. If not duplicate -> AddAsync(csv) + log history
4. Show summary MessageBox with counters
5. Reload the table
```

---

## Status

### Completed
- [x] CsvHelper integration
- [x] GlassSheetCsvMap (column order + names)
- [x] Export to CSV (respects current filter/sort)
- [x] Import from CSV (returns list, no DB writes)
- [x] Duplicate detection via unique constraint fields
- [x] DuplicateResolutionView dialog with side-by-side comparison
- [x] "Apply to remaining" per-action
- [x] Summary dialog after import
- [x] History logging for every imported row

### To-Do
- [ ] Excel import/export (.xlsx via ClosedXML)
- [ ] Import validation preview (show all rows before committing)
- [ ] Template download (empty CSV with headers)
- [ ] Auto-detect encoding
- [ ] Progress bar for large imports
- [ ] Undo import (rollback all rows added)

---

## Dependencies

- **CsvHelper** 33.x (MIT license, ~250KB)

---

## Common Issues

| Issue | Fix |
|---|---|
| "Format exception on Thickness" | CSV has non-numeric value in Thickness column |
| Rows silently skipped | Check `MissingFieldFound = null` — headers may not match |
| Decimal values wrong | Use InvariantCulture: `.` for decimal (not `,`) |
| Import very slow | For >10k rows, consider bulk insert instead of per-row |

---

*Last updated: CSV import/export complete with duplicate handling.*

<!-- pg-nav -->
---

*This README documents `Services/SheetStore/SheetStoreCsv/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../../docs/README.md).*
<!-- pg-nav -->
