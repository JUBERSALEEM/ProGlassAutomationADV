// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Dialogs/IDialogService.cs
// PROJECT: ProGlassAutomation
// MODULE: MVVM leakage sweep (Phase 2, audit item 2.1)
//
// Single interception point for user-facing dialogs. ViewModels must NOT
// call MessageBox.* directly — they depend on this service (currently
// resolved as App.Dialogs, the provider-backed static migration pattern).
//
// The current implementation delegates to MessageBox.Show so behaviour is
// byte-identical; later it can be swapped for a themed/owner-aware dialog
// host, or faked in unit tests, without touching 200+ call sites.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;

namespace ProGlassAutomation.Services.Dialogs
{
    /// <summary>Dialog abstraction for ViewModels (MVVM boundary).</summary>
    public interface IDialogService
    {
        /// <summary>Generic show; same signature as MessageBox.Show.</summary>
        MessageBoxResult Show(string message, string? caption = null,
            MessageBoxButton button = MessageBoxButton.OK,
            MessageBoxImage icon = MessageBoxImage.None);

        /// <summary>Information message (OK).</summary>
        void Info(string message, string? caption = null);

        /// <summary>Warning message (OK).</summary>
        void Warning(string message, string? caption = null);

        /// <summary>Error message (OK).</summary>
        void Error(string message, string? caption = null);

        /// <summary>Yes/No confirmation. Returns true when the user picked
        /// Yes.</summary>
        bool Confirm(string message, string? caption = null);
    }
}
