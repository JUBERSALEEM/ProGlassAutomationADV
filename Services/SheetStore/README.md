# 📦 Sheet Store Module

<!-- pg-nav -->
> **[Repository root](../../README.md)** · [All documents](../../docs/README.md) · [Application workflow](../../docs/WORKFLOW.md) · [Design system](../../docs/DESIGN_SYSTEM.md)
<!-- pg-nav -->
Manages raw glass sheet inventory. Foundation of the ProGlass workflow —
Proforma Invoice, Job Order, and downstream modules pull pricing and
stock availability from here.

---

## 📁 Files

| File | Purpose |
|---|---|
| `ISheetStoreRepository.cs` | Contract for all data operations |
| `SheetStoreRepository.cs` | SQLite / EF Core implementation |
| `README.md` | This file |

Related:
- `Models/SheetStore/GlassSheet.cs` — entity
- `Services/Database/AppDbContext.cs` — shared DB context
- `ViewModels/SheetStore/*.cs` — UI logic
- `Views/SheetStore/*.xaml` — UI

---

## 📊 GlassSheet Entity

| Field | Type | Notes |
|---|---|---|
| Id | int | Auto-increment PK |
| Category | string(100) | e.g., "Clear Float", "Guardian ClimaGuard 70" |
| ThicknessMm | int | 3, 4, 5, 6, 8, 10, 12, 15, 19 |
| Color | string(50) | Clear, HD Grey, Bronze, Green, Blue, Silver |
| WidthMm, HeightMm | int | mm |
| Supplier | string(100) | Vendor name |
| PurchasePricePerSqm | decimal(18,2) | Cost per m² |
| SellPricePerSqm | decimal(18,2) | Sell per m² |
| Stock | int | Total bought |
| Used | int | Total consumed |
| CreatedAt, LastUpdated | DateTime | Auto-managed |
| LastPurchaseDate | DateTime? | Set on Add or Buy |

**Computed** (not stored):
- `Sqm` = W × H / 1,000,000
- `Balance` = Stock − Used
- `TotalPurchaseValue`, `TotalSellValue`, `BalanceValue`
- `ProfitPerSqm`
- `SizeDisplay`, `ThicknessDisplay`

**Unique constraint**: Category + Thickness + Color + Size + Supplier

---

## ✅ Completed (Phase 1 — Foundation)

- [x] GlassSheet entity with computed properties
- [x] AppDbContext with unique constraint + decimal precision
- [x] DatabaseInitializer (auto-creates on startup)
- [x] ISheetStoreRepository interface (provider-agnostic)
- [x] SheetStoreRepository (async EF Core impl)
- [x] Full CRUD + BuyStock/UseStock business operations
- [x] Filter lookups (Categories, Colors, Suppliers, Thicknesses)

## ✅ Phase 2 — Basic UI (this session)

- [x] SheetStoreViewModel with commands + stats
- [x] AddEditSheetViewModel for dialog
- [x] SheetStoreView with table + toolbar + stats
- [x] AddEditSheetView dialog with validation

---

## 🚧 To-Do

### Phase 3 — Advanced Features
- [ ] Filter dropdowns wired (Category, Thickness, Color, Supplier)
- [ ] Sort options
- [ ] Live search
- [ ] Row context menu (right-click) with keyboard shortcuts
- [ ] Import CSV / Excel
- [ ] Export CSV / Excel
- [ ] Buy/Use history log

### Phase 4 — Design Polish
- [ ] Match owner's mockup (purple gradient header, glass badges)
- [ ] Color swatch beside color name
- [ ] Sticky column headers
- [ ] Row hover animations
- [ ] Better empty state illustration

### Phase 5 — Integration
- [ ] Dashboard: "Glass Inventory" widget with live stats
- [ ] ProformaInvoice: pull glass options from this module
- [ ] Auto-decrement stock when invoice items consume sheets
- [ ] Low-stock alerts / notifications
- [ ] AI: "Suggest similar glass for out-of-stock item"

---

## 🎯 Design Principles

1. **Repository pattern** — ViewModels never touch DbContext directly
2. **Async everywhere** — no UI blocking
3. **Interface-based** — swap SheetStoreRepository for MockRepository in tests
4. **Immutable computed fields** — SQM, Balance always accurate
5. **DB enforces integrity** — unique constraint prevents duplicate SKUs
6. **Audit trail** — timestamps on every mutation

---

## 🔧 Usage Examples

```csharp
var repo = new SheetStoreRepository();

// Add a new sheet
var sheet = await repo.AddAsync(new GlassSheet
{
    Category = "Clear Float",
    ThicknessMm = 6,
    Color = "Clear",
    WidthMm = 3210,
    HeightMm = 2250,
    Supplier = "Guardian Glass",
    PurchasePricePerSqm = 100m,
    SellPricePerSqm = 102m,
    Stock = 50
});

// Buy more stock (price update optional)
await repo.BuyStockAsync(sheet.Id, quantity: 20, newPurchasePrice: 105m);

// Use stock (e.g., from a job order)
await repo.UseStockAsync(sheet.Id, quantity: 5);

// Query
var all = await repo.GetAllAsync();
var categories = await repo.GetCategoriesAsync();
```

---

*Last updated: Phase 2 UI complete.*

---

## ⏱️ Price-version signal (Phase 3, `f6dd157`)

Every write path (`AddAsync`, `UpdateAsync`, `BuyStockAsync`, `UseStockAsync`)
stamps `GlassSheet.LastUpdated`, and stock deletes change the row count.
`Services/ProformaInvoice/GlassPriceStamper` folds that into the
`{max(LastUpdated).Ticks}:{count}` signature persisted with each saved P&L —
that is what lets Job Profitability flag a quote whose *price basis moved*
instead of silently recomputing money. Bulk CSV/Excel imports pass through
the repository, so imports move the stamp just like manual edits.

<!-- pg-nav -->
---

*This README documents `Services/SheetStore/`. For the end-to-end business chain see [`docs/WORKFLOW.md`](../../docs/WORKFLOW.md); for every other document see [`docs/README.md`](../../docs/README.md).*
<!-- pg-nav -->
