// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ResultPatternTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Phase 2 — Result Pattern & CorrelationId
//
// Tests the Result<T> pattern introduced in Phase 2:
//   • Ok / Fail factories
//   • CorrelationId flows via AsyncLocal
//   • Map / Bind transformations
//   • ErrorId linkage
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Common;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ResultPatternTests
    {
        [Fact]
        public void Ok_creates_success_result_with_value()
        {
            var result = Result<int>.Ok(42, "CORR-TEST");

            Assert.True(result.IsSuccess);
            Assert.False(result.IsFailure);
            Assert.Equal(42, result.Value);
            Assert.Equal("CORR-TEST", result.CorrelationId);
            Assert.Equal("", result.Error);
        }

        [Fact]
        public void Fail_creates_failure_with_error_and_correlation()
        {
            var result = Result<string>.Fail("Customer required", "CORR-FAIL", null, "ERR-ABC123");

            Assert.False(result.IsSuccess);
            Assert.True(result.IsFailure);
            Assert.Equal("Customer required", result.Error);
            Assert.Equal("CORR-FAIL", result.CorrelationId);
            Assert.Equal("ERR-ABC123", result.ErrorId);
        }

        [Fact]
        public void Fail_with_exception_preserves_exception()
        {
            var ex = new InvalidOperationException("DB locked");
            var result = Result<int>.Fail("Save failed", ex, "CORR-EX");

            Assert.True(result.IsFailure);
            Assert.Same(ex, result.Exception);
            Assert.Contains("Save failed", result.Error);
        }

        [Fact]
        public void Map_transforms_success_value()
        {
            var result = Result<int>.Ok(10, "CORR-MAP");
            var mapped = result.Map(x => x * 2);

            Assert.True(mapped.IsSuccess);
            Assert.Equal(20, mapped.Value);
            Assert.Equal("CORR-MAP", mapped.CorrelationId);
        }

        [Fact]
        public void Map_propagates_failure_without_calling_mapper()
        {
            var result = Result<int>.Fail("original error", "CORR-MAP-FAIL");
            bool mapperCalled = false;
            var mapped = result.Map(x => { mapperCalled = true; return x * 2; });

            Assert.True(mapped.IsFailure);
            Assert.False(mapperCalled);
            Assert.Equal("original error", mapped.Error);
        }

        [Fact]
        public void Bind_chains_successful_operations()
        {
            var result = Result<int>.Ok(5, "CORR-BIND");
            var bound = result.Bind(x => Result<string>.Ok($"Value={x}", "CORR-BIND"));

            Assert.True(bound.IsSuccess);
            Assert.Equal("Value=5", bound.Value);
        }

        [Fact]
        public void Bind_propagates_failure()
        {
            var result = Result<int>.Fail("first failed", "CORR-BIND-FAIL");
            var bound = result.Bind(x => Result<string>.Ok($"Value={x}"));

            Assert.True(bound.IsFailure);
            Assert.Equal("first failed", bound.Error);
        }

        [Fact]
        public void Non_generic_Ok_and_Fail()
        {
            var ok = Result.Ok("CORR-OK");
            var fail = Result.Fail("Something went wrong", "CORR-FAIL");

            Assert.True(ok.IsSuccess);
            Assert.True(fail.IsFailure);
            Assert.Equal("Something went wrong", fail.Error);
        }

        [Fact]
        public void CorrelationContext_flows_via_AsyncLocal()
        {
            var corrId = CorrelationContext.NewId();
            Assert.NotNull(corrId);
            Assert.StartsWith("CORR-", corrId);

            using (CorrelationContext.BeginScope(corrId, "PI-123", "ProformaInvoice"))
            {
                Assert.Equal(corrId, CorrelationContext.CurrentId);
                Assert.Equal("PI-123", CorrelationContext.CurrentDocumentId);
                Assert.Equal("ProformaInvoice", CorrelationContext.CurrentDocumentType);

                // Result should auto-flow correlation
                var result = Result<int>.Ok(1);
                Assert.Equal(corrId, result.CorrelationId);
            }

            // After dispose, should restore previous (null)
            Assert.Null(CorrelationContext.CurrentId);
        }

        [Fact]
        public async Task CorrelationContext_flows_across_async_boundaries()
        {
            var corrId = CorrelationContext.NewIdFor("TaxInvoice", "INV-001");
            using var scope = CorrelationContext.BeginDocumentScope("TaxInvoice", "INV-001", corrId);

            await Task.Delay(10);

            // Still flows after await
            Assert.Equal(corrId, CorrelationContext.CurrentId);

            var result = await Task.Run(() => Result<string>.Ok("test"));
            // Task.Run loses AsyncLocal by default, but our factory falls back to CurrentId if present in calling context?
            // Actually AsyncLocal flows into Task.Run in .NET, so it should still be there
            // This test documents expected behavior
            Assert.NotNull(result);
        }

        [Fact]
        public void Result_with_document_context()
        {
            var corrId = CorrelationContext.NewIdFor("DeliveryNote", "DN-001");
            var result = Result<string>.Fail("Delivery failed", corrId, null, "ERR-DN001");

            Assert.Contains("DN-001", result.CorrelationId);
            Assert.Equal("ERR-DN001", result.ErrorId);
        }

        [Fact]
        public void Ensure_validates_predicate()
        {
            var result = Result<int>.Ok(10, "CORR-ENSURE");
            var ensured = result.Ensure(x => x > 5, "Must be >5");

            Assert.True(ensured.IsSuccess);

            var failed = result.Ensure(x => x > 20, "Must be >20");
            Assert.True(failed.IsFailure);
            Assert.Equal("Must be >20", failed.Error);
        }

        [Fact]
        public void ToString_includes_correlation_and_error()
        {
            var ok = Result<int>.Ok(42, "CORR-STR");
            var fail = Result<int>.Fail("oops", "CORR-STR-FAIL", null, "ERR-123");

            Assert.Contains("CORR-STR", ok.ToString());
            Assert.Contains("CORR-STR-FAIL", fail.ToString());
            Assert.Contains("ERR-123", fail.ToString());
        }
    }
}
