// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Updates/UpdateHistoryService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — update history (§34)
//
// A small local, append-mostly ledger of update events (detected,
// downloaded). Persisted as JSON so the Settings page can show what
// happened and when. Capped at 30 entries — this is a convenience
// log, not an audit trail (real audit lives in audit.db).
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Updates
{
    public enum UpdateEventType
    {
        Detected,
        Downloaded,
        CheckFailed
    }

    /// <summary>One recorded update event.</summary>
    public class UpdateHistoryEntry
    {
        public DateTime TimestampUtc { get; set; }
        public string EventType { get; set; } = "";
        public string Version { get; set; } = "";
        public string ReleaseName { get; set; } = "";
        public string Detail { get; set; } = "";
    }

    /// <summary>Persists update events to update-history.json.</summary>
    public static class UpdateHistoryService
    {
        private const int MaxEntries = 30;

        private static readonly JsonSerializerOptions Options = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        private static string HistoryPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "update-history.json");

        /// <summary>Appends one event (newest first, capped).</summary>
        public static void Record(
            UpdateEventType type, string version,
            string releaseName = "", string detail = "")
        {
            try
            {
                var entries = ReadAll();

                entries.Insert(0, new UpdateHistoryEntry
                {
                    TimestampUtc = DateTime.UtcNow,
                    EventType = type.ToString(),
                    Version = version ?? "",
                    ReleaseName = releaseName ?? "",
                    Detail = detail ?? ""
                });

                if (entries.Count > MaxEntries)
                    entries = entries.Take(MaxEntries).ToList();

                var dir = Path.GetDirectoryName(HistoryPath)!;
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(
                    HistoryPath, JsonSerializer.Serialize(entries, Options));
            }
            catch
            {
                // History is a convenience — never block updates on it.
            }
        }

        /// <summary>All recorded events, newest first.</summary>
        public static List<UpdateHistoryEntry> ReadAll()
        {
            try
            {
                if (!File.Exists(HistoryPath))
                    return new List<UpdateHistoryEntry>();

                var json = File.ReadAllText(HistoryPath);
                if (string.IsNullOrWhiteSpace(json))
                    return new List<UpdateHistoryEntry>();

                return JsonSerializer
                    .Deserialize<List<UpdateHistoryEntry>>(json, Options)
                    ?? new List<UpdateHistoryEntry>();
            }
            catch
            {
                return new List<UpdateHistoryEntry>();
            }
        }

        /// <summary>Async wrapper for view-models.</summary>
        public static Task<List<UpdateHistoryEntry>> ReadAllAsync() =>
            Task.Run(ReadAll);
    }
}
