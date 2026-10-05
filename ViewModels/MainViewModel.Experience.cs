// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/MainViewModel.Experience.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// GB-P14 (2026-09-18) — GLASS BLUSH PERSONAL UI EXPERIENCE SWITCH.
//
// A real, user-accessible, PERSISTED Professional ↔ Personal switch on the
// SAME MainViewModel (the ThemeManager-adjacent architecture the app
// already uses for base theme: App.Config.Appearance + SaveDebounced).
//
// DESIGN (minimal blast radius on the frozen shell):
//   * IsPersonalExperience — persisted bool (App.Config.Appearance.
//     UiExperience). Loaded via a null-safe field initializer so it is
//     settled before the first CurrentViewModel assignment in the ctor.
//   * ProfessionalContent / PersonalContent — the ONLY two properties the
//     shells bind their module hosts to. Exactly one is non-null at any
//     time, so exactly ONE module view instance exists (no double
//     instantiation, no double data loads). The module VM (CurrentViewModel)
//     itself is untouched — switching experiences preserves module state
//     (selected document, filters, VM data); only the VIEW is recreated
//     (safe shell recreation — documented in GLASS_BLUSH_IMPLEMENTATION.md).
//   * UpdateExperienceContent() is called from the CurrentViewModel setter
//     (one added line in MainViewModel.Part1.cs) so content follows module
//     navigation in either experience.
//   * ToggleExperienceCommand — lazy, no ctor edit needed.
//
// No business logic is touched: navigation, licensing, popups, AI and the
// module VMs behave identically in both experiences.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIConfig;

namespace ProGlassAutomation.ViewModels
{
    public partial class MainViewModel
    {
        // Loaded before the ctor body runs (field initializers), so the
        // first `CurrentViewModel = DashboardVM` already publishes the
        // correct experience content. Null-safe: a missing config means
        // the default Professional experience.
        private bool _isPersonalExperience = LoadExperiencePreference();

        private System.Windows.Input.ICommand? _toggleExperienceCommand;

        /// <summary>
        /// True = the Glass Blush Personal experience is active.
        /// Persisted to App.Config.Appearance.UiExperience on every change.
        /// </summary>
        public bool IsPersonalExperience
        {
            get => _isPersonalExperience;
            set
            {
                if (_isPersonalExperience == value) return;
                _isPersonalExperience = value;
                PersistExperience();
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProfessionalExperience));
                UpdateExperienceContent();

                // SENIOR FIX 2026-09-19: experience switch must NOT carry
                // AI overlays open. Previously AiChatVM.IsOpen and
                // IsAgentChatOpen survived the switch, so opening AI in
                // Professional then switching to Personal left a 920x640
                // centered chat covering all Personal modules with no
                // obvious close affordance (user reported "ai window open
                // in personal ui switch mode"). Now both chats close on
                // every experience switch — only top-panel buttons may
                // open them in the new experience.
                try
                {
                    IsAgentChatOpen = false;
                    IsSettingsOpen = false;
                    IsNotificationsOpen = false;
                    IsScreenshotOpen = false;
                    IsUserSwitchOpen = false;
                    IsScreenRecordOpen = false;
                    IsStorageOpen = false;
                    IsShellMoreOpen = false;
                    if (AiChatVM != null) AiChatVM.IsOpen = false;
                }
                catch { }
            }
        }

        /// <summary>Negation for inverse-visibility bindings.</summary>
        public bool IsProfessionalExperience => !_isPersonalExperience;

        /// <summary>
        /// Module host content for the PROFESSIONAL chrome: the current
        /// module VM, or null while the Personal experience is active
        /// (null = no Professional view instance exists).
        /// </summary>
        public object? ProfessionalContent { get; private set; }

        /// <summary>
        /// Module host content for the PERSONAL (Glass Blush) shell: the
        /// current module VM, or null while the Professional experience is
        /// active (null = no Personal view instance exists).
        /// </summary>
        public object? PersonalContent { get; private set; }

        /// <summary>
        /// Switches between the Professional and Personal experiences.
        /// Live (no restart): the shells swap chrome, the module VM and its
        /// state are preserved; only the view is recreated.
        /// </summary>
        public System.Windows.Input.ICommand ToggleExperienceCommand =>
            _toggleExperienceCommand ??= new RelayCommand(_ => IsPersonalExperience = !IsPersonalExperience);

        /// <summary>
        /// Republishes ProfessionalContent/PersonalContent from the current
        /// experience + module. Called from the CurrentViewModel setter and
        /// from the IsPersonalExperience setter.
        /// </summary>
        private void UpdateExperienceContent()
        {
            ProfessionalContent = _isPersonalExperience ? null : (object?)_currentViewModel;
            PersonalContent = _isPersonalExperience ? (object?)_currentViewModel : null;
            OnPropertyChanged(nameof(ProfessionalContent));
            OnPropertyChanged(nameof(PersonalContent));
        }

        private void PersistExperience()
        {
            try
            {
                var config = App.Config;
                if (config?.Appearance == null) return;
                config.Appearance.UiExperience = _isPersonalExperience ? "Personal" : "Professional";
                ConfigService.SaveDebounced(config);
            }
            catch (Exception ex)
            {
                // Persistence is best-effort: a failed write must never
                // block the live switch (in-memory state is authoritative
                // for this session).
                ProGlassAutomation.Services.Logging.AppLogService.Debug(
                    "[MainViewModel] UiExperience persist failed: "
                    + ProGlassAutomation.Utilities.ExceptionMessages.Deep(ex));
            }
        }

        private static bool LoadExperiencePreference()
        {
            try
            {
                var experience = App.Config?.Appearance?.UiExperience;
                return string.Equals(experience, "Personal", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false; // default: Professional
            }
        }
    }
}
