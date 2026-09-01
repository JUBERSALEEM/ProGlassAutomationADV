// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Storage/StorageBreakdownService.cs
// PROJECT: ProGlassAutomation
// MODULE: Storage transparency — what actually fills the storage card
//
// The sidebar storage card shows one total number. Users asked "where is
// this storage going?" — this service breaks the %AppData%\ProGlassAutomation
// folder into named categories (icon + module name + covered storage) so
// the UI can show exactly which thing holds the fill.
//
// DESIGN:
//   · Pure folder math, no locks, no DB access — safe to run on a
//     background thread (the VM calls it inside Task.Run).
//   · Each category maps to a real path the app writes (database files,
//     backups, attachments, logs, models, fonts, config).
//   · Anything not matched by a category lands in "Other files" so the
//     total always adds up to the real folder size.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ProGlassAutomation.Services.Database;

namespace ProGlassAutomation.Services.Storage
{
    /// <summary>One storage category shown in the storage popup.</summary>
    public sealed class StorageBreakdownItem
    {
        /// <summary>Stable key (also used as a localization key suffix).</summary>
        public string Key { get; set; } = "";

        /// <summary>Localization key, e.g. "Storage.Database".</summary>
        public string DisplayNameKey { get; set; } = "";

        /// <summary>Localized display name (filled by the VM when items are published).</summary>
        public string DisplayName { get; set; } = "";

        /// <summary>Segoe MDL2 glyph (glyphs already proven elsewhere in the app).</summary>
        public string Icon { get; set; } = "\uE8B7";

        /// <summary>Absolute path measured.</summary>
        public string Path { get; set; } = "";

        /// <summary>Total bytes in this category.</summary>
        public long Bytes { get; set; }

        /// <summary>Human size, e.g. "12.4 MB".</summary>
        public string SizeDisplay { get; set; } = "0 MB";

        /// <summary>0..1 share of the whole folder (for the bar).</summary>
        public double Fraction { get; set; }

        /// <summary>Proportional bar width in DIP (fixed max — no container binding needed).</summary>
        public double BarWidth { get; set; }
    }

    /// <summary>
    /// Computes the per-category breakdown of the ProGlass data folder.
    /// Pure and synchronous — call on a background thread.
    /// </summary>
    public static class StorageBreakdownService
    {
        private const double BarMaxWidth = 240.0;

        /// <summary>
        /// Walks the app-data folder and returns per-category sizes.
        /// Uses <see cref="AppDataRoot.CurrentFolder"/> so demo mode
        /// measures the demo folder, not the production one.
        /// </summary>
        public static IReadOnlyList<StorageBreakdownItem> ComputeBreakdown()
        {
            var folder = AppDataRoot.CurrentFolder;
            var items = new List<StorageBreakdownItem>();
            if (!Directory.Exists(folder))
                return items;

            long total = SafeDirSize(folder);

            var dbWal = new[] { "-wal", "-shm" };

            AddFile(items, "Database", "Storage.Database", "\uE9D2",
                Path.Combine(folder, "proglass.db"), dbWal);
            AddFile(items, "AuditLog", "Storage.AuditLog", "\uE7C3",
                Path.Combine(folder, "audit.db"), dbWal);
            AddFile(items, "AiMemory", "Storage.AiMemory", "\uE8EF",
                Path.Combine(folder, "aimemory.db"), dbWal);
            AddFile(items, "Settings", "Storage.Settings", "\uE713",
                Path.Combine(folder, "config.json"), null);

            AddFolder(items, "Backups", "Storage.Backups", "\uE7F8",
                Path.Combine(folder, "Backups"));
            AddFolder(items, "DnAttachments", "Storage.DnAttachments", "\uE9D2",
                Path.Combine(folder, "DeliveryNotes", "Attachments"));
            AddFolder(items, "PiAttachments", "Storage.PiAttachments", "\uE9D2",
                Path.Combine(folder, "ProformaInvoices", "Attachments"));
            AddFolder(items, "Logs", "Storage.Logs", "\uE7C3",
                Path.Combine(folder, "logs"));
            AddFolder(items, "VoiceModels", "Storage.VoiceModels", "\uE8EF",
                Path.Combine(folder, "Models"));
            AddFolder(items, "Fonts", "Storage.Fonts", "\uE7C3",
                Path.Combine(folder, "Fonts"));

            long covered = items.Sum(i => i.Bytes);

            // Everything else at any depth that no category matched —
            // the remainder of the real folder size (never negative).
            long other = Math.Max(0, total - covered);
            if (other > 0)
            {
                items.Add(new StorageBreakdownItem
                {
                    Key = "Other",
                    DisplayNameKey = "Storage.Other",
                    Icon = "\uE8B7",
                    Path = folder,
                    Bytes = other,
                    SizeDisplay = FormatSize(other),
                });
            }

            // Fractions + bar widths after the total is final.
            long grand = Math.Max(1, total);
            foreach (var item in items)
            {
                item.Fraction = (double)item.Bytes / grand;
                item.BarWidth = item.Fraction * BarMaxWidth;
            }

            return items
                .OrderByDescending(i => i.Bytes)
                .ThenBy(i => i.Key, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Human size for the given bytes ("12.4 MB", "1.2 GB").</summary>
        public static string FormatSize(long bytes)
        {
            double mb = bytes / (1024d * 1024d);
            return mb >= 1024 ? $"{mb / 1024:N2} GB" : $"{mb:N0} MB";
        }

        private static void AddFile(
            List<StorageBreakdownItem> items, string key, string nameKey,
            string icon, string path, string[]? siblings)
        {
            long bytes = 0;
            foreach (var p in WithSiblings(path, siblings))
            {
                try
                {
                    if (File.Exists(p))
                        bytes += new FileInfo(p).Length;
                }
                catch { /* file vanished mid-walk — skip it */ }
            }
            if (bytes <= 0) return;
            items.Add(new StorageBreakdownItem
            {
                Key = key,
                DisplayNameKey = nameKey,
                Icon = icon,
                Path = path,
                Bytes = bytes,
                SizeDisplay = FormatSize(bytes),
            });
        }

        private static void AddFolder(
            List<StorageBreakdownItem> items, string key, string nameKey,
            string icon, string path)
        {
            long bytes = SafeDirSize(path);
            if (bytes <= 0) return;
            items.Add(new StorageBreakdownItem
            {
                Key = key,
                DisplayNameKey = nameKey,
                Icon = icon,
                Path = path,
                Bytes = bytes,
                SizeDisplay = FormatSize(bytes),
            });
        }

        private static IEnumerable<string> WithSiblings(string path, string[]? siblings)
        {
            yield return path;
            if (siblings == null) return;
            foreach (var s in siblings)
                yield return path + s;
        }

        /// <summary>Recursive folder size; never throws (skips unreadable files).</summary>
        private static long SafeDirSize(string dir)
        {
            long total = 0;
            try
            {
                if (!Directory.Exists(dir)) return 0;
                foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try { total += new FileInfo(f).Length; }
                    catch { /* locked/vanished file — skip */ }
                }
            }
            catch { /* folder unreadable — report 0 */ }
            return total;
        }
    }
}
