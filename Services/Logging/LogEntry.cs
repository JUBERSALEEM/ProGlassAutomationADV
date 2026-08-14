using System;

namespace ProGlassAutomation.Services.Logging
{
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Where an operation happened. Every field is optional — log what
    /// you know. Example: Module="Proforma Invoice", Class="PrintableInvoiceViewModel",
    /// Method="ExecuteSaveInvoiceAsync", Operation="Save".
    /// </summary>
    public class LogContext
    {
        public string Module { get; set; } = "";
        public string Screen { get; set; } = "";
        public string Class { get; set; } = "";
        public string Method { get; set; } = "";
        public string Operation { get; set; } = "";
    }

    /// <summary>One technical log record (app log, not user activity).</summary>
    public class LogEntry
    {
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
        public LogLevel Level { get; set; } = LogLevel.Info;
        public string Message { get; set; } = "";
        public string ErrorId { get; set; } = "";
        public LogContext Context { get; set; } = new();
        public string ExceptionText { get; set; } = "";
        public string Username { get; set; } = "";

        public string LevelDisplay => Level switch
        {
            LogLevel.Debug => "DEBUG",
            LogLevel.Info => "INFO",
            LogLevel.Warning => "WARN",
            LogLevel.Error => "ERROR",
            _ => "CRITICAL"
        };

        /// <summary>"(ERR-XXXXXX)" or empty — ready for direct display.</summary>
        public string ErrorIdDisplay =>
            string.IsNullOrEmpty(ErrorId) ? "" : $"  ({ErrorId})";

        public string TimeDisplay =>
            TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff");

        public string ContextDisplay
        {
            get
            {
                var parts = new System.Collections.Generic.List<string>();
                if (!string.IsNullOrEmpty(Context.Module)) parts.Add(Context.Module);
                if (!string.IsNullOrEmpty(Context.Class)) parts.Add(Context.Class);
                if (!string.IsNullOrEmpty(Context.Method)) parts.Add(Context.Method + "()");
                return string.Join(" › ", parts);
            }
        }
    }
}
