// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Optimization/OptimizationManualCutListReentryTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Tests — Optimization module open crash (2026-09-17)
//
// WHAT THIS PINS
// Opening the Optimization module used to kill the process instantly,
// with no log line and no crash report. Root cause:
// OptimizationViewModel.ManualCutList is a lazy property
// (`_manualCutList ??= CreateManualCutList()`), and CreateManualCutList
// subscribed the group's PropertyChanged hook and set a default
// SelectedGlassType BEFORE the backing field was assigned. Both steps
// raise change notifications synchronously; the CUT LIST tab binds
// CutListCompositionDisplay (whose getter reads ManualCutList) and a
// dozen ManualCutList.* paths, so those notifications re-read the lazy
// property while it was still null — re-entering CreateManualCutList
// without limit. That is a StackOverflowException: uncatchable in .NET,
// which is also why no exception handler ever ran and nothing was logged.
//
// HOW THIS TEST REPRODUCES IT WITHOUT WPF
// The listener below plays exactly the role the title-row TextBlock
// binding plays in production: every time CutListCompositionDisplay is
// raised, it re-reads ManualCutList. Against the pre-fix code the very
// first read of ManualCutList recurses until the stack overflows and the
// test host process dies — a hard crash is the expected failure mode and
// the point of the pin. Against the fixed code (backing field published
// before any notification can fire) every re-entrant read returns the
// already-created instance and the test passes.
//
// NOTE ON CONSTRUCTION
// `new OptimizationViewModel()` starts the same fire-and-forget database
// loads the real app starts (constructor contract, documented in the
// class). With no App.ServiceProvider / no Application.Current present in
// tests those paths short-circuit through their null guards and
// try/catch; they never touch the UI.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.ViewModels.Optimization;
using Xunit;

namespace ProGlassAutomation.Tests.Optimization
{
    public sealed class OptimizationManualCutListReentryTests
    {
        [Fact]
        public void ManualCutList_FirstRead_WithReentrantNotificationListener_DoesNotReenterCreation()
        {
            var vm = new OptimizationViewModel();

            int reentrantReads = 0;
            object? reentrantResult = null;

            // The production trigger: the title row of the CUT LIST tab
            // binds CutListCompositionDisplay, whose getter reads
            // ManualCutList. Every notification raised while the group is
            // being created therefore re-reads the lazy property.
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(vm.CutListCompositionDisplay))
                    return;

                // A bounded, defensive cap: if creation ever re-enters
                // itself the count runs away long before this — the cap
                // turns "process dies" into a failing assert for the
                // shallow-recursion variants.
                if (reentrantReads >= 100)
                    throw new InvalidOperationException(
                        "ManualCutList creation is re-entering itself — " +
                        "the lazy initialisation is raising notifications " +
                        "before its backing field is assigned.");

                reentrantReads++;
                reentrantResult = vm.ManualCutList;
            };

            // The first read. Pre-fix this call stack-overflowed the test
            // host (every notification re-entered CreateManualCutList).
            var group = vm.ManualCutList;

            Assert.NotNull(group);

            // Every re-entrant read must have returned the SAME instance —
            // proof creation never ran twice and no duplicate SpecGroup was
            // leaked on the static spec-catalogue hook.
            Assert.Same(group, reentrantResult);
            Assert.Same(group, vm.ManualCutList);

            // The default build type was applied exactly once, on the one
            // instance, and survived the re-entrant storm.
            Assert.False(string.IsNullOrWhiteSpace(group.SelectedGlassType));

            // The composition display reads through the same property and
            // must be servable without recursing.
            Assert.False(string.IsNullOrWhiteSpace(vm.CutListCompositionDisplay));

            // Notifications did fire (the default glass type raises one),
            // so the listener really exercised the re-entry window.
            Assert.True(reentrantReads >= 1,
                "Expected at least one CutListCompositionDisplay " +
                "notification during first creation — the re-entry window " +
                "was not exercised.");
        }

        [Fact]
        public void ManualCutList_SecondRead_ReturnsSameInstance()
        {
            var vm = new OptimizationViewModel();

            var first = vm.ManualCutList;
            var second = vm.ManualCutList;

            Assert.Same(first, second);
        }
    }
}
