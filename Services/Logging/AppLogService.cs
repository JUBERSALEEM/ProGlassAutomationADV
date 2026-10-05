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

        // ── SIZE BOUNDS (2026-10-05) ──────────────────────────────────────
        // Retention was time-only: files older than RetentionDays are deleted
        // in Initialize(), which runs ONCE at startup. Two consequences that
        // only show up in trouble:
        //
        //   • A session left open for a week never prunes anything.
        //   • Nothing caps a single file, so one runaway loop — a retry storm,
        //     a binding error raised per frame, an exception inside a
        //     timer — writes without limit until the volume is full.
        //
        // That matters more than usual here: the data root can be redirected
        // onto a network share (see Services/Database/SharedDataRootService),
        // so filling it is not "logs got big", it is an ERP that cannot write
        // to its own database.
        //
        // These are safety nets, not a working limit. A normal day of use
        // writes a few hundred kilobytes; 8 MB is roughly twenty times a heavy
        // day. Worst case is bounded at MaxLogFileBytes x (MaxRotatedParts + 1)
        // per day, and MaxLogFolderBytes bounds the folder absolutely.
        private const long MaxLogFileBytes = 8L * 1024 * 1024;
        private const int MaxRotatedParts = 2;
        private const long MaxLogFolderBytes = 200L * 1024 * 1024;

        // Written-bytes accounting, so the size check adds no syscall per
        // line. File.AppendAllText already opens and closes the file for every
        // entry, and a runaway loop is exactly the case this guard exists for,
        // so stat-ing the file on every line would make the failure mode
        // worse. Seeded from the real length whenever the path changes, so a
        // second session appending to the same file cannot hide its growth.
        // All three are only ever touched inside _writeLock.
        private static string? _sizedPath;
        private static long _sizedBytes;
        private static int _nextPart = 1;

        private static string LogFolder => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation", "logs");

        /// <summary>
        /// "Session is open" marker. Written when logging initialises,
        /// removed by <see cref="MarkCleanShutdown"/>. If it still exists
        /// at the next startup, the previous session ended without a clean
        /// shutdown — the signature of an uncatchable crash (a
        /// StackOverflowException, native fault or kill) that could not run
        /// any exception handler and therefore wrote no crash report.
        /// </summary>
        private static string SessionMarkerPath =>
            Path.Combine(LogFolder, "session-open.marker");

        /// <summary>Raised on the UI thread after each entry is written.</summary>
        public static event EventHandler<LogEntry>? LogWritten;

        /// <summary>
        /// Rolls the current day's file over to <c>app-yyyyMMdd.N.log</c> and
        /// returns the path to keep writing to. The part number cycles through
        /// 1..<see cref="MaxRotatedParts"/>, deleting the slot it is about to
        /// reuse, so a loop that logs without end is bounded instead of
        /// filling the volume. Called under <c>_writeLock</c>.
        /// </summary>
        /// <remarks>
        /// If the rename fails the counter is deliberately NOT reset: the next
        /// entry tries to rotate again, and if it never succeeds the file
        /// simply keeps growing — the pre-existing behaviour, and the least
        /// surprising outcome for a safety net that could not do its job.
        /// Resetting it would silently disable the guard for the rest of the
        /// session.
        /// </remarks>
        private static string RotateLog(string path)
        {
            try
            {
                var dir = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(dir)) dir = LogFolder;
                var stem = Path.GetFileNameWithoutExtension(path);
                var target = Path.Combine(dir, $"{stem}.{_nextPart}.log");

                if (File.Exists(target)) File.Delete(target);
                File.Move(path, target);

                _nextPart = _nextPart >= MaxRotatedParts ? 1 : _nextPart + 1;
                _sizedBytes = 0;
            }
            catch
            {
                // Logging must never be the reason the app misbehaves. See the
                // remarks: the counter stays high so the next write retries.
            }
            return path;
        }

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

                // Age alone is not a bound: fourteen days of a runaway loop is
                // fourteen times whatever one day produced. Sweep oldest-first
                // until the folder is under MaxLogFolderBytes. Today's file is
                // excluded — it is the one being appended to, and deleting the
                // live log to save space would destroy exactly the evidence
                // that explains why the disk filled up.
                var today = Path.Combine(LogFolder, $"app-{DateTime.Now:yyyyMMdd}.log");
                var byAge = new DirectoryInfo(LogFolder)
                    .GetFiles("app-*.log")
                    .Where(f => !string.Equals(
                        f.FullName, today, StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => f.LastWriteTime)
                    .ToList();

                long total = byAge.Sum(f => f.Length);
                if (File.Exists(today)) total += new FileInfo(today).Length;

                foreach (var file in byAge)
                {
                    if (total <= MaxLogFolderBytes) break;
                    total -= file.Length;
                    file.Delete();
                }
            }
            catch { /* never fail startup because of logging */ }

            // ── Unclean-shutdown detection (2026-09-17) ─────────────────
            // A StackOverflowException (or any native / process-level fault)
            // terminates a .NET process immediately: DispatcherUnhandledException,
            // AppDomain.UnhandledException and UnobservedTaskException NEVER
            // run for it, so no log entry and no crash-*.txt can be written
            // at the moment of death. The marker below is the evidence that
            // survives: it is only removed on a clean exit, so its presence
            // at startup proves the previous run died hard. The app-*.log
            // lines around the recorded end time then show the last thing
            // the app was doing (module navigation breadcrumbs are written
            // synchronously to disk for exactly this reason).
            try
            {
                bool unclean = File.Exists(SessionMarkerPath);
                string previousMarker = unclean
                    ? SafeReadMarker()
                    : "";

                File.WriteAllText(
                    SessionMarkerPath,
                    $"session-open pid={Environment.ProcessId} " +
                    $"started={DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}");

                if (unclean)
                {
                    Warning(
                        "Previous session ended WITHOUT a clean shutdown " +
                        "(no OnExit). This is the signature of an uncatchable " +
                        "crash such as a StackOverflowException — those bypass " +
                        "every .NET exception handler, which is why no crash " +
                        "report was written at the time. Previous session: " +
                        (string.IsNullOrWhiteSpace(previousMarker)
                            ? "(marker unreadable)"
                            : previousMarker.Trim()) +
                        ". Check the log lines immediately before this entry " +
                        "and the Windows Event Viewer (Application log, " +
                        ".NET Runtime / Application Error) for the faulting " +
                        "module.",
                        new LogContext { Operation = "Startup" });
                }
            }
            catch { /* marker bookkeeping must never break startup */ }
        }

        private static string SafeReadMarker()
        {
            try { return File.ReadAllText(SessionMarkerPath); }
            catch { return ""; }
        }

        /// <summary>
        /// Removes the "session is open" marker. Called from App.OnExit so
        /// only a clean shutdown clears it. A crash-triggered Shutdown still
        /// runs OnExit — that is fine, because a catchable crash has already
        /// written its crash report; this marker exists for the uncatchable
        /// ones that write nothing.
        /// </summary>
        public static void MarkCleanShutdown()
        {
            try
            {
                if (File.Exists(SessionMarkerPath))
                    File.Delete(SessionMarkerPath);
            }
            catch { /* never throw from shutdown bookkeeping */ }
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
                {
                    if (_sizedPath != path)
                    {
                        _sizedPath = path;
                        _sizedBytes = File.Exists(path)
                            ? new FileInfo(path).Length
                            : 0;
                    }

                    if (_sizedBytes >= MaxLogFileBytes)
                        path = RotateLog(path);

                    File.AppendAllText(path, line);

                    // A character count used as a byte estimate. The log
                    // format is almost entirely ASCII, so the two agree; where
                    // they do not, this under-counts, which only means the cap
                    // trips marginally later. Measuring UTF-8 bytes exactly
                    // would cost a pass over every line to sharpen a safety
                    // net that does not need sharpening.
                    _sizedBytes += line.Length;
                }
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
