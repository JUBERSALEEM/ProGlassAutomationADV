// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings (Chat 25 v1.1 — CS0272 fix)
//
// CHANGES vs Chat 25 v1.0:
//   ✓ FIX CS0272: _speechToText setter is now public (fixed in
//     App.xaml.cs). All three assignment sites now compile correctly:
//       _speechToText = new WhisperSpeechService()  ← line 151
//       _speechToText?.Dispose()                    ← line 196
//       _speechToText = new SpeechToTextService()   ← line 197
//   ✓ FIX CS0428: WhisperModelManager.IsModelDownloaded now resolves
//     as bool property — line 498 compiles correctly.
//   ✓ All other code preserved exactly from Chat 25 v1.0.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Fonts;
using System;
using ProGlassAutomation.Utilities;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        private readonly AppConfig _config = null!;
        private readonly IDialogService _dialogs = null!;
        private ISpeechToTextService _speechToText = null!;
        private readonly IDatabaseServices _database = null!;
        private readonly Services.AI.IAiProviderEvents _providerEvents = null!;

        public SettingsViewModel(IAiService? ai, AppConfig? config, IDialogService? dialogs, ISpeechToTextService? speechToText, IDatabaseServices? database, Services.AI.IAiProviderEvents? providerEvents = null)
        {
            _config = config!;
            _dialogs = dialogs!;
            _speechToText = speechToText!;
            _database = database!;
            _providerEvents = providerEvents ?? new Services.AI.AiProviderEvents();
        
            // ERR-633EEB: run shared initialization on EVERY construction
            // path. DI selects this (greediest) ctor, so without this call the
            // parameterless ctor's setup never ran and commands stayed null.
            InitializeCore();
        }

        // Phase 4: Named handlers for static events — detachable
        private EventHandler? _themeChangedHandler;
        private EventHandler? _uiScaleChangedHandler;
        private bool _disposed;


        // ══════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════

        public SettingsViewModel()
         : this(
            App.ServiceProvider?.GetService<IAiService>()!,
            App.ServiceProvider?.GetService<AppConfig>()?? App.Config,
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<ISpeechToTextService>()!,
            App.ServiceProvider?.GetService<IDatabaseServices>()!){ }

        /// <summary>Sharing Center page (v3 pilot: first page-scoped VM).
        /// The page template binds the section's DataContext to this.</summary>
        public ProGlassAutomation.ViewModels.Settings.Pages.SettingsSharingPageViewModel SharingPage { get; private set; } = null!;

        /// <summary>Voice page (v3 promotion #2: Whisper picker/downloader).
        /// The page template binds the section's DataContext to this.</summary>
        public ProGlassAutomation.ViewModels.Settings.Pages.SettingsVoicePageViewModel VoicePage { get; private set; } = null!;

        /// <summary>Updates page (v3 promotion #3: OTA check/download/install).
        /// The page template binds the section's DataContext to this.</summary>
        public ProGlassAutomation.ViewModels.Settings.Pages.SettingsUpdatesPageViewModel UpdatesPage { get; private set; } = null!;

        /// <summary>AI Assistant page (v3 promotion #4: providers, keys,
        /// tests, security). The page template binds the section's
        /// DataContext to this; the three Ai cards inherit it.</summary>
        public ProGlassAutomation.ViewModels.Settings.Pages.SettingsAiPageViewModel AiPage { get; private set; } = null!;

        /// <summary>Pass-through for the About section, which shows the app
        /// version too. The Updates page VM has its own identical copy —
        /// both read the static Services.Updates.AppInfo.Version.</summary>
        public string CurrentVersionDisplay => Services.Updates.AppInfo.Version;

        // ── Shared initialization (ERR-633EEB) ─────────────────────
        // Extracted verbatim from the parameterless ctor so both it
        // and the DI-selected parameterized ctor initialize fully.
        private void InitializeCore()
        {
            SharingPage = new ProGlassAutomation.ViewModels.Settings.Pages.SettingsSharingPageViewModel(_config);
            VoicePage = new ProGlassAutomation.ViewModels.Settings.Pages.SettingsVoicePageViewModel(
                _config,
                () => _speechToText,
                v => _speechToText = v!);
            UpdatesPage = new ProGlassAutomation.ViewModels.Settings.Pages.SettingsUpdatesPageViewModel();
            AiPage = new ProGlassAutomation.ViewModels.Settings.Pages.SettingsAiPageViewModel(
                _config, _dialogs, _providerEvents);
            _config.DataConnectivity ??= new DataConnectivitySettings();

            // v2 shell: navigation between the settings pages.
            SwitchSettingsPageCommand = new RelayCommand(
                p => SettingsPage = p as string ?? "Appearance");

            // Features page: switch which module's field-toggles are shown.
            SwitchFeaturesModuleCommand = new RelayCommand(
                p => FeaturesModule = p as string ?? "Proforma");

            ConfigFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ProGlassAutomation",
                "config.json");


            ResetUiScaleCommand = new RelayCommand(_ =>
                UiScale = Services.Viewport.ViewportService.DefaultUiScale);

            // Phase 4: Named handlers — no lambda leak
            _themeChangedHandler = (_, __) => OnPropertyChanged(nameof(IsDarkTheme));
            ThemeManager.ThemeChanged += _themeChangedHandler;

            _uiScaleChangedHandler = (_, __) =>
            {
                OnPropertyChanged(nameof(UiScale));
                OnPropertyChanged(nameof(UiScalePercent));
            };
            ThemeManager.UiScaleChanged += _uiScaleChangedHandler;

            // Dynamic Theme Engine — accent presets (Settings UI)
            ApplyAccentCommand = new RelayCommand(param =>
                ApplyAccentPreset(param as string ?? "GlassBlue"));

            // 2026-08-25 — colour palette expansion: free-pick custom
            // colour + palette generated from AccentPresets.All so the
            // swatches shown ARE the colours applied.
            ApplyCustomAccentCommand = new RelayCommand(_ => ApplyCustomAccent());
            InitializeAccentPalette();

            // 2026-09-07 — card-edge colour plate: the same preset palette
            // (AccentPresets.All) drives the "Define Card Edges" swatches;
            // picking one applies it to every card border app-wide.
            ApplyCardEdgePresetCommand = new RelayCommand(param =>
                ApplyCardEdgePreset(param as string ?? ""));
            InitializeCardEdgePalette();
            ApplyCardFrostTintCommand = new RelayCommand(param =>
                ApplyCardFrostTintPreset(param as string ?? ""));

            // 2026-08-25 — global font colour: one user-chosen text
            // colour for the whole UI (Auto = theme/family palette).
            ApplyFontColorCommand = new RelayCommand(param =>
                ApplyFontColorPreset(param as string ??
                    ProGlassAutomation.Services.Theme.FontColorPresets.Auto));
            ApplyCustomFontColorCommand =
                new RelayCommand(_ => ApplyCustomFontColor());
            InitializeFontColorPalette();

            // 2026-08-25 — one-tap factory reset for everything the
            // Appearance module owns (theme, accent, gradients, card
            // shape, density, zoom, font, wallpapers, PDF layout).
            ResetAppearanceCommand = new RelayCommand(async _ => await ResetAppearanceToDefaultsAsync());

            // Global font engine (system-wide UI font)
            InitializeFontSettings();

            // v2 sections: security keys, OTA updates, backup & restore
            InitializeV2Sections();
        }

    }
}
