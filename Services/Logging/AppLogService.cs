// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Logging/AppLogService.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — technical application log
//
// One central technical log for the whole app (startup, shutdown,
// warnings, errors, exceptions, DB/import/export problems). Distinct
// from the USER ACTIVITY log in Services/Audit (who did what) — this
// log is for diagnostics (what went wrong, exactly where).
//
// Every error gets a short ErrorId ("ERR-3F9A2C") that is shown in
// the error dialog and written to the file, so support can find the
// full technical record in seconds.
//
// Files: %AppData%\ProGlassAutomation\logs\app-yyyyMMdd.log
// Retention: 14 days, cleaned at startup.
// Thread-safe; never throws — logging must not break the app.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Audit;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;

namespace ProGlassAutomation.Services.Logging
{
    /// <summary>Central technical logger (static facade).</summary>
    public static class AppLogService
    {
        private static readonly object _writeLock = new();
        private static readonly Queue<LogEntry> _recent = new();
        private const int MaxBuffered = 500;
        private const int RetentionDays = 14;

        private static string LogFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "logs");

        /// <summary>Raised on the UI thread after each entry is written.</summary>
        public static event EventHandler<LogEntry>? LogWritten;

        /// <summary>Creates the log folder and prunes old files once.</summary>
        public static void Initialize()
        {
            try
            {
                Directory.CreateDirectory(LogFolder);

                var cutoff = DateTime.Now.AddDays(-RetentionDays);
                foreach (var file in new DirectoryInfo(LogFolder)
                    .GetFiles("app-*.log")
                    .Where(f => f.LastWriteTime < cutoff))
                {
                    file.Delete();
                }
            }
            catch { /* never fail startup because of logging */ }
        }

        // ── Convenience API ─────────────────────────────────────────────

        public static void Debug(string message, LogContext? context = null)
            => Write(LogLevel.Debug, message, null, context);

        public static void Info(string message, LogContext? context = null)
            => Write(LogLevel.Info, message, null, context);

        public static void Warning(string message, LogContext? context = null)
            => Write(LogLevel.Warning, message, null, context);

        /// <summary>Logs an error; returns its ErrorId for user-facing dialogs.</summary>
        public static string Error(
            string message, Exception? ex = null, LogContext? context = null)
        {
            var id = NewErrorId();
            Write(LogLevel.Error, message, ex, context, id);
            return id;
        }

        /// <summary>Logs a crash-level error; returns its ErrorId.</summary>
        public static string Critical(
            string message, Exception? ex = null, LogContext? context = null)
        {
            var id = NewErrorId();
            Write(LogLevel.Critical, message, ex, context, id);
            return id;
        }

        /// <summary>Snapshot of the in-memory ring buffer (newest last).</summary>
        public static List<LogEntry> GetRecent(int count = 200)
        {
            lock (_writeLock)
                return _recent.TakeLast(count).ToList();
        }

        // ── Core writer ─────────────────────────────────────────────────

        private static void Write(
            LogLevel level, string message, Exception? ex,
            LogContext? context, string errorId = "")
        {
            // Phase 2: Auto-flow ambient correlation if context doesn't have one
            if (context != null && string.IsNullOrWhiteSpace(context.CorrelationId))
            {
                try
                {
                    var ambient = ProGlassAutomation.Services.Common.CorrelationContext.CurrentId;
                    if (!string.IsNullOrWhiteSpace(ambient))
                        context.CorrelationId = ambient;
                }
                catch { /* Logger must never throw — see file header. */ }
            }
            if (context != null && string.IsNullOrWhiteSpace(context.DocumentId))
            {
                try
                {
                    var docId = ProGlassAutomation.Services.Common.CorrelationContext.CurrentDocumentId;
                    if (!string.IsNullOrWhiteSpace(docId))
                        context.DocumentId = docId;
                }
                catch { /* Logger must never throw — see file header. */ }
            }
            if (context != null && string.IsNullOrWhiteSpace(context.DocumentType))
            {
                try
                {
                    var docType = ProGlassAutomation.Services.Common.CorrelationContext.CurrentDocumentType;
                    if (!string.IsNullOrWhiteSpace(docType))
                        context.DocumentType = docType;
                }
                catch { /* Logger must never throw — see file header. */ }
            }

            var entry = new LogEntry
            {
                Level = level,
                Message = message ?? "",
                Context = context ?? new LogContext(),
                ErrorId = errorId,
                Username = AuditService.CurrentUsername,
                ExceptionText = ex == null ? "" : FormatException(ex)
            };

            // Also flow correlation from context if it was empty before but now filled
            if (string.IsNullOrWhiteSpace(entry.Context.CorrelationId) && !string.IsNullOrWhiteSpace(errorId))
            {
                // Use ErrorId as correlation fallback for errors without explicit correlation
            }

            lock (_writeLock)
            {
                _recent.Enqueue(entry);
                while (_recent.Count > MaxBuffered)
                    _recent.Dequeue();
            }

            try
            {
                var path = Path.Combine(
                    LogFolder,
                    $"app-{DateTime.Now:yyyyMMdd}.log");

                var line = new StringBuilder()
                    .Append(entry.TimestampUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"))
                    .Append("  [").Append(entry.LevelDisplay.PadRight(8)).Append("]")
                    .Append(string.IsNullOrEmpty(errorId) ? "" : $"  ({errorId})")
                    .Append(string.IsNullOrEmpty(entry.CorrelationId) ? "" : $"  {entry.CorrelationId}")
                    .Append(string.IsNullOrEmpty(entry.DocumentType) ? "" : $"  {entry.DocumentType}")
                    .Append(string.IsNullOrEmpty(entry.DocumentId) ? "" : $"  {entry.DocumentId}")
                    .Append(string.IsNullOrEmpty(entry.Username) ? "" : $"  user={entry.Username}")
                    .Append(entry.ContextDisplay.Length == 0 ? "" : $"  [{entry.ContextDisplay}]")
                    .Append("  ").Append(entry.Message)
                    .AppendLine(entry.ExceptionText.Length == 0
                        ? ""
                        : Environment.NewLine + entry.ExceptionText)
                    .ToString();

                lock (_writeLock)
                    File.AppendAllText(path, line);
            }
            catch
            {
                // A logging failure must never surface to the user.
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                if (dispatcher.CheckAccess())
                    LogWritten?.Invoke(null, entry);
                else
                    dispatcher.InvokeAsync(() => LogWritten?.Invoke(null, entry));
            }
        }

        private static string FormatException(Exception ex)
        {
            var sb = new StringBuilder();
            var current = ex;
            int depth = 0;

            while (current != null && depth < 4)
            {
                if (depth > 0) sb.AppendLine("  caused by:");
                sb.Append("  ").Append(current.GetType().Name)
                  .Append(": ").AppendLine(current.Message);

                var stack = current.StackTrace ?? "";
                if (stack.Length > 1500) stack = stack[..1500] + " ...";
                if (stack.Length > 0) sb.AppendLine(stack);

                current = current.InnerException;
                depth++;
            }

            return sb.ToString().TrimEnd();
        }

        private static string NewErrorId() =>
            "ERR-" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
    }
}
