// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Agent/ServiceRegistrationCompositionTests.cs
// PROJECT: ProGlassAutomation.Tests
// MODULE: Tests — DI composition root coherence
//
// WHY THIS SUITE EXISTS
// ─────────────────────
// StartupCoreAsync builds the DI provider once (BuildProGlassProvider)
// and resolves MainViewModel through it. MS.DI does NOT validate the
// graph at build time — it only throws InvalidOperationException when a
// registered service's constructor has a dependency that was never
// registered. That failure mode first surfaced as two caught
// InvalidOperationException during startup (MainViewModel is constructed
// twice: once from the XAML DataContext, once via GetRequiredService).
//
// The bug: ViewModels.AI.AiChatViewModel was registered as a transient
// but its constructor needs IAiService / ISpeechToTextService /
// ITextToSpeechService — which are App runtime statics, not DI services.
// Resolving it therefore threw. The fix registered those interfaces as
// singletons (factory reading the App statics). Idem for the dashboard
// metrics service. These tests pin the contract so a future refactor
// cannot silently re-add an unresolvable registration.
//
// Hermetic: no App config, no DB, no UI. Just the DI container.
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.Extensions.DependencyInjection;
using ProGlassAutomation.Services.DependencyInjection;
using ProGlassAutomation.ViewModels.AI;
using ProGlassAutomation.ViewModels.Dashboard;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ServiceRegistrationCompositionTests
    {
        [Fact]
        public void AiChatViewModel_is_constructible_via_container()
        {
            // App.Ai / App.SpeechToText / App.TextToSpeech may be null in a
            // headless test host; AiChatViewModel tolerates null services
            // (the DI factory returns the App statics, which are null here).
            using var provider = ServiceRegistration.BuildProGlassProvider();

            // Must NOT throw InvalidOperationException ("Unable to resolve
            // service for type 'IAiService'..."). The regression was a
            // registered AiChatViewModel with unregistered ctor deps.
            var vm = provider.GetService<AiChatViewModel>();

            Assert.NotNull(vm);
        }

        [Fact]
        public void MainViewModel_is_constructible_via_container()
        {
            // MainViewModel is the shell resolved in StartupCoreAsync.
            // Its ctor resolves ~26 module VMs via GetService; any one of
            // those being registered-but-unconstructable would throw here.
            using var provider = ServiceRegistration.BuildProGlassProvider();

            var vm = provider.GetRequiredService<ProGlassAutomation.ViewModels.MainViewModel>();

            Assert.NotNull(vm);
            Assert.NotNull(vm.DashboardVM);
            Assert.NotNull(vm.AiChatVM);
        }

        [Fact]
        public void Dashboard_sub_viewmodels_resolve_without_unregistered_deps()
        {
            // TiDashboardViewModel needs ITaxInvoiceMetricsService, which
            // was missing from the container. Resolve every registered
            // dashboard sub-VM to confirm the graph is complete.
            using var provider = ServiceRegistration.BuildProGlassProvider();

            Assert.NotNull(provider.GetService<DashboardViewModel>());
            Assert.NotNull(provider.GetService<ProGlassAutomation.ViewModels.Dashboard.TaxInvoice.TiDashboard.TiDashboardViewModel>());
        }
    }
}
