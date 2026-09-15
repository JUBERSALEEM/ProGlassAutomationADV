// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.WallpaperStudio.Library.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Settings v3 redesign (2026-09-17): split out of SettingsViewModel.WallpaperStudio.cs so no file in the
// module exceeds the ~350-line budget. Same partial class, members moved
// verbatim at member boundaries — behaviour unchanged.
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
            // 2026-09-05 breadcrumb: if the user ever sees "all wallpaper
            // buttons dead", this status line proving the initializer ran
            // is the first diagnostic (alongside the AppLog entry).
            AppLogService.Debug(
                "[Wallpapers] Studio initialized — commands wired",
                new LogContext { Module = "Wallpapers", Operation = "Init" });
            FetchWallpaperCommand = new RelayCommand(_ => FetchWallpaperAsync());
            ApplyCachedWallpaperCommand = new RelayCommand(p =>
            {
                if (p is string path && File.Exists(path))
                {
                    WallpaperOrchestrator.SetFile(path);
                    WallpaperFetchStatus = $"Applied: {Path.GetFileName(path)}";
                    IsWallpaperFetchError = false;
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
                    IsWallpaperFetchError = false;
                    OnPropertyChanged(nameof(SelectedWallpaperMode));
                }
                catch (Exception ex)
                {
                    WallpaperFetchStatus = $"Delete failed: {ex.Message}";
                    IsWallpaperFetchError = true;
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
                        try { f.Delete(); } catch {
                ProGlassAutomation.Services.Logging.AppLogService.Debug("catch suppressed");
            }
                    }
                    RefreshWallpaperLibrary();
                    WallpaperFetchStatus = "Library cleared (the active wallpaper was kept).";
                    IsWallpaperFetchError = false;
                }
                catch (Exception ex)
                {
                    WallpaperFetchStatus = $"Clear failed: {ex.Message}";
                    IsWallpaperFetchError = true;
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
                    IsWallpaperFetchError = true;
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
                    IsWallpaperFetchError = true;
                }
                else
                {
                    RefreshWallpaperLibrary();
                    WallpaperOrchestrator.SetFile(path);
                    WallpaperFetchStatus =
                        $"Downloaded & applied: {Path.GetFileName(path)}";
                    IsWallpaperFetchError = false;
                    OnPropertyChanged(nameof(SelectedWallpaperMode));
                    OnPropertyChanged(nameof(WallpaperStatus));
                }
            }
            catch (Exception ex)
            {
                WallpaperFetchStatus = $"Download failed: {ex.Message}";
                IsWallpaperFetchError = true;
            }
            finally
            {
                IsWallpaperBusy = false;
            }
        }
    }
}
