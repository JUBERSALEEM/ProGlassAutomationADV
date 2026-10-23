// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Appearance.V3Themes.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Settings Appearance — Theme Engine combo on the V3 catalog (owner ruling).
// The combo lists ThemeCatalog.Sequence: Bento Grid installed and
// selectable, the other eight identities Planned and disabled. No
// Monet/Crystal/Aurora/Neo rows — the legacy VisualThemeOptions list stays
// untouched for the Themes page, it is simply not bound on Appearance.
//
// Selecting the installed row runs the ONE publisher
// (V3ThemeApplier.ApplyCurrent) with the current Light/Dark — there is no
// legacy family swap and no accent paint on this path.
// ═══════════════════════════════════════════════════════════════════════
using ProGlassAutomation.Theming;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.ViewModels.Settings
{
    /// <summary>One row of the Theme Engine combo (V3 catalog identity).</summary>
    public sealed class V3ThemeOptionRow
    {
        /// <summary>Catalog identity name (ThemeCatalog.Sequence).</summary>
        public string Name { get; init; } = "";

        /// <summary>True only for Installed packages — drives ComboBoxItem.IsEnabled.</summary>
        public bool IsInstalled { get; init; }

        /// <summary>Style-immune label (same contract as VisualThemeOptionRow).</summary>
        public string Display =>
            IsInstalled ? Name + "  —  Installed" : Name + "  —  Planned";

        public override string ToString() => Display;
    }

    public sealed partial class SettingsViewModel : ViewModelBase, IDisposable
    {
        /// <summary>
        /// The V3 catalog rows for the Theme Engine combo. Built from
        /// ThemeCatalog.Sequence so a package cannot be registered in the
        /// catalog and then forgotten on the Appearance page — the exact
        /// failure mode the old hand-listed picker had.
        /// </summary>
        public List<V3ThemeOptionRow> V3ThemeOptions { get; } =
            ThemeCatalog.Sequence
                .Select(identity => new V3ThemeOptionRow
                {
                    Name = identity.Name,
                    IsInstalled = identity.Status == ThemeImplementationStatus.Installed
                })
                .ToList();

        /// <summary>
        /// The active V3 package name (ThemeService.ActiveTheme, Bento in
        /// Phase 1). Two-way bound by the Theme Engine combo; Planned rows
        /// are disabled in the container style AND guarded here so a stale
        /// binding can never activate a package that is not installed.
        /// </summary>
        public string SelectedV3Theme
        {
            get => V3ThemeApplier.Instance?.ActiveTheme ?? ThemeCatalog.BentoName;
            set
            {
                if (string.IsNullOrEmpty(value)) return;

                var row = V3ThemeOptions.FirstOrDefault(r => r.Name == value);
                if (row is null || !row.IsInstalled) return;

                // Selecting the already-active package is a no-op — same
                // contract as the Light/Dark segmented control: a re-select
                // while the binding settles must not repaint.
                if (value == SelectedV3Theme)
                {
                    OnPropertyChanged();
                    return;
                }

                // Phase 4 hook: installed-but-not-active packages activate
                // here through the single publisher. Today only Bento is
                // installed, so the equality check above absorbs every UI
                // interaction; ApplyCurrent stays for forward-compatibility.
                V3ThemeApplier.ApplyCurrent();

                OnPropertyChanged();
                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Settings,
                    "Theme (V3 catalog)", value);
            }
        }
    }
}
