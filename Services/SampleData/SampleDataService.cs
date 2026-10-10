// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/SampleData/SampleDataService.cs
// PROJECT: ProGlassAutomation
// MODULE: Sample data seeding — single comprehensive workflow demo
//
// Creates ONE complete, realistic workflow that demonstrates the full
// pipeline from Inquiry → Proforma Invoice → Job Order → Delivery Note
// → Tax Invoice, all with the same data flowing through each stage.
//
//   • Inquiry          — 1 confirmed inquiry (all fields filled)
//   • Proforma Invoice — 1 approved PI with 22 glass items across 3 spec groups
//   • Job Order        — 1 released job order (from the PI)
//   • Delivery Note    — 1 partial delivery (demonstrates balance workflow)
//   • Tax Invoice      — 1 issued invoice (from the delivery)
//
// Rules:
//   • Never overwrites real data: seeding is refused when the target
//     tables already contain records.
//   • Uses the same repositories the app uses everywhere else.
//   • Logs the operation to the audit trail.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Models.ProformaInvoice.SavedInvoice;
using ProGlassAutomation.Models.SheetStore;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.DeliveryNote.Mapper;
using ProGlassAutomation.Services.JobOrder.Mapper;
using ProGlassAutomation.Services.MasterData.Database;
using ProGlassAutomation.Services.SheetStore;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Database;

using ProGlassAutomation.Services.Licensing;         // ReadOnlyGate
using ProGlassAutomation.Services.Reporting;
namespace ProGlassAutomation.Services.SampleData
{
    /// <summary>Seeds a single comprehensive demo workflow across all modules.</summary>
    public static class SampleDataService
    {
        /// <summary>Raised (UI thread) after a successful seed so open
        /// lists refresh instantly without a manual refresh.</summary>
        public static event Func<Task>? DataSeeded;

        /// <summary>
        /// Invokes every DataSeeded handler sequentially.
        /// </summary>
        private static async Task RaiseDataSeededAsync()
        {
            if (DataSeeded == null) return;

            foreach (Func<Task> handler in DataSeeded.GetInvocationList())
            {
                try { await handler(); }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ex.Message}"); }
            }
        }

        /// <summary>
        /// Returns true when any business table already has data.
        /// </summary>
        public static async Task<bool> HasExistingDataAsync()
        {
            try
            {
                var sheets = await new SheetStoreRepository().GetAllAsync();
                if (sheets.Count > 0) return true;
                var items = await new InventoryItemRepository().GetAllAsync();
                if (items.Count > 0) return true;

                if (App.Invoices != null &&
                    (await App.Invoices.GetAllAsync()).Count > 0) return true;
                if (App.Jobs != null &&
                    (await App.Jobs.GetAllAsync()).Count > 0) return true;
                if (App.DeliveryNotes != null &&
                    (await App.DeliveryNotes.GetAllAsync()).Count > 0) return true;
                if (App.TaxInvoices != null &&
                    (await App.TaxInvoices.GetAllAsync()).Count > 0) return true;
            }
            catch
            {
                return true;
            }
            return false;
        }

        /// <summary>Seeds a single comprehensive workflow. Returns a human summary.</summary>
        public static async Task<string> SeedAsync()
        {
            ReadOnlyGate.Demand("seed sample data");
            if (await HasExistingDataAsync())
                return "Sample data was NOT loaded — the database already " +
                       "contains real records. Sample seeding only runs on " +
                       "an empty database to keep your data pure.";

            var year = DateTime.Now.Year;
            var today = DateTime.Now.Date;

            // ── Common data for the entire workflow ──────────────────
            const string customerName = "Al Noor Glazing Contracting LLC";
            const string customerTrn = "100987654300003";
            const string projectName = "Al Zahia Tower — Podium Glazing";
            const string projectRef = "AZT-POD-2026";
            const string contractNo = "CTR-2026-042";
            const string lpoNumber = "LPO-AN-2026-088";
            const string siteLocation = "Al Zahia, Sharjah, UAE";
            const string salesmanName = "Ahmed Rahman";
            const string preparedBy = "Demo User";
            const string companyName = "ProGlass Automation Demo";
            const string companyTrn = "100234567800003";

            string piId = $"PI-{year}-001";
            string jobId = $"JOB-{year}-001";
            string dnId = $"DN-{year}-001-01";
            string tiId = $"INV-{year}-001";
            string inqId = $"INQ-{year}-001";

            // ── 1. Sheet Store (stock for the glass items) ───────────
            var sheets = new SheetStoreRepository();
            await sheets.AddAsync(Sheet("Clear", 6, "Clear", 2440, 1830, "Guardian Glass", 95, 150, 80, 10));
            await sheets.AddAsync(Sheet("Clear", 8, "Clear", 2440, 1830, "Guardian Glass", 120, 185, 60, 8));
            await sheets.AddAsync(Sheet("Clear", 10, "Clear", 2440, 1830, "Guardian Glass", 140, 210, 50, 6));
            await sheets.AddAsync(Sheet("Clear", 12, "Clear", 2440, 1830, "Guardian Glass", 165, 245, 40, 4));
            await sheets.AddAsync(Sheet("Tinted", 6, "Bronze", 2440, 1830, "Al Gurg Glass", 110, 175, 55, 5));
            await sheets.AddAsync(Sheet("Tinted", 8, "Grey", 2440, 1830, "Al Gurg Glass", 125, 195, 45, 3));
            await sheets.AddAsync(Sheet("Low-E", 6, "Clear", 2440, 1830, "Saint-Gobain", 180, 260, 35, 2));
            await sheets.AddAsync(Sheet("Frosted", 5, "Frosted", 2200, 1650, "National Glass", 130, 200, 30, 2));

            // ── 1b. Consumables (the non-glass side of an IGU) ───────
            var items = new InventoryItemRepository();
            foreach (var it in new[]
            {
                Item("Warm-edge spacer bar", InventoryCategory.SpacerBar,
                     StockUom.LinearMetre, "12mm black", "SPB-12-BLK",
                     "Technoform Gulf", 4.20m, 6.50m, 2400, 780, 400),
                Item("Warm-edge spacer bar", InventoryCategory.SpacerBar,
                     StockUom.LinearMetre, "16mm black", "SPB-16-BLK",
                     "Technoform Gulf", 4.80m, 7.20m, 1800, 640, 400),
                Item("Aluminium spacer bar", InventoryCategory.SpacerBar,
                     StockUom.LinearMetre, "12mm mill", "SPA-12-ML",
                     "Al Gurg Glass", 2.90m, 4.60m, 3200, 1150, 500),
                Item("Molecular sieve desiccant", InventoryCategory.Desiccant,
                     StockUom.Kilogram, "3A beads", "DES-3A",
                     "Grace Davison", 12.50m, 19.00m, 240, 96, 60),
                Item("Polyisobutylene (PIB) butyl", InventoryCategory.PrimarySeal,
                     StockUom.Kilogram, "Hot-melt primary seal", "PIB-STD",
                     "Fenzi Gulf", 22.00m, 33.00m, 180, 74, 45),
                Item("Two-part silicone sealant", InventoryCategory.SecondarySeal,
                     StockUom.Kilogram, "Structural grade", "SIL-2K",
                     "Dow Chemical", 28.50m, 42.00m, 320, 128, 80),
                Item("Polysulphide sealant", InventoryCategory.SecondarySeal,
                     StockUom.Kilogram, "Secondary seal", "PSF-STD",
                     "Fenzi Gulf", 19.75m, 30.00m, 150, 122, 40),
                Item("Argon gas", InventoryCategory.Gas,
                     StockUom.CubicMetre, "99.9% purity", "GAS-AR-999",
                     "Air Liquide", 8.40m, 13.00m, 500, 210, 120),
                Item("Spacer corner key", InventoryCategory.Hardware,
                     StockUom.Piece, "12mm straight", "CK-12",
                     "Technoform Gulf", 0.35m, 0.70m, 5000, 1800, 1000),
                Item("Spacer bar connector", InventoryCategory.Hardware,
                     StockUom.Piece, "16mm inline joiner", "CN-16",
                     "Technoform Gulf", 0.48m, 0.95m, 3000, 1120, 800),
            })
            {
                await items.AddAsync(it);
            }

            // ── 2. Inquiry (starting point of the workflow) ──────────
            try
            {
                var demoInquiry = new ProGlassAutomation.Models.Inquiry.InquiryRecord
                {
                    InquiryNumber = inqId,
                    InquiryReference = "Podium facade glazing quotation",
                    CustomerName = customerName,
                    CustomerLocation = "Sharjah, UAE",
                    ProjectName = projectName,
                    ProjectDescription =
                        "Supply and installation of clear tempered, tinted, and Low-E glass " +
                        "for the tower podium facade. Includes 22 glass panels across 3 specification " +
                        "groups: clear tempered for main facade, tinted for sun-exposed areas, " +
                        "and Low-E for energy-efficient zones.",
                    SalesmanName = salesmanName,
                    Status = ProGlassAutomation.Models.Inquiry.InquiryStatuses.Confirmed,
                    Priority = "High",
                    InquiryValue = 87_450m,
                    ReceivedAt = today.AddDays(-14),
                    PreparedAt = today.AddDays(-12),
                    Notes = "Client confirmed. Proceed with proforma invoice preparation. " +
                            "Site survey completed. Measurements verified by site engineer. " +
                            "Delivery required in 2 batches — podium level access via loading bay.",
                };
                await App.Inquiries.CreateAsync(demoInquiry);
            }
            catch (Exception ex)
            {
                AppLogService.Debug("[SampleData] Demo inquiry seed skipped: " + ex.Message);
            }

            // ── 3. Proforma Invoice (with 22 items in 3 spec groups) ─
            var pi = BuildComprehensivePi(
                piId, today.AddDays(-10),
                customerName, customerTrn,
                projectName, projectRef, contractNo, lpoNumber, siteLocation,
                salesmanName, preparedBy, companyName, companyTrn);
            await App.Invoices.InsertAsync(pi);

            // ── 4. Job Order (from the PI) ───────────────────────────
            var (jo, joSnap) = BuildJobOrder(pi, jobId, "Released", "High", today.AddDays(14));
            await App.Jobs.InsertAsync(jo);

            // ── 5. Delivery Note (partial delivery — 15 of 22 items) ─
            var dn = BuildDeliveryNote(
                jo, joSnap, dnId, 1,
                "Dispatching", today,
                "Salim Khan", "SHJ-48211", "Mohammed Ali (Site Storekeeper)");
            await App.DeliveryNotes.InsertAsync(dn);

            // ── 6. Tax Invoice (from the delivery) ───────────────────
            var ti = BuildTaxInvoice(
                tiId, piId, dnId,
                customerName, customerTrn,
                projectName, companyName, companyTrn,
                "Issued", today, today.AddDays(30));
            await App.TaxInvoices.InsertAsync(ti);

            AuditService.Log(AuditSections.Settings,
                "Sample data loaded",
                "1 complete workflow seeded: Inquiry → PI (22 items) → JO → DN → TI");

            await RaiseDataSeededAsync();

            return "Sample data loaded ✔\n\n" +
                   "ONE complete workflow demonstrating the full pipeline:\n\n" +
                   $"  📋 Inquiry:        {inqId} (Confirmed)\n" +
                   $"  📄 Proforma:       {piId} (Approved, 22 glass items)\n" +
                   $"  🔧 Job Order:      {jobId} (Released)\n" +
                   $"  🚚 Delivery Note:  {dnId} (Partial delivery)\n" +
                   $"  💰 Tax Invoice:    {tiId} (Issued)\n\n" +
                   "All documents are linked and share the same data.\n" +
                   "Open any document to see the full detail.\n" +
                   "The delivery note demonstrates partial delivery —\n" +
                   "click 'CREATE DN' again to see the balance workflow.";
        }

        /// <summary>
        /// Removes EVERY record across all business modules.
        /// </summary>
        public static async Task<string> ClearAllDataAsync()
        {
            ReadOnlyGate.Demand("clear all data");
            int ti = 0, dn = 0, jo = 0, pi = 0, sh = 0, inq = 0, ci = 0;

            try
            {
                // Tax Invoices
                if (App.TaxInvoices != null)
                {
                    foreach (var x in await App.TaxInvoices.GetAllAsync())
                    {
                        await App.TaxInvoices.DeleteAsync(x.Id, x.RowVersion);
                        ti++;
                    }
                }

                // Delivery Notes
                if (App.DeliveryNotes != null)
                {
                    foreach (var x in await App.DeliveryNotes.GetAllAsync())
                    {
                        await App.DeliveryNotes.DeleteAsync(x.Id);
                        dn++;
                    }
                    foreach (var x in await App.DeliveryNotes.GetMasterDnsAsync())
                    {
                        await App.DeliveryNotes.DeleteAsync(x.Id);
                        dn++;
                    }
                }

                // Job Orders
                if (App.Jobs != null)
                {
                    foreach (var x in await App.Jobs.GetAllAsync())
                    {
                        await App.Jobs.DeleteAsync(x.Id);
                        jo++;
                    }
                }

                // Proforma Invoices
                if (App.Invoices != null)
                {
                    foreach (var x in await App.Invoices.GetAllAsync())
                    {
                        await App.Invoices.DeleteAsync(x.Id, x.RowVersion);
                        pi++;
                    }
                }

                // Inquiries
                try
                {
                    if (App.Inquiries != null)
                    {
                        var inqResult = await App.Inquiries.QueryAsync(
                            new ProGlassAutomation.Services.Inquiry.InquiryQuery { PageSize = 1000 });
                        foreach (var x in inqResult.Items)
                        {
                            await App.Inquiries.DeleteAsync(x.Id);
                            inq++;
                        }
                    }
                }
                catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("foreach suppressed");
            }

                // Sheet Store
                var sheetRepo = new SheetStoreRepository();
                foreach (var x in await sheetRepo.GetAllAsync())
                {
                    await sheetRepo.DeleteAsync(x.Id);
                    sh++;
                }

                var itemRepo = new InventoryItemRepository();
                foreach (var x in await itemRepo.GetAllAsync())
                {
                    await itemRepo.DeleteAsync(x.Id, x.RowVersion);
                    ci++;
                }
            }
            catch (Exception ex)
            {
                AuditService.Log(AuditSections.Settings,
                    "Clear all data failed", ex.Message);
                return "Could not clear all data: " + ex.Message;
            }

            AuditService.Log(AuditSections.Settings,
                "All data cleared",
                $"Removed {sh} sheets, {ci} consumables, {pi} PIs, {jo} JOs, {dn} DNs, {ti} TIs, {inq} inquiries");

            await RaiseDataSeededAsync();

            return "All data cleared ✔\n\n" +
                   $"• Inquiries: {inq} removed\n" +
                   $"• Sheet Store: {sh} sheet(s) removed\n" +
                   $"• Consumables: {ci} item(s) removed\n" +
                   $"• Proforma Invoices: {pi} removed\n" +
                   $"• Job Orders: {jo} removed\n" +
                   $"• Delivery Notes: {dn} removed (incl. master)\n" +
                   $"• Tax Invoices: {ti} removed\n\n" +
                   "The database is empty and ready for real data.";
        }

        /// <summary>
        /// Inserts company / client / project / salesman cards when those
        /// tables are empty.
        /// </summary>
        public static async Task SeedMasterProfilesAsync()
        {
            ReadOnlyGate.Demand("seed master profiles");
            using var db = new MasterDataDbContext();
            await db.Database.EnsureCreatedAsync();

            var now = DateTime.Now;
            bool added = false;

            if (!await db.Companies.AnyAsync())
            {
                db.Companies.Add(new SavedCompany
                {
                    CompanyName = "ProGlass Automation Demo",
                    CompanyTrn = "100234567800003",
                    CompanyLocation = "Sharjah, UAE",
                    Representative = "Demo User",
                    IsDefault = true,
                    UsageCount = 4,
                    CreatedAt = now,
                    LastUsedAt = now
                });
                added = true;
            }

            if (!await db.Clients.AnyAsync())
            {
                db.Clients.Add(new SavedClient
                {
                    CustomerName = "Al Noor Glazing Contracting LLC",
                    CustomerTrn = "100987654300003",
                    BillingAddress = "PO Box 45678, Sharjah, UAE",
                    SalesRepresentative = "Ahmed Rahman",
                    DocumentPreparedBy = "Demo User",
                    ClientContactPhone = "+971 50 987 6543",
                    UsageCount = 4,
                    CreatedAt = now,
                    LastUsedAt = now
                });
                added = true;
            }

            if (!await db.Projects.AnyAsync())
            {
                db.Projects.Add(new SavedProject
                {
                    ProjectIdentifierName = "Al Zahia Tower — Podium Glazing",
                    ProjectContractNo = "CTR-2026-042",
                    ProjectReference = "AZT-POD-2026",
                    SiteDeliveryLocation = "Al Zahia, Sharjah, UAE",
                    LpoNumber = "LPO-AN-2026-088",
                    CustomerName = "Al Noor Glazing Contracting LLC",
                    UsageCount = 4,
                    CreatedAt = now,
                    LastUsedAt = now
                });
                added = true;
            }

            if (!await db.Salesmen.AnyAsync())
            {
                db.Salesmen.Add(new SavedSalesman
                {
                    Name = "Ahmed Rahman",
                    Department = "Sales",
                    UsageCount = 4,
                    CreatedAt = now,
                    LastUsedAt = now
                });
                added = true;
            }

            if (added)
                await SqliteResilience.SaveWithRetryAsync(_ => db.SaveChangesAsync());
        }

        // ── builders ──────────────────────────────────────────────────

        private static InventoryItem Item(
            string name, InventoryCategory category, StockUom uom,
            string spec, string partNumber, string supplier,
            decimal buy, decimal sell,
            decimal stock, decimal used, decimal reorder) => new InventoryItem
        {
            Name = name,
            Category = category,
            Uom = uom,
            Specification = spec,
            PartNumber = partNumber,
            Supplier = supplier,
            PurchasePricePerUnit = buy,
            SellPricePerUnit = sell,
            Stock = stock,
            Used = used,
            ReorderLevel = reorder,
            CreatedAt = DateTime.Now,
            LastUpdated = DateTime.Now
        };

        private static GlassSheet Sheet(string cat, int th, string color,
            int w, int h, string supplier, decimal buy, decimal sell,
            int stock, int used) => new GlassSheet
        {
            Category = cat, ThicknessMm = th, Color = color,
            WidthMm = w, HeightMm = h, Supplier = supplier,
            PurchasePricePerSqm = buy, SellPricePerSqm = sell,
            Stock = stock, Used = used
        };

        /// <summary>
        /// Builds a comprehensive Proforma Invoice with 22 glass items
        /// across 3 specification groups.
        /// </summary>
        private static SavedProformaInvoice BuildComprehensivePi(
            string id, DateTime issue,
            string customerName, string customerTrn,
            string projectName, string projectRef, string contractNo,
            string lpoNumber, string siteLocation,
            string salesmanName, string preparedBy,
            string companyName, string companyTrn)
        {
            var snapshot = new ProformaInvoiceSnapshot
            {
                Company = new CompanySnapshot
                {
                    CompanyName = companyName,
                    CompanyTrn = companyTrn,
                    CompanyLocation = "Sharjah, UAE",
                    ProformaId = id,
                    IssueDate = issue,
                    ValidUntil = issue.AddDays(30)
                },
                Client = new ClientSnapshot
                {
                    CustomerName = customerName,
                    CustomerTrn = customerTrn,
                    BillingAddress = "PO Box 45678, Sharjah, UAE",
                    SalesRepresentative = salesmanName,
                    DocumentPreparedBy = preparedBy,
                    ClientContactPhone = "+971 50 987 6543"
                },
                Project = new ProjectSnapshot
                {
                    ProjectIdentifierName = projectName,
                    ProjectContractNo = contractNo,
                    ProjectReference = projectRef,
                    SiteDeliveryLocation = siteLocation,
                    LpoNumber = lpoNumber
                },
                SpecGroups =
                {
                    // ── Spec Group 1: Clear Tempered (10 items) ──────
                    new SpecGroupSnapshot
                    {
                        GroupNumber = 1,
                        GroupName = "Clear Tempered — Main Facade",
                        SelectedGlassType = "SGU",
                        BaseRate = 150m,
                        ItemRows =
                        {
                            new GlassItemRowSnapshot { SerialNumber = 1, GlassRef = "CT-06-1200x900", W1 = 1200, H1 = 900, W2 = 0, H2 = 0, Qty = 8, Rate = 150m },
                            new GlassItemRowSnapshot { SerialNumber = 2, GlassRef = "CT-06-1500x1000", W1 = 1500, H1 = 1000, W2 = 0, H2 = 0, Qty = 6, Rate = 150m },
                            new GlassItemRowSnapshot { SerialNumber = 3, GlassRef = "CT-08-1800x1200", W1 = 1800, H1 = 1200, W2 = 0, H2 = 0, Qty = 5, Rate = 185m },
                            new GlassItemRowSnapshot { SerialNumber = 4, GlassRef = "CT-08-2000x1500", W1 = 2000, H1 = 1500, W2 = 0, H2 = 0, Qty = 4, Rate = 185m },
                            new GlassItemRowSnapshot { SerialNumber = 5, GlassRef = "CT-10-2200x1400", W1 = 2200, H1 = 1400, W2 = 0, H2 = 0, Qty = 3, Rate = 210m },
                            new GlassItemRowSnapshot { SerialNumber = 6, GlassRef = "CT-10-2400x1600", W1 = 2400, H1 = 1600, W2 = 0, H2 = 0, Qty = 3, Rate = 210m },
                            new GlassItemRowSnapshot { SerialNumber = 7, GlassRef = "CT-12-1800x1200", W1 = 1800, H1 = 1200, W2 = 0, H2 = 0, Qty = 4, Rate = 245m },
                            new GlassItemRowSnapshot { SerialNumber = 8, GlassRef = "CT-12-2000x1500", W1 = 2000, H1 = 1500, W2 = 0, H2 = 0, Qty = 2, Rate = 245m },
                            new GlassItemRowSnapshot { SerialNumber = 9, GlassRef = "CT-06-1000x800", W1 = 1000, H1 = 800, W2 = 0, H2 = 0, Qty = 6, Rate = 150m },
                            new GlassItemRowSnapshot { SerialNumber = 10, GlassRef = "CT-08-1600x1100", W1 = 1600, H1 = 1100, W2 = 0, H2 = 0, Qty = 4, Rate = 185m },
                        }
                    },
                    // ── Spec Group 2: Tinted Glass (7 items) ─────────
                    new SpecGroupSnapshot
                    {
                        GroupNumber = 2,
                        GroupName = "Tinted — Sun-Exposed Areas",
                        SelectedGlassType = "SGU",
                        BaseRate = 175m,
                        ItemRows =
                        {
                            new GlassItemRowSnapshot { SerialNumber = 1, GlassRef = "TT-06-1200x900", W1 = 1200, H1 = 900, W2 = 0, H2 = 0, Qty = 5, Rate = 175m },
                            new GlassItemRowSnapshot { SerialNumber = 2, GlassRef = "TT-06-1500x1000", W1 = 1500, H1 = 1000, W2 = 0, H2 = 0, Qty = 4, Rate = 175m },
                            new GlassItemRowSnapshot { SerialNumber = 3, GlassRef = "TT-08-1800x1200", W1 = 1800, H1 = 1200, W2 = 0, H2 = 0, Qty = 3, Rate = 195m },
                            new GlassItemRowSnapshot { SerialNumber = 4, GlassRef = "TT-08-2000x1500", W1 = 2000, H1 = 1500, W2 = 0, H2 = 0, Qty = 2, Rate = 195m },
                            new GlassItemRowSnapshot { SerialNumber = 5, GlassRef = "TT-06-1000x800", W1 = 1000, H1 = 800, W2 = 0, H2 = 0, Qty = 4, Rate = 175m },
                            new GlassItemRowSnapshot { SerialNumber = 6, GlassRef = "TT-08-1600x1100", W1 = 1600, H1 = 1100, W2 = 0, H2 = 0, Qty = 3, Rate = 195m },
                            new GlassItemRowSnapshot { SerialNumber = 7, GlassRef = "TT-06-1400x950", W1 = 1400, H1 = 950, W2 = 0, H2 = 0, Qty = 3, Rate = 175m },
                        }
                    },
                    // ── Spec Group 3: Low-E Energy Efficient (5 items) ─
                    new SpecGroupSnapshot
                    {
                        GroupNumber = 3,
                        GroupName = "Low-E — Energy Efficient Zones",
                        SelectedGlassType = "DGU",
                        BaseRate = 260m,
                        ItemRows =
                        {
                            new GlassItemRowSnapshot { SerialNumber = 1, GlassRef = "LE-06-1800x1200", W1 = 1800, H1 = 1200, W2 = 1800, H2 = 1200, Qty = 4, Rate = 260m },
                            new GlassItemRowSnapshot { SerialNumber = 2, GlassRef = "LE-06-2000x1500", W1 = 2000, H1 = 1500, W2 = 2000, H2 = 1500, Qty = 3, Rate = 260m },
                            new GlassItemRowSnapshot { SerialNumber = 3, GlassRef = "LE-06-2200x1400", W1 = 2200, H1 = 1400, W2 = 2200, H2 = 1400, Qty = 2, Rate = 260m },
                            new GlassItemRowSnapshot { SerialNumber = 4, GlassRef = "LE-06-1600x1100", W1 = 1600, H1 = 1100, W2 = 1600, H2 = 1100, Qty = 3, Rate = 260m },
                            new GlassItemRowSnapshot { SerialNumber = 5, GlassRef = "LE-06-2400x1600", W1 = 2400, H1 = 1600, W2 = 2400, H2 = 1600, Qty = 2, Rate = 260m },
                        }
                    },
                },
                Summary = new InvoiceSummarySnapshot
                {
                    DiscountType = "Percentage",
                    DiscountValue = 0m,
                    IsVatApplicable = true,
                    VatPercent = 5m
                }
            };

            // Calculate totals
            decimal subtotal = 0;
            foreach (var sg in snapshot.SpecGroups)
            {
                foreach (var item in sg.ItemRows)
                {
                    decimal sqm = ((decimal)item.W1 / 1000m) * ((decimal)item.H1 / 1000m) * item.Qty;
                    subtotal += MoneyRounding.Round(sqm * item.Rate);
                }
            }

            decimal discount = 0m;
            decimal afterDiscount = subtotal - discount;
            decimal vat = Reporting.VatCalculator.CalculateVatFromSubtotal(afterDiscount);
            decimal grandTotal = afterDiscount + vat;

            return new SavedProformaInvoice
            {
                ProformaId = id,
                CompanyName = companyName,
                CustomerName = customerName,
                ProjectName = projectName,
                Status = "Approved",
                IssueDate = issue,
                ValidUntil = issue.AddDays(30),
                GlassMaterialsSubtotal = subtotal,
                OtherChargesSubtotal = 0m,
                DiscountAmount = discount,
                VatAmount = vat,
                GrandTotalVatInclusive = grandTotal,
                FullSnapshotJson = JsonSerializer.Serialize(snapshot),
                CreatedAt = issue,
                UpdatedAt = issue
            };
        }

        private static (SavedJobOrder Entity, Models.JobOrder.SavedJobOrder.JobOrderSnapshot Snapshot)
            BuildJobOrder(SavedProformaInvoice pi, string jobId, string status,
                string priority, DateTime? delivery)
        {
            var snap = JobOrderMapper.BuildFromProformaInvoice(pi);
            snap.JobOrderId = jobId;
            snap.Production ??= new Models.JobOrder.ProductionDetails.ProductionDetails();
            snap.Production.Status = status;
            snap.Production.Priority = priority;
            snap.Production.DeliveryDate = delivery;
            snap.Production.InternalNotes = "Podium level delivery — use loading bay B2. " +
                                     "Site contact: Mohammed Ali +971 55 123 4567. " +
                                     "Deliver in 2 batches: Batch 1 = Clear Tempered (Group 1), " +
                                     "Batch 2 = Tinted + Low-E (Groups 2 & 3).";
            var entity = JobOrderMapper.BuildEntityFromSnapshot(
                snap, pi.GrandTotalVatInclusive);
            entity.Status = status;
            entity.Priority = priority;
            entity.DeliveryDate = delivery;
            return (entity, snap);
        }

        /// <summary>
        /// Builds a delivery note with partial delivery.
        /// Delivers all of Spec Group 1 (Clear Tempered — 10 items)
        /// and nothing from Groups 2 & 3 (leaving balance for next DN).
        /// </summary>
        private static SavedDeliveryNote BuildDeliveryNote(
            SavedJobOrder job,
            Models.JobOrder.SavedJobOrder.JobOrderSnapshot snap,
            string dnId, int seq, string status,
            DateTime date, string driver, string vehicle, string receiver)
        {
            var dn = DeliveryNoteMapper.FromJobOrder(job, snap, dnId, seq);
            dn.Status = status;
            dn.DeliveryDate = date;
            dn.DeliveryTime = date.ToString("HH:mm");
            dn.DriverName = driver;
            dn.VehicleNumber = vehicle;
            dn.ReceiverName = receiver;
            dn.Notes = "Batch 1 of 2 — Clear Tempered panels only. " +
                       "Tinted and Low-E to follow in Batch 2.";

            // Deliver all items from Spec Group 1 (first 10 items)
            // Leave Spec Groups 2 & 3 with 0 delivered (balance = ordered)
            // Also set realistic WeightPerPiece for each item (glass weight = area × thickness × 2.5 kg/m²/mm)
            int itemIndex = 0;
            foreach (var item in dn.Items)
            {
                // Calculate realistic weight: area(m²) × thickness(mm) × 2.5 kg/m²/mm
                decimal areaM2 = (decimal)item.W1 / 1000m * (decimal)item.H1 / 1000m;
                decimal thicknessMm = itemIndex < 10
                    ? GetThicknessFromRef(item.GlassRef, "CT")  // Clear Tempered
                    : itemIndex < 17
                        ? GetThicknessFromRef(item.GlassRef, "TT")  // Tinted
                        : GetThicknessFromRef(item.GlassRef, "LE"); // Low-E (DGU = double)
                item.WeightPerPiece = Math.Round(areaM2 * thicknessMm * 2.5m, 1);

                if (itemIndex < 10) // Spec Group 1: deliver all
                {
                    item.DeliveredQty = item.OrderedQty;
                    item.BalanceQty = 0;
                }
                else // Spec Groups 2 & 3: deliver nothing (partial delivery demo)
                {
                    item.DeliveredQty = 0;
                    item.BalanceQty = item.OrderedQty;
                }
                itemIndex++;
            }

            dn.TotalDeliveredQty = dn.Items.Sum(i => i.DeliveredQty);
            dn.TotalBalanceQty = dn.Items.Sum(i => i.BalanceQty);
            dn.TotalPreviouslyDeliveredQty = dn.Items.Sum(i => i.PreviouslyDeliveredQty);
            dn.TotalOrderedQty = dn.Items.Sum(i => i.OrderedQty);
            DeliveryNoteMapper.RefreshSnapshot(dn);
            return dn;
        }

        /// <summary>
        /// Extracts glass thickness in mm from the GlassRef string.
        /// E.g., "CT-06-1200x900" → 6mm, "CT-12-2000x1500" → 12mm.
        /// For DGU (Low-E), doubles the thickness (two panes + air gap).
        /// </summary>
        private static decimal GetThicknessFromRef(string glassRef, string type)
        {
            // GlassRef format: "XX-TT-WWWxHHH" where TT = thickness
            if (string.IsNullOrEmpty(glassRef)) return 6m;
            var parts = glassRef.Split('-');
            if (parts.Length >= 2 && decimal.TryParse(parts[1], out decimal thickness))
            {
                // DGU (double glazing unit) = 2× glass + 12mm air gap ≈ 2× + 12
                return type == "LE" ? (thickness * 2 + 12) : thickness;
            }
            return type == "LE" ? 24m : 6m; // defaults
        }

        private static SavedTaxInvoice BuildTaxInvoice(
            string num, string piId, string dnId,
            string customerName, string customerTrn,
            string projectName, string companyName, string companyTrn,
            string status, DateTime issue, DateTime due)
        {
            // Build line items from delivered Spec Group 1 items
            var items = new List<TaxInvoiceItem>();
            
            // Spec Group 1: Clear Tempered — Main Facade (10 items, all delivered)
            var sg1Data = new[]
            {
                (1, "CT-06-1200x900", 1200, 900, 8, 150m),
                (2, "CT-06-1500x1000", 1500, 1000, 6, 150m),
                (3, "CT-08-1800x1200", 1800, 1200, 5, 185m),
                (4, "CT-08-2000x1500", 2000, 1500, 4, 185m),
                (5, "CT-10-2200x1400", 2200, 1400, 3, 210m),
                (6, "CT-10-2400x1600", 2400, 1600, 3, 210m),
                (7, "CT-12-1800x1200", 1800, 1200, 4, 245m),
                (8, "CT-12-2000x1500", 2000, 1500, 2, 245m),
                (9, "CT-06-1000x800", 1000, 800, 6, 150m),
                (10, "CT-08-1600x1100", 1600, 1100, 4, 185m),
            };

            foreach (var (serial, glassRef, w1, h1, qty, rate) in sg1Data)
            {
                decimal sqm = Math.Max(0.5m, (decimal)w1 / 1000m * (decimal)h1 / 1000m);
                decimal totalSqm = sqm * qty;
                decimal value = rate * totalSqm;
                
                items.Add(new TaxInvoiceItem
                {
                    InvoiceNumber = num,
                    ProformaId = piId,
                    SpecGroupNumber = 1,
                    SpecGroupName = "Clear Tempered — Main Facade",
                    GlassType = "SGU",
                    SerialNumber = serial,
                    GlassRef = glassRef,
                    W1 = w1, H1 = h1, W2 = 0, H2 = 0,
                    Qty = qty,
                    Sqm = Math.Round(sqm, 3),
                    TotalSqm = Math.Round(totalSqm, 3),
                    Rate = rate,
                    Value = MoneyRounding.Round(value),
                    Notes = ""
                });
            }

            decimal deliveredSubtotal = items.Sum(i => i.Value);
            deliveredSubtotal = MoneyRounding.Round(deliveredSubtotal);
            decimal vat = Reporting.VatCalculator.CalculateVatFromSubtotal(deliveredSubtotal);
            decimal total = deliveredSubtotal + vat;

            return new SavedTaxInvoice
            {
                InvoiceNumber = num,
                ProformaId = piId,
                DeliveryNoteReference = dnId,
                Status = status,
                CustomerName = customerName,
                ProjectName = projectName,
                CompanyName = companyName,
                CompanyTrn = companyTrn,
                IssueDate = issue,
                DueDate = due,
                PaymentTerms = "Net 30",
                PaymentTermsDays = 30,
                Subtotal = deliveredSubtotal,
                VatAmount = vat,
                GrandTotal = total,
                Items = items
            };
        }
    }
}
