// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Appearance.Surfaces.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Settings v3 redesign (2026-09-17): split out of SettingsViewModel.Appearance.cs so no file in the
// module exceeds the ~350-line budget. Same partial class, members moved
// verbatim at member boundaries — behaviour unchanged.
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

        /// <summary>
        /// Global card corner radius (DIPs). Default 12. One slider
        /// restyles every card through CardCornerRadius (and the derived
        /// Control / Large tokens). Persisted across restarts.
        /// </summary>
        public List<string> DisplayDensityOptions { get; } = new()
        {
            "Compact", "Normal", "Comfortable", "Spacious"
        };

        public List<string> PdfLayoutOptions { get; } = new()
        {
            "Classic Navy", "Modern Teal", "Editorial Ink",
            "Industrial Steel", "Minimal Ivory"
        };

        public double CardBlurIntensity
        {
            get => _config?.Appearance?.CardBlurIntensity ?? 28;
            set => SetSurfaceDouble(value, 1, 100,
                v => _config!.Appearance.CardBlurIntensity = v,
                () => _config!.Appearance.CardBlurIntensity,
                nameof(CardBlurIntensity));
        }

        public string CardFrostTint
        {
            get => _config?.Appearance?.CardFrostTint ?? "";
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.CardFrostTint == value) return;
                _config.Appearance.CardFrostTint = value ?? "";
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                ScheduleDebouncedSave();
                OnPropertyChanged();
                OnPropertyChanged(nameof(CardFrostTintSwatch));
            }
        }

        public Brush CardFrostTintSwatch
        {
            get
            {
                var parsed = ProGlassAutomation.Services.Theme.AccentPresets
                    .TryParseHex(_config?.Appearance?.CardFrostTint);
                if (parsed is Color c)
                {
                    var brush = new SolidColorBrush(c);
                    brush.Freeze();
                    return brush;
                }
                return Application.Current?.TryFindResource("Surface.Primary.BackgroundBrush") as Brush
                    ?? Brushes.White;
            }
        }

        /// <summary>
        /// 0 washes the plate to white. 100 paints the swatch unchanged.
        /// </summary>
        public double CardFrostStrength
        {
            get => _config?.Appearance?.CardFrostStrength ?? 100;
            set => SetSurfaceDouble(value, 0, 100,
                v => _config!.Appearance.CardFrostStrength = v,
                () => _config!.Appearance.CardFrostStrength,
                nameof(CardFrostStrength));
        }

        /// <summary>
        /// 0 is clear. 100 is opaque, so the card is not composited
        /// toward the page and a black pick stays black.
        /// </summary>
        public double CardFrostOpacity
        {
            get => _config?.Appearance?.CardFrostOpacity ?? 100;
            set => SetSurfaceDouble(value, 0, 100,
                v => _config!.Appearance.CardFrostOpacity = v,
                () => _config!.Appearance.CardFrostOpacity,
                nameof(CardFrostOpacity));
        }

        public ObservableCollection<AccentOption> CardFrostPalette { get; } = new();

        public void InitializeCardFrostPalette()
        {
            CardFrostPalette.Clear();
            CardFrostPalette.Add(new AccentOption(
                "White", "White", Color.FromRgb(255, 255, 255)));
            foreach (var (key, name) in
                ProGlassAutomation.Services.Theme.AccentPresets.All)
            {
                // The accent "Black" chip is ink #111827 so a button stays
                // visible. On a card plate, Black means black.
                var swatch = key == ProGlassAutomation.Services.Theme.AccentPresets.Black
                    ? Color.FromRgb(0, 0, 0)
                    : ProGlassAutomation.Services.Theme.AccentPresets.SwatchFor(key);
                CardFrostPalette.Add(new AccentOption(key, name, swatch));
            }
        }

        public ICommand ApplyCardFrostTintCommand { get; private set; }

        private void ApplyCardFrostTintPreset(string key)
        {
            if (_config?.Appearance == null) return;
            if (string.IsNullOrWhiteSpace(key) || key == "Auto")
            {
                CardFrostTint = "";
                return;
            }
            if (key == "White")
            {
                CardFrostTint = "#FFFFFF";
                return;
            }
            if (key == ProGlassAutomation.Services.Theme.AccentPresets.Black)
            {
                CardFrostTint = "#000000";
                return;
            }
            var color = ProGlassAutomation.Services.Theme.AccentPresets.ColorFor(key);
            if (color == null) return;
            CardFrostTint = $"#{color.Value.R:X2}{color.Value.G:X2}{color.Value.B:X2}";
        }

        public List<string> SurfaceMaterialOptions { get; } = new()
        {
            "Glass", "Solid", "Transparent"
        };

        public string SurfaceMaterialMode
        {
            get => _config?.Appearance?.SurfaceMaterialMode ?? "Glass";
            set
            {
                if (_config?.Appearance == null) return;
                string v = string.IsNullOrWhiteSpace(value) ? "Glass" : value.Trim();
                var material = ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine.ParseMaterial(v);
                v = ProGlassAutomation.Services.Theme.Engine.ProGlassThemeEngine.PersistMaterial(material);
                if (_config.Appearance.SurfaceMaterialMode == v) return;
                _config.Appearance.SurfaceMaterialMode = v;
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                ScheduleDebouncedSave();
                OnPropertyChanged();
            }
        }

        public bool UseRoundedSurfaces
        {
            get => _config?.Appearance?.UseRoundedSurfaces ?? true;
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.UseRoundedSurfaces == value) return;
                _config.Appearance.UseRoundedSurfaces = value;
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                ScheduleDebouncedSave();
                OnPropertyChanged();
            }
        }

        public bool UsePrimarySurfaceElevation
        {
            get => _config?.Appearance?.UsePrimarySurfaceElevation ?? false;
            set
            {
                if (_config?.Appearance == null) return;
                if (_config.Appearance.UsePrimarySurfaceElevation == value) return;
                _config.Appearance.UsePrimarySurfaceElevation = value;
                ThemeManager.ApplySemanticSurfaces(_config.Appearance);
                ScheduleDebouncedSave();
                OnPropertyChanged();
            }
        }

        public double SurfacePrimaryOpacity
        {
            get => _config?.Appearance?.SurfacePrimaryOpacity ?? 0.82;
            set => SetSurfaceDouble(value, 0.30, 1.0,
                v => _config!.Appearance.SurfacePrimaryOpacity = v,
                () => _config!.Appearance.SurfacePrimaryOpacity,
                nameof(SurfacePrimaryOpacity));
        }

        public double SurfaceSecondaryOpacity
        {
            get => _config?.Appearance?.SurfaceSecondaryOpacity ?? 0.94;
            set => SetSurfaceDouble(value, 0.25, 1.0,
                v => _config!.Appearance.SurfaceSecondaryOpacity = v,
                () => _config!.Appearance.SurfaceSecondaryOpacity,
                nameof(SurfaceSecondaryOpacity));
        }

        public double SurfaceDetailOpacity
        {
            get => _config?.Appearance?.SurfaceDetailOpacity ?? 0.86;
            set => SetSurfaceDouble(value, 0.15, 1.0,
                v => _config!.Appearance.SurfaceDetailOpacity = v,
                () => _config!.Appearance.SurfaceDetailOpacity,
                nameof(SurfaceDetailOpacity));
        }

        public double PrimarySurfaceOverlayOpacity
        {
            get => _config?.Appearance?.PrimarySurfaceOverlayOpacity ?? 0.0;
            set => SetSurfaceDouble(value, 0.0, 0.35,
                v => _config!.Appearance.PrimarySurfaceOverlayOpacity = v,
                () => _config!.Appearance.PrimarySurfaceOverlayOpacity,
                nameof(PrimarySurfaceOverlayOpacity));
        }

        public double SurfacePrimaryRadius
        {
            get => _config?.Appearance?.SurfacePrimaryRadius ?? CardCornerRadius;
            set => SetSurfaceNullableDouble(value, 0, 48,
                v => _config!.Appearance.SurfacePrimaryRadius = v,
                () => _config!.Appearance.SurfacePrimaryRadius ?? CardCornerRadius,
                nameof(SurfacePrimaryRadius));
        }

        public double SurfaceSecondaryRadius
        {
            get => _config?.Appearance?.SurfaceSecondaryRadius ?? CardCornerRadius;
            set => SetSurfaceNullableDouble(value, 0, 48,
                v => _config!.Appearance.SurfaceSecondaryRadius = v,
                () => _config!.Appearance.SurfaceSecondaryRadius ?? CardCornerRadius,
                nameof(SurfaceSecondaryRadius));
        }

        public double SurfaceDetailRadius
        {
            get => _config?.Appearance?.SurfaceDetailRadius ?? CardCornerRadius;
            set => SetSurfaceNullableDouble(value, 0, 48,
                v => _config!.Appearance.SurfaceDetailRadius = v,
                () => _config!.Appearance.SurfaceDetailRadius ?? CardCornerRadius,
                nameof(SurfaceDetailRadius));
        }

        public double SurfacePrimaryBorderThickness
        {
            get => _config?.Appearance?.SurfacePrimaryBorderThickness ?? CardEdgeThickness;
            set => SetSurfaceNullableDouble(value, 0, 24,
                v => _config!.Appearance.SurfacePrimaryBorderThickness = v,
                () => _config!.Appearance.SurfacePrimaryBorderThickness ?? CardEdgeThickness,
                nameof(SurfacePrimaryBorderThickness));
        }

        public double SurfaceSecondaryBorderThickness
        {
            get => _config?.Appearance?.SurfaceSecondaryBorderThickness ?? CardBorderThickness;
            set => SetSurfaceNullableDouble(value, 0, 24,
                v => _config!.Appearance.SurfaceSecondaryBorderThickness = v,
                () => _config!.Appearance.SurfaceSecondaryBorderThickness ?? CardBorderThickness,
                nameof(SurfaceSecondaryBorderThickness));
        }

        public double SurfaceDetailBorderThickness
        {
            get => _config?.Appearance?.SurfaceDetailBorderThickness ?? 1.0;
            set => SetSurfaceNullableDouble(value, 0, 24,
                v => _config!.Appearance.SurfaceDetailBorderThickness = v,
                () => _config!.Appearance.SurfaceDetailBorderThickness ?? 1.0,
                nameof(SurfaceDetailBorderThickness));
        }

        private void SetSurfaceDouble(
            double value, double min, double max,
            Action<double> setter, Func<double> getter, string propertyName)
        {
            if (_config?.Appearance == null) return;
            double v = value < min ? min : (value > max ? max : value);
            if (Math.Abs(getter() - v) < 0.001) return;
            setter(v);
            ThemeManager.ApplySemanticSurfaces(_config.Appearance);
            ScheduleDebouncedSave();
            OnPropertyChanged(propertyName);
        }

        private void SetSurfaceNullableDouble(
            double value, double min, double max,
            Action<double?> setter, Func<double> getter, string propertyName)
        {
            if (_config?.Appearance == null) return;
            double v = value < min ? min : (value > max ? max : value);
            if (Math.Abs(getter() - v) < 0.001) return;
            setter(v);
            ThemeManager.ApplySemanticSurfaces(_config.Appearance);
            ScheduleDebouncedSave();
            OnPropertyChanged(propertyName);
        }


        public string PdfLayout
        {
            get
            {
                var id = _config?.Appearance?.PdfLayoutId ?? "ClassicNavy";
                return ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                    .Resolve(id).DisplayName;
            }
            set
            {
                if (_config?.Appearance == null) return;
                var theme = ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                    .Resolve(value);
                if (_config.Appearance.PdfLayoutId == theme.Id) return;

                _config.Appearance.PdfLayoutId = theme.Id;
                ProGlassAutomation.Services.Export.Pdf.DocumentStyle.Apply(theme);
                Services.AI.AIConfig.ConfigService.SaveDebounced(_config);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "PDF layout changed", theme.DisplayName);

                OnPropertyChanged();
                OnPropertyChanged(nameof(PdfLayoutHint));
            }
        }

        public string PdfLayoutHint =>
            ProGlassAutomation.Services.Export.Pdf.PdfThemeCatalog
                .Resolve(_config?.Appearance?.PdfLayoutId).Description;

        /// <summary>
        /// Compact / Normal / Comfortable. One setting shrinks shared
        /// chrome so laptop screens can see the data grid.
        /// </summary>
    }
}
