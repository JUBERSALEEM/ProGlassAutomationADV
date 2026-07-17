# 🗄️ Database Layer — ProGlass Automation

Single SQLite database used by ALL modules. One file, multiple tables,
per-module repositories provide clean data access.

---

## 📁 Files

| File | Purpose |
|---|---|
| `AppDbContext.cs` | Single EF Core context with DbSets for all modules |
| `DatabaseInitializer.cs` | Auto-creates DB + tables on app startup |

---

## 🗂️ Database Location

`%AppData%\ProGlassAutomation\proglass.db`

- Portable — copy this file to backup entire business data
- Auto-created on first run
- Grows with data (typical: 5-50 MB after years of use)

---

## 🔧 Adding a New Module (Recipe)

When you build a new module (e.g., ProformaInvoice):

1. Create entity: `Models/ProformaInvoice/ProformaInvoice.cs`
2. Add to `AppDbContext`:
   ```csharp
   public DbSet<ProformaInvoice> ProformaInvoices { get; set; }
   ```
3. Configure in `OnModelCreating` (indexes, constraints, precision)
4. Create repository: `Services/ProformaInvoice/ProformaInvoiceRepository.cs`
5. Next app launch → new table auto-created via `EnsureCreated()`

**No manual migration needed** for adding new tables in dev mode.
For production schema changes to existing tables, use EF migrations.

---

## ✅ Current Tables

| Table | Module | Rows Expected |
|---|---|---|
| `GlassSheets` | Sheet Store | 100-2000 |

---

## 🚧 Future Tables

| Table | Module |
|---|---|
| `ProformaInvoices` | Proforma Invoice |
| `ProformaInvoiceItems` | Proforma Invoice |
| `JobOrders` | Job Order |
| `Customers` | Common |
| `Suppliers` | Common |
| `DeliveryNotes` | Dispatch |
| `TaxInvoices` | Dispatch |
| `ProductionEntries` | Production |
| `HistoryLogs` | Common |

---

## 🎯 Design Principles

1. **Single DB file** — enables cross-module queries + referential integrity
2. **Short-lived DbContexts** — `using var db = new AppDbContext()` per operation
3. **No coupling** — modules access only their own DbSets via repositories
4. **Auto-managed timestamps** — CreatedAt / LastUpdated on every entity
5. **Unique constraints** — prevent duplicate SKUs, invoice numbers, etc.
6. **Decimal precision** — always `HasPrecision(18, 2)` for money

---

## 🐛 Troubleshooting

| Issue | Fix |
|---|---|
| "Unable to open database file" | Check `%AppData%` folder permissions |
| Table missing after adding new module | Delete DB file (dev only!) or add EF migration |
| Slow queries | Add index in `OnModelCreating` |

---

*Last updated: Sheet Store foundation complete.*