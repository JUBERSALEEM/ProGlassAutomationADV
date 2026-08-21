// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/SampleData/SampleDataService.cs
// PROJECT: ProGlassAutomation
// MODULE: Sample data seeding — first-run learning aid
//
// Fills EVERY module with a small, internally consistent demo dataset so
// a fresh install can be explored immediately:
//   • Sheet Store   — 6 glass sheets (stock/used/prices)
//   • Proforma      — 3 invoices (Approved / Sent / Draft) with full
//                     snapshots (openable in the wizard & printable)
//   • Job Order     — 2 jobs linked to the PIs (Released / InProduction)
//   • Delivery Note — 2 DNs linked to the JOs (Delivered / Draft)
//   • Tax Invoice   — 3 TIs (Paid / Issued / Overdue for aging views)
//
// Rules:
//   • Never overwrites real data: seeding is refused when the target
//     tables already contain records (or the user picks "add anyway"
//     with SAMPLE- prefixed ids — not implemented: refuse only).
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
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.SampleData
{
    /// <summary>Seeds a consistent demo dataset across all modules.</summary>
    public static class SampleDataService
    {
        /// <summary>Raised (UI thread) after a successful seed so open
        /// lists refresh instantly without a manual refresh.</summary>
        /// <remarks>
        /// Func&lt;Task&gt; rather than Action: every subscriber is an async
        /// reload (the ViewModels assign `async () => await LoadAsync()`),
        /// so the event must carry Task-returning delegates (CS0029).
        /// Handlers are awaited one at a time; a failing handler is logged
        /// and must not break the others.
        /// </remarks>
        public static event Func<Task>? DataSeeded;

        /// <summary>
        /// Invokes every DataSeeded handler sequentially. A throwing
        /// handler is logged and never prevents the others from running.
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
        /// Returns true when any business table already has data
        /// (seeding would mix demo with real records — refuse).
        /// </summary>
        public static async Task<bool> HasExistingDataAsync()
        {
            try
            {
                var sheets = await new SheetStoreRepository().GetAllAsync();
                if (sheets.Count > 0) return true;

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
                return true; // on any doubt, refuse
            }
            return false;
        }

        /// <summary>Seeds the demo dataset. Returns a human summary.</summary>
        public static async Task<string> SeedAsync()
        {
            if (await HasExistingDataAsync())
                return "Sample data was NOT loaded — the database already " +
                       "contains real records. Sample seeding only runs on " +
                       "an empty database to keep your data pure.";

            var year = DateTime.Now.Year;
            var today = DateTime.Now.Date;

            // ── 1. Sheet Store ────────────────────────────────────────
            var sheets = new SheetStoreRepository();
            await sheets.AddAsync(Sheet("Clear", 6, "Clear", 2440, 1830, "Guardian Glass", 95, 150, 40, 6));
            await sheets.AddAsync(Sheet("Clear", 10, "Clear", 2440, 1830, "Guardian Glass", 140, 210, 25, 4));
            await sheets.AddAsync(Sheet("Tinted", 6, "Bronze", 2440, 1830, "Al Gurg Glass", 110, 175, 30, 5));
            await sheets.AddAsync(Sheet("Frosted", 5, "Frosted", 2200, 1650, "National Glass", 130, 200, 18, 2));
            await sheets.AddAsync(Sheet("Low-E", 6, "Clear", 2440, 1830, "Saint-Gobain", 180, 260, 22, 3));
            await sheets.AddAsync(Sheet("Mirror", 4, "Silver", 1830, 1220, "Al Gurg Glass", 90, 145, 15, 1));

            // ── 2. Proforma Invoices (with full snapshots) ───────────
            var pi1 = Pi($"PI-{year}-001", "Approved", today.AddDays(-20),
                "Skyline Interiors LLC", "Marina Tower — Fit-Out", 12_450m);
            var pi2 = Pi($"PI-{year}-002", "Sent", today.AddDays(-6),
                "Desert Rose Contracting", "Palm Villa 17", 6_820m);
            var pi3 = Pi($"PI-{year}-003", "Draft", today.AddDays(-1),
                "Al Noor Glass Trading", "Showroom Partition Works", 3_140m);
            await App.Invoices.InsertAsync(pi1);
            await App.Invoices.InsertAsync(pi2);
            await App.Invoices.InsertAsync(pi3);

            // ── 3. Job Orders (real snapshots from the PIs) ───────────
            var jo1 = BuildJob(pi1, $"JOB-{year}-001", "Released", "High", today.AddDays(6));
            var jo2 = BuildJob(pi2, $"JOB-{year}-002", "InProduction", "High", today.AddDays(12));
            await App.Jobs.InsertAsync(jo1.Entity);
            await App.Jobs.InsertAsync(jo2.Entity);

            // ── 4. Delivery Notes (items + Prev/This/Balance) ─────────
            await App.DeliveryNotes.InsertAsync(BuildDn(
                jo1.Entity, jo1.Snapshot, $"DN-{year}-001-01", 1,
                "Delivered", deliverAll: true, today.AddDays(-4),
                "Salim Khan", "SHJ-48211", "Site storekeeper"));
            await App.DeliveryNotes.InsertAsync(BuildDn(
                jo2.Entity, jo2.Snapshot, $"DN-{year}-002-01", 1,
                "Dispatching", deliverAll: false, today,
                "", "", ""));

            // ── 5. Tax Invoices ───────────────────────────────────────
            await App.TaxInvoices.InsertAsync(Ti($"INV-{year}-001",
                $"PI-{year}-001", $"DN-{year}-001-01",
                "Skyline Interiors LLC", "Paid", 12_450m,
                today.AddDays(-3), today.AddDays(27)));
            await App.TaxInvoices.InsertAsync(Ti($"INV-{year}-002",
                $"PI-{year}-002", "",
                "Desert Rose Contracting", "Issued", 6_820m,
                today.AddDays(-2), today.AddDays(28)));
            await App.TaxInvoices.InsertAsync(Ti($"INV-{year}-003",
                $"PI-{year}-001", "",
                "Skyline Interiors LLC", "Overdue", 4_300m,
                today.AddDays(-45), today.AddDays(-15)));

            AuditService.Log(AuditSections.Settings,
                "Sample data loaded",
                "6 sheets, 3 PIs, 2 JOs, 2 DNs, 3 TIs seeded for first-run exploration");

            await RaiseDataSeededAsync();

            return "Sample data loaded ✔\n\n" +
                   "• Sheet Store: 6 glass sheets\n" +
                   "• Proforma Invoices: 3 (Approved / Sent / Draft)\n" +
                   "• Job Orders: 2 (Released / In Production)\n" +
                   "• Delivery Notes: 2 (Delivered / Draft)\n" +
                   "• Tax Invoices: 3 (Paid / Issued / Overdue)\n\n" +
                   "Every document is linked (PI → JO → DN → TI) and " +
                   "opens with full detail. Explore freely — or delete " +
                   "records individually when done.";
        }

        /// <summary>
        /// Removes EVERY record across all business modules (sheets,
        /// proformas, job orders, delivery notes — including Master DNs —
        /// and tax invoices). Intended for wiping the demo dataset after
        /// evaluation so users can start entering real data on a clean
        /// database without deleting records one by one.
        /// Caller MUST confirm with the user before invoking this.
        /// </summary>
        public static async Task<string> ClearAllDataAsync()
        {
            int ti = 0, dn = 0, jo = 0, pi = 0, sh = 0;

            try
            {
                // Tax Invoices
                if (App.TaxInvoices != null)
                {
                    foreach (var x in await App.TaxInvoices.GetAllAsync())
                    {
                        await App.TaxInvoices.DeleteAsync(x.Id);
                        ti++;
                    }
                }

                // Delivery Notes — GetAllAsync excludes Master DNs,
                // so Master DNs are cleared through their own query.
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
                        await App.Invoices.DeleteAsync(x.Id);
                        pi++;
                    }
                }

                // Sheet Store
                var sheets = new SheetStoreRepository();
                foreach (var x in await sheets.GetAllAsync())
                {
                    await sheets.DeleteAsync(x.Id);
                    sh++;
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
                $"Removed {sh} sheets, {pi} PIs, {jo} JOs, {dn} DNs, {ti} TIs");

            // Re-uses the seed event on purpose: every subscribed list
            // reloads and shows the now-empty database immediately.
            await RaiseDataSeededAsync();

            return "All data cleared ✔\n\n" +
                   $"• Sheet Store: {sh} sheet(s) removed\n" +
                   $"• Proforma Invoices: {pi} removed\n" +
                   $"• Job Orders: {jo} removed\n" +
                   $"• Delivery Notes: {dn} removed (incl. master)\n" +
                   $"• Tax Invoices: {ti} removed\n\n" +
                   "The database is empty and ready for real data.";
        }

        /// <summary>
        /// Inserts company / client / project / salesman cards when those
        /// tables are empty. Uses the DbContext directly so it does not
        /// trip OperationGuard. Safe to call after a folder switch.
        /// </summary>
        public static async Task SeedMasterProfilesAsync()
        {
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
                db.Clients.Add(Client("Demo Glass Contractor", now, 4));
                db.Clients.Add(Client("Skyline Interiors LLC", now, 3));
                db.Clients.Add(Client("Desert Rose Contracting", now, 2));
                db.Clients.Add(Client("Al Noor Glass Trading", now, 1));
                added = true;
            }

            if (!await db.Projects.AnyAsync())
            {
                db.Projects.Add(Project("Demo Tower Fit-Out", "Demo Glass Contractor", now, 4));
                db.Projects.Add(Project("Marina Tower — Fit-Out", "Skyline Interiors LLC", now, 3));
                db.Projects.Add(Project("Palm Villa 17", "Desert Rose Contracting", now, 2));
                db.Projects.Add(Project("Showroom Partition Works", "Al Noor Glass Trading", now, 1));
                added = true;
            }

            if (!await db.Salesmen.AnyAsync())
            {
                db.Salesmen.Add(new SavedSalesman
                {
                    Name = "A. Rahman",
                    Department = "Sales",
                    UsageCount = 4,
                    CreatedAt = now,
                    LastUsedAt = now
                });
                added = true;
            }

            if (added)
                await db.SaveChangesAsync();
        }

        private static SavedClient Client(string name, DateTime now, int uses) => new()
        {
            CustomerName = name,
            CustomerTrn = "100987654300003",
            BillingAddress = "PO Box 12345, Sharjah, UAE",
            SalesRepresentative = "A. Rahman",
            DocumentPreparedBy = "Demo User",
            ClientContactPhone = "+971 50 123 4567",
            UsageCount = uses,
            CreatedAt = now,
            LastUsedAt = now
        };

        private static SavedProject Project(
            string name, string customer, DateTime now, int uses) => new()
        {
            ProjectIdentifierName = name,
            ProjectContractNo = "CTR-2026-11",
            ProjectReference = name,
            SiteDeliveryLocation = "Sharjah Industrial Area",
            LpoNumber = "LPO-889",
            CustomerName = customer,
            UsageCount = uses,
            CreatedAt = now,
            LastUsedAt = now
        };

        // ── builders ──────────────────────────────────────────────────

        private static GlassSheet Sheet(string cat, int th, string color,
            int w, int h, string supplier, decimal buy, decimal sell,
            int stock, int used) => new GlassSheet
        {
            Category = cat, ThicknessMm = th, Color = color,
            WidthMm = w, HeightMm = h, Supplier = supplier,
            PurchasePricePerSqm = buy, SellPricePerSqm = sell,
            Stock = stock, Used = used
        };

        private static SavedProformaInvoice Pi(string id, string status,
            DateTime issue, string customer, string project, decimal total)
        {
            var materials = Math.Round(total / 1.05m, 2);
            var vat = Math.Round(total - materials, 2);

            var snapshot = new ProformaInvoiceSnapshot
            {
                Company = new CompanySnapshot
                {
                    CompanyName = "ProGlass Automation Demo",
                    CompanyTrn = "100234567800003",
                    CompanyLocation = "Sharjah, UAE",
                    ProformaId = id,
                    IssueDate = issue,
                    ValidUntil = issue.AddDays(7)
                },
                Client = new ClientSnapshot
                {
                    CustomerName = customer,
                    CustomerTrn = "100987654300003",
                    BillingAddress = "PO Box 12345, Sharjah, UAE",
                    SalesRepresentative = "A. Rahman",
                    DocumentPreparedBy = "Demo User",
                    ClientContactPhone = "+971 50 123 4567"
                },
                Project = new ProjectSnapshot
                {
                    ProjectIdentifierName = project,
                    ProjectContractNo = "CTR-2026-11",
                    SiteDeliveryLocation = "Sharjah Industrial Area",
                    LpoNumber = "LPO-889"
                },
                SpecGroups =
                {
                    new SpecGroupSnapshot
                    {
                        GroupNumber = 1,
                        GroupName = "Spec Group 1",
                        SelectedGlassType = "SGU",
                        BaseRate = 150m,
                        ItemRows =
                        {
                            new GlassItemRowSnapshot
                            {
                                SerialNumber = 1, GlassRef = "CLR-06",
                                W1 = 1200, H1 = 900, W2 = 1200, H2 = 900,
                                Qty = 12, Rate = 150m
                            },
                            new GlassItemRowSnapshot
                            {
                                SerialNumber = 2, GlassRef = "CLR-10",
                                W1 = 2000, H1 = 1500, W2 = 2000, H2 = 1500,
                                Qty = 6, Rate = 210m
                            }
                        }
                    }
                },
                Summary = new InvoiceSummarySnapshot
                {
                    DiscountType = "Percentage", DiscountValue = 0m,
                    IsVatApplicable = true, VatPercent = 5m
                }
            };

            return new SavedProformaInvoice
            {
                ProformaId = id,
                CompanyName = "ProGlass Automation Demo",
                CustomerName = customer,
                ProjectName = project,
                Status = status,
                IssueDate = issue,
                ValidUntil = issue.AddDays(7),
                GlassMaterialsSubtotal = materials,
                OtherChargesSubtotal = 0m,
                DiscountAmount = 0m,
                VatAmount = vat,
                GrandTotalVatInclusive = total,
                FullSnapshotJson = JsonSerializer.Serialize(snapshot),
                CreatedAt = issue,
                UpdatedAt = issue
            };
        }

        private static (SavedJobOrder Entity, Models.JobOrder.SavedJobOrder.JobOrderSnapshot Snapshot)
            BuildJob(SavedProformaInvoice pi, string jobId, string status,
                string priority, DateTime? delivery)
        {
            var snap = JobOrderMapper.BuildFromProformaInvoice(pi);
            snap.JobOrderId = jobId;
            snap.Production ??= new Models.JobOrder.ProductionDetails.ProductionDetails();
            snap.Production.Status = status;
            snap.Production.Priority = priority;
            snap.Production.DeliveryDate = delivery;
            var entity = JobOrderMapper.BuildEntityFromSnapshot(
                snap, pi.GrandTotalVatInclusive);
            entity.Status = status;
            entity.Priority = priority;
            entity.DeliveryDate = delivery;
            return (entity, snap);
        }

        private static SavedDeliveryNote BuildDn(
            SavedJobOrder job,
            Models.JobOrder.SavedJobOrder.JobOrderSnapshot snap,
            string dnId, int seq, string status, bool deliverAll,
            DateTime date, string driver, string vehicle, string receiver)
        {
            var dn = DeliveryNoteMapper.FromJobOrder(job, snap, dnId, seq);
            dn.Status = status;
            dn.DeliveryDate = date;
            dn.DriverName = driver;
            dn.VehicleNumber = vehicle;
            dn.ReceiverName = receiver;
            foreach (var item in dn.Items)
            {
                if (deliverAll)
                {
                    item.DeliveredQty = item.OrderedQty;
                    item.BalanceQty = 0;
                }
            }
            dn.TotalDeliveredQty = dn.Items.Sum(i => i.DeliveredQty);
            dn.TotalBalanceQty = dn.Items.Sum(i => i.BalanceQty);
            dn.TotalPreviouslyDeliveredQty = dn.Items.Sum(i => i.PreviouslyDeliveredQty);
            DeliveryNoteMapper.RefreshSnapshot(dn);
            return dn;
        }

        private static SavedTaxInvoice Ti(string num, string pi, string dn,
            string customer, string status, decimal total,
            DateTime issue, DateTime due) => new SavedTaxInvoice
        {
            InvoiceNumber = num,
            ProformaId = pi,
            DeliveryNoteReference = dn,
            Status = status,
            CustomerName = customer,
            ProjectName = "Demo project",
            CompanyName = "ProGlass Automation Demo",
            CompanyTrn = "100234567800003",
            IssueDate = issue,
            DueDate = due,
            PaymentTerms = "Net 30",
            PaymentTermsDays = 30
        };
    }
}
