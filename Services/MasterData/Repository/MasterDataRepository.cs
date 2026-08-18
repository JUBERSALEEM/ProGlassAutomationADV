// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/MasterData/Repository/MasterDataRepository.cs
// ═══════════════════════════════════════════════════════════════════════
// CHAT 15 — PROJECT REFERENCE FIELD ADDED
//   3 precise changes:
//     1. SaveOrUpdateProjectAsync update-branch: persist ProjectReference
//     2. AutoSaveFromInvoiceAsync signature: add projectReference param
//     3. AutoSaveFromInvoiceAsync project block: pass ProjectReference
//   All other code, comments, methods preserved byte-for-byte.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.MasterData.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;

namespace ProGlassAutomation.Services.MasterData.Repository
{
    /// <summary>
    /// EF Core implementation of IMasterDataRepository.
    /// Each method creates a fresh DbContext (matches app-wide pattern).
    ///
    /// Upsert pattern:
    ///   1. Query for existing by uniqueness key (case-insensitive)
    ///   2. If found → update all fields + increment UsageCount + set LastUsedAt
    ///   3. If not found → insert with UsageCount=1 + set CreatedAt + LastUsedAt
    ///
    /// List pattern:
    ///   All GetAll methods return ordered by UsageCount DESC then
    ///   LastUsedAt DESC. Most-frequently-used records appear first
    ///   in card lists and dropdowns.
    ///
    /// Search pattern:
    ///   Case-insensitive CONTAINS using EF.Functions.Like for SQLite.
    ///   Returns max 20 results (prevents UI overload on large datasets).
    ///
    /// Auto-save:
    ///   AutoSaveFromInvoiceAsync() is the bulk entry point called by
    ///   ProformaInvoiceMapper after every invoice save. Skips entities
    ///   with empty primary name fields — no garbage rows in master data.
    /// </summary>
    public class MasterDataRepository : IMasterDataRepository
    {
        private const int SEARCH_LIMIT = 20;

        public async Task InitializeAsync()
        {
            using var ctx = new MasterDataDbContext();
            await ctx.Database.EnsureCreatedAsync();

            // Additive schema upgrades for an existing masterdata.db
            // (future versioned migrations; empty today = no-op).
            SchemaMigrationRunner.Upgrade(
                MasterDataDbContext.DatabaseFilePath,
                AppSchemaMigrations.MasterData,
                "Master Data");

            // Schema evolution for databases created before v2 company
            // profile fields existed. EnsureCreated never ALTERs an
            // existing file, so add missing columns explicitly.
            // (Legacy inline migrator — to be folded into AppSchemaMigrations
            // in a later verified phase.)
            await EvolveCompanyColumnsAsync(ctx);
        }

        private static async Task EvolveCompanyColumnsAsync(
            MasterDataDbContext ctx)
        {
            var existing = new System.Collections.Generic.HashSet<string>(
                System.StringComparer.OrdinalIgnoreCase);

            using (var cmd = ctx.Database.GetDbConnection().CreateCommand())
            {
                cmd.CommandText = "PRAGMA table_info(Companies);";
                await ctx.Database.OpenConnectionAsync();
                using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    existing.Add(reader.GetString(1));
            }

            // (column, DDL type + default) — order matters only for
            // readability; each ALTER is independent.
            var columns = new (string Name, string Ddl)[]
            {
                ("LogoPath", "TEXT NOT NULL DEFAULT ''"),
                ("Representative", "TEXT NOT NULL DEFAULT ''"),
                ("RepresentativeMobile", "TEXT NOT NULL DEFAULT ''"),
                ("CompanyEmail", "TEXT NOT NULL DEFAULT ''"),
                ("AccountNumber", "TEXT NOT NULL DEFAULT ''"),
                ("BankName", "TEXT NOT NULL DEFAULT ''"),
                ("IsDefault", "INTEGER NOT NULL DEFAULT 0"),
            };

            foreach (var (name, ddl) in columns)
            {
                if (existing.Contains(name)) continue;

                using var cmd = ctx.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = $"ALTER TABLE Companies ADD COLUMN {name} {ddl};";
                await cmd.ExecuteNonQueryAsync();
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  COMPANIES
        // ══════════════════════════════════════════════════════════════════

        public async Task<SavedCompany> SaveOrUpdateCompanyAsync(SavedCompany company)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SettingsManage, "save a company");

            if (company == null)
                throw new ArgumentNullException(nameof(company));

            using var ctx = new MasterDataDbContext();

            var existing = await ctx.Companies.FirstOrDefaultAsync(c =>
                c.CompanyName.ToLower() == company.CompanyName.ToLower()
                && c.CompanyTrn.ToLower() == company.CompanyTrn.ToLower());

            var now = DateTime.Now;

            if (existing != null)
            {
                // Update — latest data wins
                existing.CompanyLocation = company.CompanyLocation;
                existing.UsageCount += 1;
                existing.LastUsedAt = now;

                await ctx.SaveChangesAsync();
                return existing;
            }

            // Insert new
            company.UsageCount = 1;
            company.CreatedAt = now;
            company.LastUsedAt = now;

            ctx.Companies.Add(company);
            await ctx.SaveChangesAsync();
            return company;
        }

        public async Task<List<SavedCompany>> GetAllCompaniesAsync()
        {
            using var ctx = new MasterDataDbContext();
            return await ctx.Companies
                .OrderByDescending(c => c.UsageCount)
                .ThenByDescending(c => c.LastUsedAt)
                .ToListAsync();
        }

        public async Task<List<SavedCompany>> SearchCompaniesAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SavedCompany>();

            using var ctx = new MasterDataDbContext();
            var q = query.Trim().ToLower();

            return await ctx.Companies
                .Where(c => c.CompanyName.ToLower().Contains(q))
                .OrderByDescending(c => c.UsageCount)
                .Take(SEARCH_LIMIT)
                .ToListAsync();
        }

        public async Task DeleteCompanyAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SettingsManage, "delete a company");

            using var ctx = new MasterDataDbContext();
            var entity = await ctx.Companies.FindAsync(id);
            if (entity == null) return;

            ctx.Companies.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  CLIENTS
        // ══════════════════════════════════════════════════════════════════

        public async Task<SavedClient> SaveOrUpdateClientAsync(SavedClient client)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.ProformaEdit, "save a client");

            if (client == null)
                throw new ArgumentNullException(nameof(client));

            using var ctx = new MasterDataDbContext();

            var existing = await ctx.Clients.FirstOrDefaultAsync(c =>
                c.CustomerName.ToLower() == client.CustomerName.ToLower()
                && c.CustomerTrn.ToLower() == client.CustomerTrn.ToLower());

            var now = DateTime.Now;

            if (existing != null)
            {
                // Update — latest data wins for all fields
                existing.CustomerReferenceCode = client.CustomerReferenceCode;
                existing.SalesRepresentative = client.SalesRepresentative;
                existing.DocumentPreparedBy = client.DocumentPreparedBy;
                existing.BillingAddress = client.BillingAddress;
                existing.AttentionExecutiveName = client.AttentionExecutiveName;
                existing.ClientContactPhone = client.ClientContactPhone;
                existing.UsageCount += 1;
                existing.LastUsedAt = now;

                await ctx.SaveChangesAsync();
                return existing;
            }

            // Insert new
            client.UsageCount = 1;
            client.CreatedAt = now;
            client.LastUsedAt = now;

            ctx.Clients.Add(client);
            await ctx.SaveChangesAsync();
            return client;
        }

        public async Task<List<SavedClient>> GetAllClientsAsync()
        {
            using var ctx = new MasterDataDbContext();
            return await ctx.Clients
                .OrderByDescending(c => c.UsageCount)
                .ThenByDescending(c => c.LastUsedAt)
                .ToListAsync();
        }

        public async Task<List<SavedClient>> SearchClientsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SavedClient>();

            using var ctx = new MasterDataDbContext();
            var q = query.Trim().ToLower();

            return await ctx.Clients
                .Where(c => c.CustomerName.ToLower().Contains(q))
                .OrderByDescending(c => c.UsageCount)
                .Take(SEARCH_LIMIT)
                .ToListAsync();
        }

        public async Task DeleteClientAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SettingsManage, "delete a client");

            using var ctx = new MasterDataDbContext();
            var entity = await ctx.Clients.FindAsync(id);
            if (entity == null) return;

            ctx.Clients.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  PROJECTS
        // ══════════════════════════════════════════════════════════════════

        public async Task<SavedProject> SaveOrUpdateProjectAsync(SavedProject project)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.ProformaEdit, "save a project");

            if (project == null)
                throw new ArgumentNullException(nameof(project));

            using var ctx = new MasterDataDbContext();

            var existing = await ctx.Projects.FirstOrDefaultAsync(p =>
                p.ProjectIdentifierName.ToLower() == project.ProjectIdentifierName.ToLower()
                && p.CustomerName.ToLower() == project.CustomerName.ToLower());

            var now = DateTime.Now;

            if (existing != null)
            {
                // Update — latest data wins
                existing.ProjectContractNo = project.ProjectContractNo;
                existing.ProjectReference = project.ProjectReference;    // CHAT 15: NEW
                existing.SiteDeliveryLocation = project.SiteDeliveryLocation;
                existing.LpoNumber = project.LpoNumber;
                existing.UsageCount += 1;
                existing.LastUsedAt = now;

                await ctx.SaveChangesAsync();
                return existing;
            }

            // Insert new
            project.UsageCount = 1;
            project.CreatedAt = now;
            project.LastUsedAt = now;

            ctx.Projects.Add(project);
            await ctx.SaveChangesAsync();
            return project;
        }

        public async Task<List<SavedProject>> GetAllProjectsAsync()
        {
            using var ctx = new MasterDataDbContext();
            return await ctx.Projects
                .OrderByDescending(p => p.UsageCount)
                .ThenByDescending(p => p.LastUsedAt)
                .ToListAsync();
        }

        public async Task<List<SavedProject>> SearchProjectsAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SavedProject>();

            using var ctx = new MasterDataDbContext();
            var q = query.Trim().ToLower();

            return await ctx.Projects
                .Where(p => p.ProjectIdentifierName.ToLower().Contains(q))
                .OrderByDescending(p => p.UsageCount)
                .Take(SEARCH_LIMIT)
                .ToListAsync();
        }

        public async Task<List<SavedProject>> GetProjectsByCustomerAsync(
            string customerName)
        {
            if (string.IsNullOrWhiteSpace(customerName))
                return new List<SavedProject>();

            using var ctx = new MasterDataDbContext();
            var name = customerName.Trim().ToLower();

            return await ctx.Projects
                .Where(p => p.CustomerName.ToLower() == name)
                .OrderByDescending(p => p.UsageCount)
                .ThenByDescending(p => p.LastUsedAt)
                .ToListAsync();
        }

        public async Task DeleteProjectAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SettingsManage, "delete a project");

            using var ctx = new MasterDataDbContext();
            var entity = await ctx.Projects.FindAsync(id);
            if (entity == null) return;

            ctx.Projects.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  SALESMEN
        // ══════════════════════════════════════════════════════════════════

        public async Task<SavedSalesman> SaveOrUpdateSalesmanAsync(
            SavedSalesman salesman)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SettingsManage, "save a salesman");

            if (salesman == null)
                throw new ArgumentNullException(nameof(salesman));

            using var ctx = new MasterDataDbContext();

            var existing = await ctx.Salesmen.FirstOrDefaultAsync(s =>
                s.Name.ToLower() == salesman.Name.ToLower());

            var now = DateTime.Now;

            if (existing != null)
            {
                // Update — latest department wins
                if (!string.IsNullOrWhiteSpace(salesman.Department))
                    existing.Department = salesman.Department;

                existing.UsageCount += 1;
                existing.LastUsedAt = now;

                await ctx.SaveChangesAsync();
                return existing;
            }

            // Insert new
            salesman.UsageCount = 1;
            salesman.CreatedAt = now;
            salesman.LastUsedAt = now;

            ctx.Salesmen.Add(salesman);
            await ctx.SaveChangesAsync();
            return salesman;
        }

        public async Task<List<SavedSalesman>> GetAllSalesmenAsync()
        {
            using var ctx = new MasterDataDbContext();
            return await ctx.Salesmen
                .OrderByDescending(s => s.UsageCount)
                .ThenByDescending(s => s.LastUsedAt)
                .ToListAsync();
        }

        public async Task<List<SavedSalesman>> SearchSalesmenAsync(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return new List<SavedSalesman>();

            using var ctx = new MasterDataDbContext();
            var q = query.Trim().ToLower();

            return await ctx.Salesmen
                .Where(s => s.Name.ToLower().Contains(q))
                .OrderByDescending(s => s.UsageCount)
                .Take(SEARCH_LIMIT)
                .ToListAsync();
        }

        public async Task DeleteSalesmanAsync(int id)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.SettingsManage, "delete a salesman");

            using var ctx = new MasterDataDbContext();
            var entity = await ctx.Salesmen.FindAsync(id);
            if (entity == null) return;

            ctx.Salesmen.Remove(entity);
            await ctx.SaveChangesAsync();
        }

        // ══════════════════════════════════════════════════════════════════
        //  BULK AUTO-SAVE FROM INVOICE
        //
        //  Called by ProformaInvoiceMapper.BuildEntityFromWizard() after
        //  building the invoice entity. Extracts company/client/project/
        //  salesman from raw field values and upserts all 4.
        //
        //  Skips any entity where the primary name field is empty —
        //  prevents garbage rows in master data.
        //
        //  Each upsert is independent — if one fails (e.g. duplicate
        //  race condition), the others still succeed. Errors are
        //  swallowed with Debug.WriteLine because master data auto-save
        //  should NEVER block invoice save.
        //
        //  CHAT 15: projectReference parameter added between
        //           projectContractNo and siteDeliveryLocation.
        // ══════════════════════════════════════════════════════════════════

        public async Task AutoSaveFromInvoiceAsync(
            // Company fields
            string companyName,
            string companyTrn,
            string companyLocation,

            // Client fields
            string customerName,
            string customerTrn,
            string customerReferenceCode,
            string salesRepresentative,
            string documentPreparedBy,
            string billingAddress,
            string attentionExecutiveName,
            string clientContactPhone,

            // Project fields
            string projectIdentifierName,
            string projectContractNo,
            string projectReference,           // CHAT 15: NEW
            string siteDeliveryLocation,
            string lpoNumber)
        {
            // ── Company ──────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(companyName))
            {
                try
                {
                    await SaveOrUpdateCompanyAsync(new SavedCompany
                    {
                        CompanyName = companyName.Trim(),
                        CompanyTrn = (companyTrn ?? "").Trim(),
                        CompanyLocation = (companyLocation ?? "").Trim()
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[MasterData] Company auto-save failed: {ex.Message}");
                }
            }

            // ── Client ───────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(customerName))
            {
                try
                {
                    await SaveOrUpdateClientAsync(new SavedClient
                    {
                        CustomerName = customerName.Trim(),
                        CustomerTrn = (customerTrn ?? "").Trim(),
                        CustomerReferenceCode = (customerReferenceCode ?? "").Trim(),
                        SalesRepresentative = (salesRepresentative ?? "").Trim(),
                        DocumentPreparedBy = (documentPreparedBy ?? "").Trim(),
                        BillingAddress = (billingAddress ?? "").Trim(),
                        AttentionExecutiveName = (attentionExecutiveName ?? "").Trim(),
                        ClientContactPhone = (clientContactPhone ?? "").Trim()
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[MasterData] Client auto-save failed: {ex.Message}");
                }
            }

            // ── Salesman (from client's SalesRepresentative field) ────────
            if (!string.IsNullOrWhiteSpace(salesRepresentative))
            {
                try
                {
                    await SaveOrUpdateSalesmanAsync(new SavedSalesman
                    {
                        Name = salesRepresentative.Trim()
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[MasterData] Salesman auto-save failed: {ex.Message}");
                }
            }

            // ── Project ──────────────────────────────────────────────────
            if (!string.IsNullOrWhiteSpace(projectIdentifierName))
            {
                try
                {
                    await SaveOrUpdateProjectAsync(new SavedProject
                    {
                        ProjectIdentifierName = projectIdentifierName.Trim(),
                        ProjectContractNo = (projectContractNo ?? "").Trim(),
                        ProjectReference = (projectReference ?? "").Trim(),   // CHAT 15: NEW
                        SiteDeliveryLocation = (siteDeliveryLocation ?? "").Trim(),
                        LpoNumber = (lpoNumber ?? "").Trim(),
                        CustomerName = (customerName ?? "").Trim()
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[MasterData] Project auto-save failed: {ex.Message}");
                }
            }
        }
    }
}