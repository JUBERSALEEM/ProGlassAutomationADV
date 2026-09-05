// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Database/BackupAttachmentCoverageTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// AUDIT FINDING (2026-09-05, P0 — silent data loss):
// BackupService v2 copied only *.db + config.json into a backup set.
// Tax Invoice attachments ride taxinvoices.db (they are blobs — fine),
// but Inquiry, Proforma Invoice and Delivery Note attachments live as
// FILES under:
//   {AppData}\ProGlassAutomation\Inquiries\{id}\Attachments\
//   {AppData}\ProGlassAutomation\ProformaInvoices\Attachments\
//   {AppData}\ProGlassAutomation\DeliveryNotes\Attachments\
// A restore rebuilt every document record but lost every such file —
// the exact class of bug that only shows up AFTER the customer's disk
// died. These facts pin the fix so the gap cannot reopen.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using ProGlassAutomation.Services.Database.DatabaseBackup;
using Xunit;

namespace ProGlassAutomation.Tests.Database
{
    public sealed class BackupAttachmentCoverageTests
    {
        private static string Read(string relative) =>
            File.ReadAllText(Path.Combine(LocateProjectRoot(), relative));

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "ProGlassAutomation.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new FileNotFoundException("Project root not found");
        }

        private const string ServicePath =
            "Services/Database/DatabaseBackup/BackupService.cs";

        // ── 1. WHITELIST: exactly the three attachment roots ────────────
        [Fact]
        public void Whitelist_contains_exactly_the_attachment_roots()
        {
            var field = typeof(BackupService).GetField(
                "ExtraDataSubfolders", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.NotNull(field);

            var folders = (string[])field!.GetValue(null)!;
            Assert.Equal(
                new[] { "Inquiries", "ProformaInvoices", "DeliveryNotes" },
                folders);
        }

        // ── 2. BACKUP: both top-level sweep AND the whitelisted trees ───
        [Fact]
        public void Backup_copies_database_files_and_attachment_trees()
        {
            string src = Read(ServicePath);
            // v2 contract (kept): every *.db + config.json
            Assert.Contains("GetFiles(\"*.db\")", src, StringComparison.Ordinal);
            // P0 fix: the whitelisted attachment trees ride every set
            Assert.Contains("foreach (var sub in ExtraDataSubfolders)", src,
                StringComparison.Ordinal);
            Assert.Contains("sourceDir, Path.Combine(setFolder, sub)",
                src, StringComparison.Ordinal);
        }

        // ── 3. RESTORE: attachment trees are replaced back ──────────────
        [Fact]
        public void Restore_recovers_attachment_trees_into_appdata()
        {
            string src = Read(ServicePath);
            Assert.Contains(
                "sourceDir, Path.Combine(AppDataFolder, sub)",
                src, StringComparison.Ordinal);
        }

        // ── 4. REPORTING: set sizes count the attachment files too ──────
        [Fact]
        public void Set_info_counts_files_recursively()
        {
            string src = Read(ServicePath);
            Assert.Contains(
                "GetFiles(\"*\", SearchOption.AllDirectories)",
                src, StringComparison.Ordinal);
        }

        // ── 5. NO SELF-CAPTURE: Backups\ can never be swept into a set ───
        // The whitelist is a static string[] — it cannot ever contain the
        // Backups directory itself. Guarded here as a live value assertion
        // (not just a comment) because a future "copy all subfolders"
        // refactor would recurse backups into backups forever.
        [Fact]
        public void Whitelist_can_never_include_the_backups_folder()
        {
            var field = typeof(BackupService).GetField(
                "ExtraDataSubfolders", BindingFlags.NonPublic | BindingFlags.Static)!;
            var folders = (string[])field.GetValue(null)!;
            Assert.DoesNotContain("Backups", folders, StringComparer.OrdinalIgnoreCase);
        }
    }
}
