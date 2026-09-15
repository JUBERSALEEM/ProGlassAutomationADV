// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.WallpaperStudio.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Wallpaper Studio (2026-09-05) — Settings → Appearance.
//
//   • WALLPAPER MODE: Theme-linked (dynamic scene follows the current
//     family/accent — the 2026 default) · My file/downloaded · Built-in
//     scene (pinned).
//   • BUILT-IN SCENES: six code-drawn, accent-aware gradients, one per
//     visual family — the swatch in the list IS the live scene brush.
//   • DOWNLOAD 4K: keyless providers (Wallhaven → Picsum fallback) into
//     the local library with STAGE-BY-STAGE status reporting, plus
//     management: delete one, clear all, open folder, size summary.
//   • OS WINDOW BACKDROP: experimental Windows 11 Mica/Acrylic via
//     SystemBackdropService (DWM-rendered behind WPF content).
//
// ROW CLASSES: every row overrides ToString() on purpose — the app-wide
// implicit ComboBox style templates items with raw content, so items
// rendered "ProGlassAutomation.ViewModels…+CategoryRow" typenames
// (owner report). ToString() guarantees a human label under ANY
// template, DisplayMemberPath or not.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Theme;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        // ── Row models ────────────────────────────────────────────────

        /// <summary>One built-in scene row. Preview is the LIVE scene
        /// brush — it re-derives from accent/theme exactly like the
        /// wallpaper behind the modules.</summary>
        public sealed class SceneRow
        {
            public string Key { get; }
            public string Label { get; }
            public Brush Preview { get; }
            public SceneRow(string key, string label)
            {
                Key = key;
                Label = label;
                Preview = BuiltinWallpaperScenes.For(key);
            }
            /// <summary>Style-immune label (see file header).</summary>
            public override string ToString() => Label;
        }

        /// <summary>One online category — real class with properties
        /// AND a style-immune ToString (the owner saw raw typenames in
        /// the picker when templates ignored DisplayMemberPath).</summary>
        public sealed class CategoryRow
        {
            public string Key { get; }
            public string Label { get; }
            public CategoryRow(string key, string label)
            {
                Key = key;
                Label = label;
            }
            public override string ToString() => Label;
        }

        /// <summary>One file in the downloaded library.</summary>
        public sealed class CachedWallpaperRow
        {
            public string Name { get; }
            public string FullPath { get; }
            public string SizeText { get; }
            public CachedWallpaperRow(FileInfo f)
            {
                Name = f.Name;
                FullPath = f.FullName;
                SizeText = f.Length >= 1024 * 1024
                    ? $"{f.Length / 1048576.0:0.0} MB"
                    : $"{f.Length / 1024.0:0} KB";
            }
            public override string ToString() => Name;
        }

        // ── Mode ─────────────────────────────────────────────────────

        public List<string> WallpaperModeOptions { get; } = new()
        {
            "Theme-linked (dynamic)",
            "My file / downloaded",
            "Built-in scene",
        };

        public string SelectedWallpaperMode
        {
            get => WallpaperOrchestrator.EffectiveMode(App.Config?.Appearance) switch
            {
                WallpaperOrchestrator.ModeFile => "My file / downloaded",
                WallpaperOrchestrator.ModeScene => "Built-in scene",
                _ => "Theme-linked (dynamic)",
            };
            set
            {
                switch (value)
                {
                    case "My file / downloaded":
                        var path = App.Config?.Appearance?.WallpaperPath;
                        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
                        {
                            WallpaperOrchestrator.SetFile(path);
                        }
                        else
                        {
                            WallpaperFetchStatus =
                                "No wallpaper file yet — download a 4K wallpaper below or browse one.";
                        }
                        break;
                    case "Built-in scene":
                        WallpaperOrchestrator.SetScene(SelectedBuiltinSceneKey);
                        break;
                    default:
                        WallpaperOrchestrator.SetThemeLinked();
                        break;
                }
                OnPropertyChanged();
                OnPropertyChanged(nameof(WallpaperStatus));
            }
        }

        /// <summary>Six built-in scenes with live preview swatches.</summary>
        public IReadOnlyList<SceneRow> BuiltinSceneRows { get; } =
            BuiltinWallpaperScenes.All
                .Select(s => new SceneRow(s.Key, s.Label))
                .ToList();

        private string? _selectedBuiltinSceneKey;
        public string SelectedBuiltinSceneKey
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(_selectedBuiltinSceneKey))
                    return _selectedBuiltinSceneKey!;
                var saved = App.Config?.Appearance?.BuiltinScene;
                return string.IsNullOrWhiteSpace(saved)
                    ? (VisualThemeEngine.Current ?? VisualThemeEngine.Normal)
                    : saved!;
            }
            set
            {
                _selectedBuiltinSceneKey = value;
                OnPropertyChanged();
            }
        }

        /// <summary>Online 4K categories (key = provider query id).</summary>
        public IReadOnlyList<CategoryRow> OnlineCategoryRows { get; } =
            WallpaperProviderService.Categories
                .Select(c => new CategoryRow(c.Key, c.Label))
                .ToList();

        private string _selectedOnlineCategoryKey = "glass";
        public string SelectedOnlineCategoryKey
        {
            get => _selectedOnlineCategoryKey;
            set { _selectedOnlineCategoryKey = value ?? "glass"; OnPropertyChanged(); }
        }

        // ── Busy / status ────────────────────────────────────────────

        private bool _isWallpaperBusy;
        public bool IsWallpaperBusy
        {
            get => _isWallpaperBusy;
            private set
            {
                _isWallpaperBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsWallpaperIdle));
            }
        }
        /// <summary>Inverse for IsEnabled bindings (only *ToVisibility
        /// converters are app-shared).</summary>
        public bool IsWallpaperIdle => !_isWallpaperBusy;

        private string _wallpaperFetchStatus = "";
        public string WallpaperFetchStatus
        {
            get => _wallpaperFetchStatus;
            set { _wallpaperFetchStatus = value ?? ""; OnPropertyChanged(); }
        }

        /// <summary>True when the LAST wallpaper operation ended in a
        /// failure (download/delete/clear/open-folder). Phase 3: failure
        /// used to be the same grey text as success — the status line now
        /// goes to the danger tint so a broken download is VISIBLE, not
        /// just logged. Set at every terminal outcome below; reset on the
        /// next success.</summary>
        private bool _isWallpaperFetchError;
        public bool IsWallpaperFetchError
        {
            get => _isWallpaperFetchError;
            private set
            {
                if (_isWallpaperFetchError == value) return;
                _isWallpaperFetchError = value;
                OnPropertyChanged();
            }
        }

    }
}
