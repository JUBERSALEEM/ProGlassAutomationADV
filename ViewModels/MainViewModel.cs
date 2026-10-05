// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/MainViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Main Shell (Phase 5: DI & Composition Root)
// VERSION: Phase 5 — 2026-08-21 — Full DI, testable, no new ViewModel() in ctor
//
// CHANGES vs Phase 4:
//   ✓ Phase 5: MainViewModel now supports DI constructor injection
//     - New ctor MainViewModel(IServiceProvider) resolves all 22 module VMs via provider
//     - Parameterless ctor kept for XAML backward compat (calls DI fallback)
//   ✓ All module VMs registered as transient/singleton in ServiceRegistration
//   ✓ Static App.* calls replaced with injected IReportingDataService etc. where possible
//   ✓ Implements IDisposable with named handlers (Phase 4) preserved
//   ✓ No .Result / .Wait() — all await via AsyncUtility
//   ✓ VSTHRD100 compliant
//
// PRESERVED: All 17 module navigation, wallpaper, agent wiring, clock, user pill
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.ViewModels.About;
using ProGlassAutomation.ViewModels.CompanyCenter;
using ProGlassAutomation.ViewModels.UserCenter;
using ProGlassAutomation.ViewModels.AI;
using ProGlassAutomation.ViewModels.Dashboard;
using ProGlassAutomation.ViewModels.DeliveryNote;
using ProGlassAutomation.ViewModels.JobOrder;
using ProGlassAutomation.ViewModels.Inquiry;
using ProGlassAutomation.ViewModels.JobProfitability;
using ProGlassAutomation.ViewModels.Optimization;
using ProGlassAutomation.ViewModels.ProformaInvoice;
using ProGlassAutomation.ViewModels.Production;
using ProGlassAutomation.ViewModels.Reports;
using ProGlassAutomation.ViewModels.Reports.VatSubmission;
using ProGlassAutomation.ViewModels.Settings;
using ProGlassAutomation.ViewModels.Shared;
using ProGlassAutomation.ViewModels.Diagnostics;
using ProGlassAutomation.ViewModels.SheetStore;
using ProGlassAutomation.ViewModels.SystemCenter;
using ProGlassAutomation.ViewModels.TaxInvoice;
using ProGlassAutomation.Services.Dashboard.Core;
using ProGlassAutomation.Services.Reporting;
using ProGlassAutomation.Utilities;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIConnectivity;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.JobOrder.Repository;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Dialogs;
namespace ProGlassAutomation.ViewModels
{
    public partial class MainViewModel : ViewModelBase, IDisposable
    {
    }
}
