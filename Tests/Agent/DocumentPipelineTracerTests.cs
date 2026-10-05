// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/DocumentPipelineTracerTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 10 — OpenTelemetry-compatible document tracing
//
// Pins the tracer's contract:
//   • Span names are stable "DocumentType.Operation".
//   • Spans carry correlation + document identity as tags (so an OTel
//     trace lines up 1:1 with the app log file).
//   • MarkSuccess / MarkFailure mirror the Result<T> of the operation.
// ═══════════════════════════════════════════════════════════════════════

using System.Diagnostics;
using ProGlassAutomation.Services.Observability;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class DocumentPipelineTracerTests
    {
        [Theory]
        [InlineData("Save", "TaxInvoice", "TaxInvoice.Save")]
        [InlineData("SaveWithDnLink", "TaxInvoice", "TaxInvoice.SaveWithDnLink")]
        [InlineData("Save", "DeliveryNote", "DeliveryNote.Save")]
        [InlineData("", "", "Document.DocumentOperation")]
        [InlineData("Save", "", "Document.Save")]
        [InlineData("", "TaxInvoice", "TaxInvoice.DocumentOperation")]
        public void BuildSpanName_formats_stable_names(
            string operation, string documentType, string expected)
        {
            Assert.Equal(expected,
                DocumentPipelineTracer.BuildSpanName(operation, documentType));
        }

        [Fact]
        public void StartDocumentOperation_emits_span_with_document_tags()
        {
            using var listener = AttachListener();

            using var activity = DocumentPipelineTracer.StartDocumentOperation(
                "Save", "TaxInvoice", "INV-001", "CORR-ABC123");

            Assert.NotNull(activity);
            Assert.Equal("TaxInvoice.Save", activity!.OperationName);
            Assert.Equal("TaxInvoice",
                activity.GetTagItem(DocumentPipelineTracer.TagDocumentType) as string);
            Assert.Equal("INV-001",
                activity.GetTagItem(DocumentPipelineTracer.TagDocumentId) as string);
            Assert.Equal("CORR-ABC123",
                activity.GetTagItem(DocumentPipelineTracer.TagCorrelationId) as string);
        }

        [Fact]
        public void MarkSuccess_sets_ok_status()
        {
            using var listener = AttachListener();
            using var activity = DocumentPipelineTracer.StartDocumentOperation(
                "Save", "DeliveryNote", "DN-001");

            DocumentPipelineTracer.MarkSuccess(activity);

            Assert.NotNull(activity);
            Assert.Equal(ActivityStatusCode.Ok, activity!.Status);
        }

        [Fact]
        public void MarkFailure_sets_error_status_and_description()
        {
            using var listener = AttachListener();
            using var activity = DocumentPipelineTracer.StartDocumentOperation(
                "SaveWithDnLink", "TaxInvoice", "INV-001");

            DocumentPipelineTracer.MarkFailure(activity, "DN marking failed");

            Assert.NotNull(activity);
            Assert.Equal(ActivityStatusCode.Error, activity!.Status);
            Assert.Equal("DN marking failed", activity.StatusDescription);
        }

        [Fact]
        public void Mark_and_record_helpers_are_null_safe()
        {
            // When tracing is disabled, StartDocumentOperation returns null
            // and every helper must be a no-op, never a NullReferenceException.
            DocumentPipelineTracer.MarkSuccess(null);
            DocumentPipelineTracer.MarkFailure(null);
            DocumentPipelineTracer.RecordDocumentTags(null!, "TaxInvoice", null, null);
        }

        private static ActivityListener AttachListener()
        {
            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                    ActivitySamplingResult.AllData
            };
            ActivitySource.AddActivityListener(listener);
            return listener;
        }
    }
}
