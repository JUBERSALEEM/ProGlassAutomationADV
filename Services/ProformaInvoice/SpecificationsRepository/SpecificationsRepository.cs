using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.ProformaInvoice.Specifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository
{
    /// <summary>
    /// EF Core-backed implementation of ISpecificationsRepository.
    /// Uses a separate SQLite database (specifications.db) so the spec library
    /// can be exported/backed up/shared independently of proforma data.
    /// </summary>
    public class SpecificationsRepository : ISpecificationsRepository
    {
        public async Task InitializeAsync()
        {
            using var db = new SpecificationsDbContext();
            await db.Database.EnsureCreatedAsync();
        }

        public async Task<List<SavedSpecification>> GetAllAsync(string glassTypeFilter = null)
        {
            using var db = new SpecificationsDbContext();

            var query = db.Specifications.AsQueryable();

            if (!string.IsNullOrWhiteSpace(glassTypeFilter))
                query = query.Where(s => s.GlassType == glassTypeFilter);

            // Smart sort: most-used first, then most-recent
            return await query
                .OrderByDescending(s => s.UsageCount)
                .ThenByDescending(s => s.LastUsedAt)
                .ToListAsync();
        }

        public async Task<SavedSpecification> SaveOrUpdateAsync(string glassType, string specName, decimal pricePerSqm)
        {
            if (string.IsNullOrWhiteSpace(glassType))
                throw new ArgumentException("Glass type is required", nameof(glassType));
            if (string.IsNullOrWhiteSpace(specName))
                throw new ArgumentException("Spec name is required", nameof(specName));

            using var db = new SpecificationsDbContext();

            // Check if this exact spec already exists (dedup by GlassType + SpecName)
            var existing = await db.Specifications
                .FirstOrDefaultAsync(s => s.GlassType == glassType && s.SpecName == specName);

            if (existing != null)
            {
                // Update rate to latest calculation + increment usage
                existing.PricePerSqm = pricePerSqm;
                existing.LastUsedAt = DateTime.Now;
                existing.UsageCount += 1;
                await db.SaveChangesAsync();
                return existing;
            }

            // Create new spec
            var newSpec = new SavedSpecification
            {
                GlassType = glassType,
                SpecName = specName,
                PricePerSqm = pricePerSqm,
                CreatedAt = DateTime.Now,
                LastUsedAt = DateTime.Now,
                UsageCount = 1
            };

            db.Specifications.Add(newSpec);
            await db.SaveChangesAsync();
            return newSpec;
        }

        public async Task MarkAsUsedAsync(int specId)
        {
            using var db = new SpecificationsDbContext();
            var spec = await db.Specifications.FindAsync(specId);
            if (spec != null)
            {
                spec.LastUsedAt = DateTime.Now;
                spec.UsageCount += 1;
                await db.SaveChangesAsync();
            }
        }

        public async Task DeleteAsync(int specId)
        {
            using var db = new SpecificationsDbContext();
            var spec = await db.Specifications.FindAsync(specId);
            if (spec != null)
            {
                db.Specifications.Remove(spec);
                await db.SaveChangesAsync();
            }
        }
    }
}