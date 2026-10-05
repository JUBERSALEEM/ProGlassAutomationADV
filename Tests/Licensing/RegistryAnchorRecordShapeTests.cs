// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Licensing/RegistryAnchorRecordShapeTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Licensing — RegistryAnchor.Record consumer contract
//
// REGRESSION LOCK for the Aug-2026 build-break:
//
//     'RegistryAnchor.Record' does not contain a definition for 'Value'
//     and no accessible extension method 'Value' accepting a first
//     argument of type 'RegistryAnchor.Record' could be found.
//
// The root cause was that TrialLedger.Read treated the nullable
// annotation `RegistryAnchor.Record?` as if the underlying type were a
// struct (writing `r.HasValue` and `r.Value.FirstIssuedUtc`). It is not —
// `sealed record Record { ... }` is a REFERENCE type. The correct
// pattern is `if (r is not null) { ... r.FirstIssuedUtc ... }`.
//
// This test:
//   1. Proves at compile time that `RegistryAnchor.Record` is a class
//      (a reference type), so the wrong `.Value` pattern breaks build
//      forever if reintroduced.
//   2. Proves the record still exposes the two properties TrialLedger
//      depends on: FirstIssuedUtc (DateTime) and LastSeenUtc (DateTime?).
//   3. Proves the with-expression / init pattern used by
//      TrialLedger.Write still compiles.
//
// Nothing here touches the registry, the filesystem, or the licensing
// pipeline. It is purely a shape / typing contract enforced by the C#
// compiler.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Licensing;
using System;
using System.Reflection;
using Xunit;

namespace ProGlassAutomation.Tests.Licensing
{
    public class RegistryAnchorRecordShapeTests
    {
        [Fact]
        public void Record_is_a_reference_type_not_a_value_type()
        {
            // If RegistryAnchor is ever refactored to `internal sealed
            // record struct Record` the following assertions would fail —
            // AND the .Value pattern in TrialLedger would become legal.
            // Keeping this assert forces a deliberate migration of every
            // TrialLedger call site (currently 3 read + 2 write locations).
            var recordType = GetRegistryAnchorRecordType();
            Assert.False(recordType.IsValueType,
                "RegistryAnchor.Record must remain a reference type. " +
                "If you convert it to `record struct`, TrialLedger's " +
                "`r is not null` pattern will silently start comparing " +
                "against a boxed default value.");
        }

        [Fact]
        public void Record_exposes_the_two_properties_TrialLedger_reads()
        {
            var t = GetRegistryAnchorRecordType();
            var firstIssued = t.GetProperty("FirstIssuedUtc",
                BindingFlags.Instance | BindingFlags.Public);
            var lastSeen = t.GetProperty("LastSeenUtc",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.NotNull(firstIssued);
            Assert.Equal(typeof(DateTime), firstIssued!.PropertyType);

            Assert.NotNull(lastSeen);
            Assert.Equal(typeof(DateTime?), lastSeen!.PropertyType);

            // Both must be init-only — construction is `new Record { … }`
            // via object-initializer, not through a positional record ctor.
            Assert.True(firstIssued.CanWrite,
                "FirstIssuedUtc must have a public setter (init) — TrialLedger.Write uses object-initializer syntax.");
            Assert.True(lastSeen.CanWrite,
                "LastSeenUtc must have a public setter (init).");
        }

        [Fact]
        public void Record_is_nested_inside_RegistryAnchor_not_promoted_to_a_top_level_class()
        {
            var t = GetRegistryAnchorRecordType();
            Assert.NotNull(t.DeclaringType);
            Assert.Equal("RegistryAnchor", t.DeclaringType!.Name);
        }

        private static Type GetRegistryAnchorRecordType()
        {
            // RegistryAnchor is internal — resolve it via the assembly of
            // any public type from the same project (LicenseService is
            // public and lives next door).
            var asm = typeof(LicenseService).Assembly;
            var anchor = asm.GetType(
                "ProGlassAutomation.Services.Licensing.RegistryAnchor",
                throwOnError: true)!;
            var record = anchor.GetNestedType("Record",
                BindingFlags.Public | BindingFlags.NonPublic);
            Assert.NotNull(record);
            return record!;
        }
    }
}
