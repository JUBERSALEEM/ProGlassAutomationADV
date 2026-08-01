// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings (Chat 23 v3.1 — NEW module)
// VERSION: Chat 23 v3.1 — Extracted from MainViewModel, adds persistence UX
//
// PURPOSE:
//   Consolidates all app settings previously scattered on the right
//   drawer of MainWindow. Now a proper full-screen module (like About)
//   with its own VM, view, and lifecycle.
//
// FEATURES:
//   • Appearance section — Dark Mode toggle (uses ThemeManager)
//   • AI Assistant section — Enable toggle, API key input, Test button
//   • Model & Tuning section — read-only display of current AI config
//   • API key persistence — auto-saves to disk via ConfigService.Save
//   • Visual save indicator — "SAVED ✓" flashes when API key changes
//   • Config location note — shows where config.json lives on disk
//   • ThemeChanged subscription — VM stays synced if theme changes externally
//
// PERSISTENCE:
//   Every property setter that touches AppConfig immediately calls
//   ConfigService.Save(App.Config) — writes JSON to:
//   %AppData%\ProGlassAutomation\config.json
//
//   On next app launch, ConfigService.Load() reads the JSON, so the
//   user NEVER needs to re-enter the API key. This was already true
//   in v3.0 but wasn't visually communicated to users.
//
// REGRESSION RISK: ZERO
//   New file. All persistence logic identical to previous MainViewModel
//   pattern (same ConfigService.Save call), just relocated.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services;
using ProGlassAutomation.Services.AI.AIConfig;
using System;
using System.IO;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Settings;

/// <summary>
/// View-model for the Settings module.
/// Owns theme toggle, AI enable/disable, API key entry, connection test,
/// and read-only display of the current AI tuning parameters.
///
/// All persistence goes through <see cref="ConfigService.Save"/> which
/// writes to <c>%AppData%\ProGlassAutomation\config.json</c>. The user
/// therefore only enters the API key once — it is auto-loaded on every
/// subsequent app launch.
/// </summary>
public sealed class SettingsViewModel : ViewModelBase
{
    private string _aiTestStatus = string.Empty;
    private bool _apiKeyJustSaved;

    /// <summary>
    /// Absolute path of the config file on disk (shown in the UI so
    /// users know where their settings and API key are persisted).
    /// </summary>
    public string ConfigFilePath { get; }

    public SettingsViewModel()
    {
        // Compute the config path once so the UI can display it
        ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ProGlassAutomation",
            "config.json");

        TestAiConnectionCommand = new RelayCommand(async _ =>
        {
            if (App.Ai == null)
            {
                AiTestStatus = "✗ AI service not initialized.";
                return;
            }
            AiTestStatus = "Testing…";
            var result = await App.Ai.TestConnectionAsync();
            AiTestStatus = result.Success
                ? "✓ " + result.Message
                : "✗ " + result.Message;
        });

        // Keep IsDarkTheme in sync if theme is toggled externally
        // (e.g. via a keyboard shortcut in a future release)
        ThemeManager.ThemeChanged += OnThemeChanged;
    }

    // ══════════════════════════════════════════════════════════════════
    //  APPEARANCE
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Two-way bound to the Dark Mode toggle. Setter calls
    /// <see cref="ThemeManager.ApplyTheme"/> which swaps the theme
    /// merged dictionary and raises ThemeChanged.
    /// </summary>
    public bool IsDarkTheme
    {
        get => ThemeManager.CurrentTheme == AppTheme.Dark;
        set
        {
            ThemeManager.ApplyTheme(value ? AppTheme.Dark : AppTheme.Light);
            OnPropertyChanged();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  AI ASSISTANT
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Two-way bound to the "Enable AI" toggle.
    /// Persists to config.json on every change.
    /// </summary>
    public bool IsAiEnabled
    {
        get => App.Config?.Ai?.Enabled ?? false;
        set
        {
            if (App.Config?.Ai != null)
            {
                App.Config.Ai.Enabled = value;
                ConfigService.Save(App.Config);
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gemini API key. Persisted to disk on every setter call so users
    /// never need to re-enter it. Bound from PasswordBox via code-behind
    /// because PasswordBox.Password is not a DependencyProperty.
    /// </summary>
    public string AiApiKey
    {
        get => App.Config?.Ai?.ApiKey ?? string.Empty;
        set
        {
            if (App.Config?.Ai != null && App.Config.Ai.ApiKey != value)
            {
                App.Config.Ai.ApiKey = value;
                ConfigService.Save(App.Config);
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasSavedApiKey));

                // Flash the "SAVED ✓" indicator briefly
                ApiKeyJustSaved = true;
            }
        }
    }

    /// <summary>
    /// True when an API key is stored (used to show a masked "loaded" hint).
    /// </summary>
    public bool HasSavedApiKey =>
        !string.IsNullOrWhiteSpace(App.Config?.Ai?.ApiKey);

    /// <summary>
    /// True right after user changes the key — used by the view to flash
    /// a "SAVED ✓" chip. View is responsible for un-setting it after
    /// its animation completes (or user can toggle it manually via
    /// binding). Kept in VM for pure MVVM.
    /// </summary>
    public bool ApiKeyJustSaved
    {
        get => _apiKeyJustSaved;
        set
        {
            if (_apiKeyJustSaved == value) return;
            _apiKeyJustSaved = value;
            OnPropertyChanged();
        }
    }

    /// <summary>
    /// Status message from the last "Test Connection" click.
    /// Prefixed with ✓ on success, ✗ on failure.
    /// </summary>
    public string AiTestStatus
    {
        get => _aiTestStatus;
        set
        {
            if (_aiTestStatus == value) return;
            _aiTestStatus = value;
            OnPropertyChanged();
        }
    }

    // ══════════════════════════════════════════════════════════════════
    //  MODEL & TUNING (read-only for now — future: editable)
    // ══════════════════════════════════════════════════════════════════

    /// <summary>Current AI model in use (from config).</summary>
    public string AiModel => App.Config?.Ai?.Model ?? "—";

    /// <summary>Max output tokens per request.</summary>
    public int AiMaxTokens => App.Config?.Ai?.MaxTokens ?? 1024;

    /// <summary>Temperature (creativity) parameter for AI responses.</summary>
    public double AiTemperature => App.Config?.Ai?.Temperature ?? 0.7;

    // ══════════════════════════════════════════════════════════════════
    //  COMMANDS
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Tests the Google Gemini API using the currently saved key.
    /// Updates <see cref="AiTestStatus"/> with the result.
    /// </summary>
    public ICommand TestAiConnectionCommand { get; }

    // ══════════════════════════════════════════════════════════════════
    //  THEME SYNC
    // ══════════════════════════════════════════════════════════════════

    /// <summary>
    /// Fires when theme is toggled anywhere in the app.
    /// Notifies IsDarkTheme so the toggle stays in sync if changed
    /// programmatically (e.g. via a future keyboard shortcut).
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(IsDarkTheme));
    }
}