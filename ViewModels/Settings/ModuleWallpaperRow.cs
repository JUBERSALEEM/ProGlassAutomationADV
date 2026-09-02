// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/ModuleWallpaperRow.cs
// PROJECT: ProGlassAutomation
// MODULE: Settings — module wallpapers
//
// One configurable row on the Module Wallpapers list. Extracted from the
// large SettingsViewModel file (Phase 11/12 God-VM split) — behaviour
// unchanged, relocated for organisation only.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.Theme;
using ProGlassAutomation.ViewModels;

namespace ProGlassAutomation.ViewModels.Settings
{
    /// <summary>One configurable row on the Module Wallpapers list.</summary>
    public class ModuleWallpaperRow : ViewModelBase
    {
        public string Key { get; }
        public string Label { get; }

                private readonly AppConfig _config = null!;

public ModuleWallpaperRow(string key, string label, AppConfig? config)
        {
            _config = config!;
            Key = key;
            Label = label;
        }

        private ModuleWallpaperSettings Settings()
        {
            var appearance = _config!.Appearance;
            if (!appearance.ModuleWallpapers.TryGetValue(Key, out var s)
                || s == null)
            {
                s = new ModuleWallpaperSettings();
                appearance.ModuleWallpapers[Key] = s;
            }
            return s;
        }

        public bool Enabled
        {
            get => Settings().Enabled;
            set
            {
                Settings().Enabled = value;
                Save();
                Refresh();
                OnPropertyChanged();
            }
        }

        public string FileName
        {
            get
            {
                var path = Settings().Path;
                return string.IsNullOrEmpty(path)
                    ? "No image"
                    : System.IO.Path.GetFileName(path);
            }
        }

        public double OpacityValue
        {
            get => Settings().Opacity;
            set
            {
                Settings().Opacity = value;
                Save();
                Refresh();
                OnPropertyChanged();
            }
        }

        internal void SetPath(string path)
        {
            var s = Settings();
            s.Path = path;
            s.Enabled = true;
            Save();
            Refresh();
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(FileName));
            OnPropertyChanged(nameof(OpacityValue));
        }

        internal void ClearPath()
        {
            var s = Settings();
            s.Path = "";
            s.Enabled = false;
            Save();
            Refresh();
            OnPropertyChanged(nameof(Enabled));
            OnPropertyChanged(nameof(FileName));
        }

        private void Save()
        {
            if (_config != null)
                ConfigService.SaveDebounced(_config);
        }

        /// <summary>
        /// Re-layer the wallpaper of whichever module is on screen
        /// right now, so changes are visible immediately.
        /// </summary>
        private void Refresh() =>
            VisualThemeEngine.ApplyModule(
                VisualThemeEngine.LastRequestedModule,
                _config?.Appearance);
    }
}
