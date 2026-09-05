// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.WallpaperStudio.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Wallpaper Studio (2026-09-05) — Settings → Appearance.
//
//   • WALLPAPER MODE: Theme-linked (dynamic scene follows the current
//     family/accent) · My file/downloaded · Built-in scene (pinned).
//   • BUILT-IN SCENES: six code-drawn, accent-aware gradients, one per
//     visual family — the swatch in the list IS the live scene brush.
//   • DOWNLOAD 4K: keyless providers (Wallhaven → Picsum fallback) into
//     the local library; applying is instant and offline afterwards.
//   • LIBRARY: everything downloaded so far, newest first; one tap
//     applies through WallpaperOrchestrator.SetFile (full theme chain).
//
// All mutations go through WallpaperOrchestrator, which persists and
// repaints via the proven AppearanceService.ApplyTheme chain — this VM
// never touches resource dictionaries directly.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Theme;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;

namespace ProGlassAutomation.ViewModels.Settings
{
    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        // ── Wallpaper Studio state ────────────────────────────────────

        /// <summary>One row in the built-in scene list. Preview is the
        /// LIVE scene brush — it re-derives from accent/theme exactly
        /// like the wallpaper behind the modules.</summary>
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
        }

        /// <summary>One online category — a REAL class (ValueTuple
        /// element names are not CLR properties; ComboBox's
        /// DisplayMemberPath/SelectedValuePath would show blank rows).</summary>
        public sealed class CategoryRow
        {
            public string Key { get; }
            public string Label { get; }
            public CategoryRow(string key, string label)
            {
                Key = key;
                Label = label;
            }
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
        }

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
                            // No file yet — stay put and point the user at
                            // the download row / browse button instead of
                            // silently swapping the wallpaper out.
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
                    ? (VisualThemeEngine.Current ?? VisualThemeEngine.LiquidGlass)
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

        /// <summary>Downloaded wallpapers, newest first (cap 30 rows —
        /// text rows, no 4K thumbnails decoded into memory).</summary>
        public ObservableCollection<CachedWallpaperRow> CachedWallpapers { get; } = new();

        public ICommand FetchWallpaperCommand { get; private set; }
        public ICommand ApplyCachedWallpaperCommand { get; private set; }
        public ICommand ApplyBuiltinSceneCommand { get; private set; }
        public ICommand RefreshWallpaperLibraryCommand { get; private set; }

        private void InitializeWallpaperStudio()
        {
            FetchWallpaperCommand = new RelayCommand(_ => FetchWallpaperAsync());
            ApplyCachedWallpaperCommand = new RelayCommand(p =>
            {
                if (p is string path && File.Exists(path))
                {
                    WallpaperOrchestrator.SetFile(path);
                    WallpaperFetchStatus = $"Applied: {Path.GetFileName(path)}";
                    OnPropertyChanged(nameof(SelectedWallpaperMode));
                    OnPropertyChanged(nameof(WallpaperStatus));
                }
            });
            ApplyBuiltinSceneCommand = new RelayCommand(p =>
            {
                if (p is string key)
                {
                    SelectedBuiltinSceneKey = key;
                    WallpaperOrchestrator.SetScene(key);
                    WallpaperFetchStatus = $"Scene applied: {key}";
                    OnPropertyChanged(nameof(SelectedWallpaperMode));
                    OnPropertyChanged(nameof(WallpaperStatus));
                }
            });
            RefreshWallpaperLibraryCommand =
                new RelayCommand(_ => RefreshWallpaperLibrary());

            RefreshWallpaperLibrary();
        }

        private void RefreshWallpaperLibrary()
        {
            CachedWallpapers.Clear();
            foreach (var f in WallpaperProviderService.Library().Take(30))
                CachedWallpapers.Add(new CachedWallpaperRow(f));
        }

        /// <summary>Downloads one random 4K wallpaper for the selected
        /// category (Wallhaven → Picsum fallback), adds it to the local
        /// library, and applies it as the File wallpaper.</summary>
        private async void FetchWallpaperAsync()
        {
            if (_isWallpaperBusy) return;
            IsWallpaperBusy = true;
            WallpaperFetchStatus = "Contacting wallpaper service…";
            try
            {
                var path = await WallpaperProviderService
                    .FetchRandomAsync(_selectedOnlineCategoryKey)
                    .ConfigureAwait(true);

                if (path == null)
                {
                    WallpaperFetchStatus =
                        "Download failed — check the internet connection and try again.";
                }
                else
                {
                    RefreshWallpaperLibrary();
                    WallpaperOrchestrator.SetFile(path);
                    WallpaperFetchStatus =
                        $"Downloaded & applied: {Path.GetFileName(path)}";
                    OnPropertyChanged(nameof(SelectedWallpaperMode));
                    OnPropertyChanged(nameof(WallpaperStatus));
                }
            }
            catch (Exception ex)
            {
                WallpaperFetchStatus = $"Download failed: {ex.Message}";
            }
            finally
            {
                IsWallpaperBusy = false;
            }
        }
    }
}
