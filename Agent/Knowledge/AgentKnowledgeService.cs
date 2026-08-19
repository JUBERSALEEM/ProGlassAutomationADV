// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Knowledge/AgentKnowledgeService.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The curated facts the LOCAL provider answers from. This is what lets
// the agent be useful with zero internet: the answers come from a small
// vetted glass-industry knowledge base, not from a model's guesswork.
//
// Export/import are deliberate, audited operations:
//   • ExportJson  — the user is choosing to take data out; audited.
//   • ImportJson  — validated and capped at 500 rows so a pasted blob
//                   cannot balloon agent.db or smuggle in 10k rows.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Storage;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Knowledge
{
    /// <summary>Transport shape for export/import.</summary>
    public sealed class KnowledgeExportItem
    {
        public string Category { get; set; } = "";
        public string Title { get; set; } = "";
        public string Content { get; set; } = "";
    }

    /// <summary>SQLite-backed knowledge base with a built-in seed.</summary>
    public sealed class AgentKnowledgeService : IAgentKnowledge
    {
        /// <summary>Maximum rows accepted from one import.</summary>
        public const int MaxImportRows = 500;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };

        private readonly IAgentAuditService? _audit;

        public AgentKnowledgeService(IAgentAuditService? audit = null)
        {
            _audit = audit;
            AgentDbContext.EnsureCreated();
        }

        public async Task AddAsync(string category, string title, string content)
        {
            title = (title ?? "").Trim();
            content = (content ?? "").Trim();
            if (title.Length == 0 || content.Length == 0) return;

            try
            {
                using var db = new AgentDbContext();

                db.KnowledgeItems.Add(new KnowledgeItem
                {
                    Category = Cap(string.IsNullOrWhiteSpace(category) ? "general" : category, 80),
                    Title = Cap(title, 200),
                    Content = Cap(content, 4000),
                    CreatedAtUtc = DateTime.UtcNow
                });

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentKnowledge] Add failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Substring match over title and content, capped. Word-level so
        /// "what is the VAT rate" still finds the "UAE VAT" entry.
        /// </summary>
        public async Task<IReadOnlyList<string>> RetrieveAsync(
            string query, int max = 8)
        {
            string term = (query ?? "").Trim();
            if (term.Length == 0) return Array.Empty<string>();

            try
            {
                using var db = new AgentDbContext();
                var all = await db.KnowledgeItems.ToListAsync();

                // Whole-phrase hits first, then per-word hits.
                var hits = all
                    .Where(k => Contains(k.Title, term) || Contains(k.Content, term))
                    .ToList();

                if (hits.Count == 0)
                {
                    var words = term
                        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                        .Where(w => w.Length >= 3)
                        .ToArray();

                    hits = all
                        .Where(k => words.Any(w =>
                            Contains(k.Title, w) || Contains(k.Content, w)))
                        .ToList();
                }

                return hits
                    .Take(max)
                    .Select(k => $"{k.Title}: {k.Content}")
                    .ToList();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentKnowledge] Retrieve failed: {ex.Message}");
                return Array.Empty<string>();
            }
        }

        /// <summary>Serializes everything to JSON. Deliberate + audited.</summary>
        public async Task<string> ExportJsonAsync()
        {
            try
            {
                using var db = new AgentDbContext();
                var rows = await db.KnowledgeItems
                    .OrderBy(k => k.Category).ThenBy(k => k.Title)
                    .Select(k => new KnowledgeExportItem
                    {
                        Category = k.Category,
                        Title = k.Title,
                        Content = k.Content
                    })
                    .ToListAsync();

                _audit?.Security(
                    Services.Audit.AuditService.CurrentUsername,
                    "Knowledge exported",
                    $"{rows.Count} item(s)");

                return JsonSerializer.Serialize(rows, JsonOptions);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentKnowledge] Export failed: {ex.Message}");
                return "[]";
            }
        }

        /// <summary>
        /// Imports validated JSON. Rows missing a title or content are
        /// skipped; the batch is capped at MaxImportRows. Returns the
        /// number actually stored.
        /// </summary>
        public async Task<int> ImportJsonAsync(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return 0;

            List<KnowledgeExportItem>? items;
            try
            {
                items = JsonSerializer.Deserialize<List<KnowledgeExportItem>>(
                    json, JsonOptions);
            }
            catch (JsonException ex)
            {
                AppLogService.Debug($"[AgentKnowledge] Import parse failed: {ex.Message}");
                return 0;
            }

            if (items == null || items.Count == 0) return 0;

            try
            {
                using var db = new AgentDbContext();

                int stored = 0;
                foreach (var item in items.Take(MaxImportRows))
                {
                    string title = (item.Title ?? "").Trim();
                    string content = (item.Content ?? "").Trim();
                    if (title.Length == 0 || content.Length == 0) continue;

                    db.KnowledgeItems.Add(new KnowledgeItem
                    {
                        Category = Cap(string.IsNullOrWhiteSpace(item.Category)
                            ? "imported" : item.Category, 80),
                        Title = Cap(title, 200),
                        Content = Cap(content, 4000),
                        CreatedAtUtc = DateTime.UtcNow
                    });
                    stored++;
                }

                await db.SaveChangesAsync();

                _audit?.Security(
                    Services.Audit.AuditService.CurrentUsername,
                    "Knowledge imported",
                    $"{stored} item(s) of {items.Count} offered");

                return stored;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentKnowledge] Import failed: {ex.Message}");
                return 0;
            }
        }

        public async Task ClearAsync()
        {
            try
            {
                using var db = new AgentDbContext();
                int count = await db.KnowledgeItems.CountAsync();

                db.KnowledgeItems.RemoveRange(db.KnowledgeItems);
                await db.SaveChangesAsync();

                _audit?.Security("", "Knowledge cleared", $"{count} item(s) removed");
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentKnowledge] Clear failed: {ex.Message}");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SEED — the facts that make the local agent useful offline
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Inserts the built-in glass-industry facts once. Idempotent:
        /// seeding is skipped when a seeded row already exists, so calling
        /// this on every startup is safe.
        /// </summary>
        public async Task EnsureSeededAsync()
        {
            try
            {
                using var db = new AgentDbContext();

                bool alreadySeeded = await db.KnowledgeItems
                    .AnyAsync(k => k.Category == SeedCategory);

                if (alreadySeeded) return;

                foreach (var (title, content) in SeedFacts)
                {
                    db.KnowledgeItems.Add(new KnowledgeItem
                    {
                        Category = SeedCategory,
                        Title = title,
                        Content = content,
                        CreatedAtUtc = DateTime.UtcNow
                    });
                }

                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentKnowledge] Seed failed: {ex.Message}");
            }
        }

        private const string SeedCategory = "proglass-core";

        private static readonly (string Title, string Content)[] SeedFacts =
        {
            ("UAE VAT",
             "VAT in the UAE is 5%. A VAT-inclusive total is converted to " +
             "net by dividing by 1.05 (net = gross / 1.05); VAT amount = " +
             "gross - net."),

            ("Glass thicknesses",
             "Standard flat-glass thicknesses stocked are 3, 4, 5, 6, 8, 10, " +
             "12, 15 and 19 mm."),

            ("Jumbo sheet sizes",
             "Common stock sheet sizes are 2440x1830 mm, 3210x2250 mm and " +
             "3660x2440 mm. Sheet area in SQM = width_mm * height_mm / 1,000,000."),

            ("Quoting per SQM",
             "Glass is quoted per square metre. The selling rate must cover " +
             "the sheet purchase price per SQM plus processing (cutting, " +
             "tempering, polishing) plus a margin; the spread between " +
             "purchase and selling price per SQM is the gross margin."),

            ("DGU naming",
             "A double glazed unit is named outer pane + air spacer + inner " +
             "pane, for example '6mm Clear FT Glass + 12mm ASP + 6mm Clear " +
             "FT Glass'. ASP means air spacer and its width is in millimetres."),

            ("Master delivery note",
             "The Master Delivery Note holds the running balance for a job " +
             "order: ordered quantity minus everything already delivered. " +
             "Partial deliveries draw down that balance.")
        };

        private static bool Contains(string? haystack, string needle) =>
            (haystack ?? "").IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0;

        private static string Cap(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
