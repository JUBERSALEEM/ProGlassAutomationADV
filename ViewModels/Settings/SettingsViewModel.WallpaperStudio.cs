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

        // ── Library ──────────────────────────────────────────────────

        /// <summary>Downloaded wallpapers, newest first (text rows — no
        /// 4K thumbnails decoded into memory).</summary>
        public ObservableCollection<CachedWallpaperRow> CachedWallpapers { get; } = new();

        private string _wallpaperLibrarySummary = "";
        public string WallpaperLibrarySummary
        {
            get => _wallpaperLibrarySummary;
            private set { _wallpaperLibrarySummary = value; OnPropertyChanged(); }
        }

        public ICommand FetchWallpaperCommand { get; private set; }
        public ICommand ApplyCachedWallpaperCommand { get; private set; }
        public ICommand DeleteCachedWallpaperCommand { get; private set; }
        public ICommand ClearWallpaperLibraryCommand { get; private set; }
        public ICommand OpenWallpaperFolderCommand { get; private set; }
        public ICommand ApplyBuiltinSceneCommand { get; private set; }
        public ICommand SetWindowBackdropCommand { get; private set; }
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
            DeleteCachedWallpaperCommand = new RelayCommand(p =>
            {
                if (p is not string path || !File.Exists(path)) return;
                try
                {
                    File.Delete(path);
                    // Deleting the ACTIVE wallpaper falls back to the
                    // theme-linked scene instead of a broken path.
                    if (string.Equals(
                            App.Config?.Appearance?.WallpaperPath, path,
                            StringComparison.OrdinalIgnoreCase))
                        WallpaperOrchestrator.SetThemeLinked();
                    RefreshWallpaperLibrary();
                    WallpaperFetchStatus = $"Deleted: {Path.GetFileName(path)}";
                    OnPropertyChanged(nameof(SelectedWallpaperMode));
                }
                catch (Exception ex)
                {
                    WallpaperFetchStatus = $"Delete failed: {ex.Message}";
                }
            });
            ClearWallpaperLibraryCommand = new RelayCommand(_ =>
            {
                try
                {
                    var active = App.Config?.Appearance?.WallpaperPath ?? "";
                    foreach (var f in WallpaperProviderService.Library())
                    {
                        if (string.Equals(f.FullName, active,
                                StringComparison.OrdinalIgnoreCase))
                            continue; // never delete the live wallpaper
                        try { f.Delete(); } catch { /* locked file — skip */ }
                    }
                    RefreshWallpaperLibrary();
                    WallpaperFetchStatus = "Library cleared (the active wallpaper was kept).";
                }
                catch (Exception ex)
                {
                    WallpaperFetchStatus = $"Clear failed: {ex.Message}";
                }
            });
            OpenWallpaperFolderCommand = new RelayCommand(_ =>
            {
                // 2026-09-05 deep-debug ("Open folder still does nothing"):
                // ShellExecute on a directory path relies on the shell's
                // folder handler — hardened PC policies can swallow it with
                // NO exception (button looks dead). Launch explorer.exe
                // EXPLICITLY (works regardless of shell handlers), report
                // success visibly, and fall back to the shell verb only if
                // the explicit launcher itself throws.
                bool opened = false;
                string dir = WallpaperProviderService.CacheDir;
                try
                {
                    Directory.CreateDirectory(dir);
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "explorer.exe",
                        Arguments = $"\"{dir}\"",
                        UseShellExecute = true,
                    });
                    opened = true;
                }
                catch (Exception ex1)
                {
                    AppLogService.Warning(
                        $"explorer.exe launch failed, falling back: {ex1.Message}",
                        new LogContext { Module = "Wallpapers", Operation = "OpenFolder" });
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = dir,
                            UseShellExecute = true,
                            Verb = "open",
                        });
                        opened = true;
                    }
                    catch (Exception ex2)
                    {
                        AppLogService.Warning(
                            $"Open folder failed ({dir}): {ex2.Message}",
                            new LogContext { Module = "Wallpapers", Operation = "OpenFolder" });
                        WallpaperFetchStatus =
                            $"Open folder failed: {ex2.Message} — the library lives at {dir}";
                    }
                }
                if (opened)
                    WallpaperFetchStatus = $"Library folder opened: {dir}";
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
            SetWindowBackdropCommand = new RelayCommand(p =>
            {
                if (p is string mode)
                {
                    WallpaperOrchestrator.SetWindowBackdrop(mode);
                    WallpaperFetchStatus =
                        mode == SystemBackdropService.Off
                            ? "Window backdrop off — painted liquid-glass stage restored."
                            : SystemBackdropService.IsSupported
                                ? $"Window backdrop: {mode} (OS-rendered behind the app)."
                                : $"{mode} needs Windows 11 22H2+; the painted stage was kept.";
                }
            });
            RefreshWallpaperLibraryCommand =
                new RelayCommand(_ => RefreshWallpaperLibrary());

            RefreshWallpaperLibrary();
        }

        private void RefreshWallpaperLibrary()
        {
            var files = WallpaperProviderService.Library();
            CachedWallpapers.Clear();
            foreach (var f in files.Take(30))
                CachedWallpapers.Add(new CachedWallpaperRow(f));

            long bytes = files.Sum(f => f.Length);
            WallpaperLibrarySummary = files.Count == 0
                ? "Library is empty — download a 4K wallpaper above."
                : $"{files.Count} wallpaper(s) · {bytes / 1048576.0:0.0} MB stored in the local library";
        }

        /// <summary>Downloads one random 4K wallpaper for the selected
        /// category (Wallhaven → Picsum fallback), adds it to the local
        /// library, and applies it as the File wallpaper. The provider
        /// reports every stage into the status line, so a working
        /// download is VISIBLE while it happens.</summary>
        private async void FetchWallpaperAsync()
        {
            if (_isWallpaperBusy) return;
            IsWallpaperBusy = true;
            try
            {
                var path = await WallpaperProviderService
                    .FetchRandomAsync(
                        _selectedOnlineCategoryKey,
                        s => WallpaperFetchStatus = s)
                    .ConfigureAwait(true);

                if (path == null)
                {
                    WallpaperFetchStatus =
                        "Download failed — check the internet connection and try again. " +
                        "Details were written to the app log.";
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
