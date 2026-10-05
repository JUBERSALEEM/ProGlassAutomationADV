// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Observability/DocumentPipelineTracer.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — OpenTelemetry-compatible tracing (Phase 10)
// VERSION: Phase 10 — 2026-08-21
//
// PURPOSE (Observability):
//   Business operations span multiple SQLite files:
//     PI → JO → DN → TI, and TI-save → DN "invoiced" flag in another DB.
//   When books disagree, support needs to see the WHOLE journey, not one
//   log line. This tracer emits spans through System.Diagnostics.Activity,
//   the built-in API that the OpenTelemetry SDK consumes unchanged. When
//   an OTel exporter is later attached (AddOpenTelemetry in a future host
//   scenario) every span here flows out automatically — no code changes.
//
//   Every span carries the ambient CorrelationId + document identity as
//   tags, so a span trace lines up 1:1 with the app log file
//   (AppLogService) and the audit trail. Span status (Ok/Error) mirrors
//   the Result<T> returned by the operation.
//
//   IMPORTANT: without a listener (the default desktop app), StartActivity
//   returns null and every call is a cheap no-op. Tracing costs nothing
//   until it is observed.
//
// USAGE:
//   using var span = DocumentPipelineTracer.StartDocumentOperation(
//       "Save", "TaxInvoice", invoiceNumber, corrId);
//   ...
//   DocumentPipelineTracer.MarkSuccess(span);   // or MarkFailure(span, err)
//
// ═══════════════════════════════════════════════════════════════════════

using System.Diagnostics;

namespace ProGlassAutomation.Services.Observability
{
    /// <summary>
    /// Emits OpenTelemetry-compatible Activity spans for the document
    /// pipeline (PI → JO → DN → TI). Static facade; safe to call with no
    /// listener attached.
    /// </summary>
    public static class DocumentPipelineTracer
    {
        /// <summary>Trace source name for OTel resource attribution.</summary>
        public const string SourceName = "ProGlassAutomation.Documents";

        /// <summary>Span tag key for the correlation id.</summary>
        public const string TagCorrelationId = "proglass.correlation_id";

        /// <summary>Span tag key for the document type (ProformaInvoice, JobOrder, …).</summary>
        public const string TagDocumentType = "proglass.document_type";

        /// <summary>Span tag key for the document id (PI-2026-001, …).</summary>
        public const string TagDocumentId = "proglass.document_id";

        private static readonly ActivitySource Source = new(SourceName);

        /// <summary>
        /// Starts a span for one document operation. Returns null when no
        /// listener is sampling (tracing disabled) — callers can ignore.
        /// </summary>
        public static Activity? StartDocumentOperation(
            string operation,
            string documentType,
            string? documentId = null,
            string? correlationId = null,
            ActivityKind kind = ActivityKind.Internal)
        {
            var activity = Source.StartActivity(
                BuildSpanName(operation, documentType), kind);

            if (activity == null)
                return null;

            RecordDocumentTags(activity, documentType, documentId, correlationId);
            return activity;
        }

        /// <summary>
        /// Writes document identity + correlation onto an existing span.
        /// Public so tests can verify tag content without a listener.
        /// </summary>
        public static void RecordDocumentTags(
            Activity activity,
            string documentType,
            string? documentId,
            string? correlationId)
        {
            if (activity == null)
                return;

            activity.SetTag(TagDocumentType, documentType ?? "");
            if (!string.IsNullOrWhiteSpace(documentId))
                activity.SetTag(TagDocumentId, documentId!);
            if (!string.IsNullOrWhiteSpace(correlationId))
                activity.SetTag(TagCorrelationId, correlationId!);
        }

        /// <summary>Marks a span as succeeded.</summary>
        public static void MarkSuccess(Activity? activity)
        {
            if (activity == null)
                return;

            activity.SetStatus(ActivityStatusCode.Ok);
        }

        /// <summary>Marks a span as failed with an optional description.</summary>
        public static void MarkFailure(Activity? activity, string? error = null)
        {
            if (activity == null)
                return;

            activity.SetStatus(
                ActivityStatusCode.Error,
                string.IsNullOrWhiteSpace(error) ? null : error);
        }

        /// <summary>
        /// Builds a stable span name "DocumentType.Operation".
        /// Pure predicate — testable.
        /// </summary>
        public static string BuildSpanName(string operation, string documentType)
        {
            var op = string.IsNullOrWhiteSpace(operation)
                ? "DocumentOperation"
                : operation.Trim();
            var type = string.IsNullOrWhiteSpace(documentType)
                ? "Document"
                : documentType.Trim();

            return $"{type}.{op}";
        }
    }
}
