using ProGlassAutomation.Agent.Demo.Automation;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Demo.Workflows
{
    /// <summary>
    /// Verified guided tour for modules that do not own a destructive demo
    /// transaction. It lands on the live page, waits for paint, then speaks
    /// the catalog beats while that page is on screen. Settings and Features
    /// also step through their inner pages so the voice never describes a
    /// section the recorder cannot see.
    /// </summary>
    public sealed class ModuleTourWorkflow : IDemoWorkflowRunner
    {
        private static readonly string[] SettingsPages =
            { "Appearance", "AI", "Voice", "Data", "Updates" };

        private static readonly string[] SettingsTitles =
        {
            "Appearance",
            "AI and providers",
            "Voice",
            "Data and backup",
            "Updates"
        };

        private static readonly string[] FeaturesModules =
            { "Proforma", "Inquiry", "JobOrder", "DeliveryNote", "TaxInvoice" };

        private static readonly string[] FeaturesTitles =
        {
            "Proforma Invoice fields",
            "Inquiry fields",
            "Job Order fields",
            "Delivery Note fields",
            "Tax Invoice fields"
        };

        private readonly string _title;
        private readonly string _purpose;
        private readonly IReadOnlyList<string> _beats;

        public ModuleTourWorkflow(string id, string navigate, string title, string purpose)
        {
            ModuleId = id;
            _title = title;
            _purpose = purpose;
            var catalog = DemoWorkflowCatalog.Find(id);
            _beats = catalog?.NarrationBeats is { Count: > 0 } list
                ? list
                : new[] { purpose };
            _ = navigate;
        }

        public string ModuleId { get; }
        public string OutputFileName => ModuleId + "_Demo.mp4";
        public IReadOnlyList<string> DeepEvidenceIds { get; } =
            new[] { "module_ready", "workspace", "controls" };

        public async Task<IReadOnlyList<VerifiedEvent>> RunAsync(CancellationToken ct)
        {
            var events = new List<VerifiedEvent>();
            if (!DemoWalk.LandOnModule(ModuleId)) return events;
            await WpfUiDriver.WaitReadyAsync(900, ct).ConfigureAwait(false);

            int count = Math.Max(3, _beats.Count);
            for (int i = 0; i < count; i++)
            {
                await AdvanceInnerPageAsync(i, ct).ConfigureAwait(false);
                await DemoWalk.PresentAsync(
                    events,
                    BeatId(i),
                    BeatTitle(i),
                    BeatNarration(i),
                    "Module=" + ModuleId + " beat=" + (i + 1),
                    DemoWalk.HoldMs, ct).ConfigureAwait(false);
            }

            return events;
        }

        private async Task AdvanceInnerPageAsync(int index, CancellationToken ct)
        {
            if (string.Equals(ModuleId, "Settings", StringComparison.OrdinalIgnoreCase)
                && index < SettingsPages.Length)
            {
                await WpfUiDriver.OnUiAsync(
                    () => DemoWalk.OpenSettingsPage(SettingsPages[index]), ct)
                    .ConfigureAwait(false);
                await WpfUiDriver.WaitReadyAsync(500, ct).ConfigureAwait(false);
                return;
            }

            if (string.Equals(ModuleId, "Features", StringComparison.OrdinalIgnoreCase)
                && index < FeaturesModules.Length)
            {
                await WpfUiDriver.OnUiAsync(
                    () => DemoWalk.SwitchFeaturesModule(FeaturesModules[index]), ct)
                    .ConfigureAwait(false);
                await WpfUiDriver.WaitReadyAsync(400, ct).ConfigureAwait(false);
            }
        }

        private string BeatId(int index) => index switch
        {
            0 => "module_ready",
            1 => "workspace",
            2 => "controls",
            _ => "tour_" + (index + 1)
        };

        private string BeatTitle(int index)
        {
            if (string.Equals(ModuleId, "Settings", StringComparison.OrdinalIgnoreCase)
                && index < SettingsTitles.Length)
                return SettingsTitles[index];
            if (string.Equals(ModuleId, "Features", StringComparison.OrdinalIgnoreCase)
                && index < FeaturesTitles.Length)
                return FeaturesTitles[index];
            return index switch
            {
                0 => _title,
                1 => "Live operational workspace",
                2 => "Controls and traceability",
                _ => _title + " — continued"
            };
        }

        private string BeatNarration(int index)
        {
            if (index < _beats.Count && !string.IsNullOrWhiteSpace(_beats[index]))
                return _beats[index];
            return index switch
            {
                0 => string.IsNullOrWhiteSpace(_purpose)
                    ? "This is the live " + _title + " workspace in ProGlass Automation."
                    : _purpose,
                1 => "This is the real application workspace, using the same filters, records, permissions and validation available during normal operation. The demonstration remains isolated, so production records are not changed.",
                _ => "Use the visible search, status, action and detail controls to move from an overview into the underlying work. Every completed operation remains traceable through the application audit and reporting services."
            };
        }
    }
}
