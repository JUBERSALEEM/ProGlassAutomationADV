// ═══════════════════════════════════════════════════════════════════════
//  SOURCE PINS — non-glass consumables & unit of measure
//  Added 2026-09-06.
//
//  Owner: "include air spacer silicon air spacer joint ... as per their
//  type like qty based sqm based lm based ... for example sheet in qty so
//  9 qty if anything bought in lm 45.55 lm".
//
//  THE DESIGN DECISION THESE LOCK
//
//  Consumables live in their OWN table, not in a widened GlassSheet:
//
//    • GlassSheet.Stock is an INT. 45.55 LM of spacer bar cannot be
//      represented. Widening it to decimal would ripple through
//      SheetStorePricingService, GlassPriceStamper, SpecPaneResolver,
//      SpecSheetRegistry, DashboardMetricsService and four Agent tools —
//      all of which price live proforma invoices.
//    • GlassSheet derives area from WidthMm x HeightMm. A drum of
//      silicone has no width or height.
//    • IX_GlassSheet_Unique keys on Category+Thickness+Colour+Size+
//      Supplier, which is meaningless for a sack of desiccant.
//
//  The owner's hard constraint was "without breaking its binding to
//  proforma invoice or other module", so glass is untouched.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class InventoryConsumablesSourceTests
    {
        private static string RepoRoot()
        {
            var dir = AppContext.BaseDirectory;
            while (dir != null && !File.Exists(Path.Combine(dir, "ProGlassAutomation.csproj")))
                dir = Path.GetDirectoryName(dir);
            return dir ?? throw new InvalidOperationException("repo root not found");
        }

        private static string Read(params string[] parts) =>
            File.ReadAllText(Path.Combine(RepoRoot(), Path.Combine(parts)));

        // ── 1. Glass is untouched — the invoice binding is safe ────────
        //
        // This is the pin that protects the owner's hard constraint. If a
        // later change widens GlassSheet.Stock, it fails here first.
        [Fact]
        public void GlassSheet_stock_remains_an_int_so_invoice_pricing_is_undisturbed()
        {
            var glass = Read("Models", "SheetStore", "GlassSheet.cs");

            Assert.Contains("public int Stock { get; set; }", glass);
            Assert.Contains("public int Used { get; set; }", glass);
        }

        // ── 2. Consumable quantities are DECIMAL ───────────────────────
        [Fact]
        public void Consumable_quantities_are_decimal_for_lm_and_kg()
        {
            var item = Read("Models", "SheetStore", "InventoryItem.cs");

            Assert.Contains("public decimal Stock { get; set; }", item);
            Assert.Contains("public decimal Used { get; set; }", item);
            Assert.Contains("public decimal Balance => Stock - Used;", item);
        }

        // ── 3. Every IGU material's unit is representable ──────────────
        [Fact]
        public void All_igu_material_units_exist()
        {
            var item = Read("Models", "SheetStore", "InventoryItem.cs");

            Assert.Contains("Piece", item);         // corner keys
            Assert.Contains("LinearMetre", item);   // spacer bar
            Assert.Contains("SquareMetre", item);   // film
            Assert.Contains("Kilogram", item);      // desiccant, butyl
            Assert.Contains("Litre", item);         // liquid sealant
            Assert.Contains("CubicMetre", item);    // argon

            Assert.Contains("SpacerBar", item);
            Assert.Contains("Desiccant", item);
            Assert.Contains("PrimarySeal", item);
            Assert.Contains("SecondarySeal", item);
            Assert.Contains("Gas", item);
        }

        // ── 4. Enum members are append-only ────────────────────────────
        //
        // These persist as INTs. Reordering silently reinterprets every
        // stored row — spacer bar would become desiccant.
        [Fact]
        public void Enum_ordinals_are_pinned_because_they_persist_as_ints()
        {
            var item = Read("Models", "SheetStore", "InventoryItem.cs");

            Assert.Contains("Piece = 0", item);
            Assert.Contains("LinearMetre = 1", item);
            Assert.Contains("SquareMetre = 2", item);
            Assert.Contains("Kilogram = 3", item);
            Assert.Contains("Litre = 4", item);
            Assert.Contains("CubicMetre = 5", item);

            Assert.Contains("Other = 0", item);
            Assert.Contains("SpacerBar = 1", item);
            Assert.Contains("Desiccant = 2", item);
        }

        // ── 5. Decimals use TEXT affinity (D-6 convention) ─────────────
        //
        // NUMERIC affinity on SQLite yields REAL (binary float) storage.
        // For MONEY that loses precision under SQL-side SUM; for spacer-bar
        // QUANTITIES it drifts across repeated receipts and issues until
        // the balance stops matching the shelf.
        [Fact]
        public void Consumable_decimals_are_stored_as_text_affinity()
        {
            var ctx = Read("Services", "Database", "AppDbContext.cs");

            Assert.Contains("nameof(InventoryItem.Stock)", ctx);
            Assert.Contains("nameof(InventoryItem.Used)", ctx);
            Assert.Contains("nameof(InventoryItem.PurchasePricePerUnit)", ctx);
            Assert.Contains("HasColumnType(\"TEXT\")", ctx);
        }

        // ── 6. Existing installs get the table ─────────────────────────
        //
        // EnsureCreated only ever creates a MISSING database; it never
        // alters one that exists. Without migration 1003 every install in
        // the field would query a table that is not there.
        [Fact]
        public void Consumables_table_ships_as_an_idempotent_migration()
        {
            var mig = Read("Services", "Database", "SchemaMigrations",
                           "AppSchemaMigrations.cs");

            Assert.Contains("1003", mig);
            Assert.Contains("CREATE TABLE IF NOT EXISTS InventoryItems", mig);
            Assert.Contains("IX_InventoryItem_Category", mig);

            // Must not contain the double-quoted SQL literal that a
            // verbatim string would mangle into invalid syntax.
            Assert.DoesNotContain("DEFAULT ''0''", mig);
        }

        // ── 7. Movements refuse zero instead of no-op'ing ──────────────
        //
        // The same lesson as the Buy dialog: silently doing nothing is how
        // an operator ends up believing stock was booked in.
        [Fact]
        public void Receive_and_consume_refuse_zero_loudly()
        {
            var repo = Read("Services", "SheetStore", "InventoryItemRepository.cs");

            Assert.Contains("Received quantity must be greater than zero.", repo);
            Assert.Contains("Consumed quantity must be greater than zero.", repo);

            // Over-issue is refused, not clamped.
            Assert.Contains("Not enough stock.", repo);
        }

        // ── 8. Editing cannot strand recorded consumption ──────────────
        [Fact]
        public void Stock_cannot_be_corrected_below_what_is_already_used()
        {
            var repo = Read("Services", "SheetStore", "InventoryItemRepository.cs");

            Assert.Contains("item.Stock < existing.Used", repo);
            Assert.Contains("already recorded as used", repo);
        }

        // ── 9. Writes are gated and resilient, like the glass repo ─────
        [Fact]
        public void Mutations_are_readonly_gated_and_retry_on_sqlite_contention()
        {
            var repo = Read("Services", "SheetStore", "InventoryItemRepository.cs");

            Assert.Contains("ReadOnlyGate.Demand(\"add an inventory item\")", repo);
            Assert.Contains("ReadOnlyGate.Demand(\"receive inventory stock\")", repo);
            Assert.Contains("ReadOnlyGate.Demand(\"consume inventory stock\")", repo);
            Assert.Contains("SqliteResilience.SaveWithRetryAsync", repo);
        }

        // ── 10. Low-stock is evaluated in memory, deliberately ─────────
        //
        // Balance/IsLowStock are [NotMapped] computed properties. EF cannot
        // translate them, and a wrong-but-fast SQL rewrite would silently
        // miss items — the one thing this query exists to catch.
        [Fact]
        public void Low_stock_query_is_documented_as_an_in_memory_filter()
        {
            var repo = Read("Services", "SheetStore", "InventoryItemRepository.cs");

            Assert.Contains("GetLowStockAsync", repo);
            Assert.Contains("NotMapped", repo);
            Assert.Contains("i.IsLowStock", repo);
        }

        // ── 11. Reorder level of 0 means "not tracked" ─────────────────
        //
        // Otherwise every existing row, defaulting to 0, would alarm.
        [Fact]
        public void Zero_reorder_level_disables_the_alert()
        {
            var item = Read("Models", "SheetStore", "InventoryItem.cs");

            Assert.Contains("ReorderLevel > 0 && Balance <= ReorderLevel", item);
        }

        // ── 12. Registered for DI and schema health ────────────────────
        [Fact]
        public void Repository_is_registered_and_table_is_health_checked()
        {
            var di = Read("Services", "DependencyInjection", "ServiceRegistration.cs");
            Assert.Contains("IInventoryItemRepository", di);

            var health = Read("Services", "Database", "SchemaHealthService.cs");
            Assert.Contains("InventoryItems", health);
        }
    }
}
