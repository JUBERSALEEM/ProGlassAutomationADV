// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Storage/AgentDbContext.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The agent gets its OWN database (agent.db). It never opens proglass.db,
// audit.db or any ERP context, so nothing it stores can corrupt business
// data, and deleting agent.db resets the agent without touching the ERP.
//
// Path is FIXED at %AppData%/ProGlassAutomation/Agent/agent.db — the same
// per-user location every other module uses, so it is covered by the
// existing backup story and survives reinstalls.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;

namespace ProGlassAutomation.Agent.Storage
{
    /// <summary>A "whenever I say X, do Y" rule. Inert until approved.</summary>
    public class TeachingRule
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(300)]
        public string Trigger { get; set; } = "";

        /// <summary>Action DSL: "navigate:SheetStore" or "tool:get_inventory".</summary>
        [Required, MaxLength(300)]
        public string Action { get; set; } = "";

        /// <summary>False until a user explicitly approves it.</summary>
        public bool IsApproved { get; set; }

        [MaxLength(60)]
        public string ApprovedBy { get; set; } = "";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A fact the user explicitly asked the agent to remember.</summary>
    public class MemoryItem
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(60)]
        public string Kind { get; set; } = "note";

        [Required, MaxLength(2000)]
        public string Content { get; set; } = "";

        [MaxLength(60)]
        public string CreatedBy { get; set; } = "";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>A curated domain fact the local provider answers from.</summary>
    public class KnowledgeItem
    {
        [Key]
        public int Id { get; set; }

        [Required, MaxLength(80)]
        public string Category { get; set; } = "";

        [Required, MaxLength(200)]
        public string Title { get; set; } = "";

        [Required, MaxLength(4000)]
        public string Content { get; set; } = "";

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>EF Core SQLite context for the agent's private database.</summary>
    public class AgentDbContext : DbContext
    {
        public DbSet<TeachingRule> TeachingRules { get; set; } = null!;
        public DbSet<MemoryItem> MemoryItems { get; set; } = null!;
        public DbSet<KnowledgeItem> KnowledgeItems { get; set; } = null!;

        private static readonly string DbFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation",
            "Agent");

        private static readonly string DbPath = Path.Combine(DbFolder, "agent.db");

        /// <summary>Absolute path to agent.db (shown in Settings).</summary>
        public static string DatabaseFilePath => DbPath;

        protected override void OnConfiguring(DbContextOptionsBuilder options)
        {
            if (!Directory.Exists(DbFolder))
                Directory.CreateDirectory(DbFolder);

            options.UseSqlite($"Data Source={DbPath}");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<TeachingRule>()
                .HasIndex(r => r.IsApproved)
                .HasDatabaseName("IX_TeachingRules_Approved");

            modelBuilder.Entity<KnowledgeItem>()
                .HasIndex(k => k.Category)
                .HasDatabaseName("IX_KnowledgeItems_Category");
        }

        /// <summary>
        /// Creates agent.db when missing. Never throws — the agent must
        /// degrade to "storage unavailable" rather than break startup.
        /// </summary>
        public static bool EnsureCreated()
        {
            try
            {
                using var db = new AgentDbContext();
                db.Database.EnsureCreated();
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[AgentDbContext] EnsureCreated failed: {ex.Message}");
                return false;
            }
        }
    }
}
