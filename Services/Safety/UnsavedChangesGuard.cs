// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Safety/UnsavedChangesGuard.cs
// PROJECT: ProGlassAutomation
// MODULE: Core — unsaved-changes safety (§28)
//
// One shared guard for every screen that tracks dirty state. The user
// is never silently stripped of entered data: leaving a dirty screen
// always offers Cancel / Discard / Save & Continue.
//
// Usage in a shell ViewModel:
//   if (!await UnsavedChangesGuard.ConfirmLeaveAsync(
//           () => DetailVM.HasUnsavedChanges,
//           () => DetailVM.TrySaveAsync(),
//           "job order"))
//       return;   // user chose Cancel — stay on the screen
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Views.Shared;
using System;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Safety
{
    /// <summary>Shared unsaved-changes gate.</summary>
    public static class UnsavedChangesGuard
    {
        /// <summary>
        /// Returns true when it is safe to leave the screen:
        ///   • nothing dirty                → true immediately
        ///   • user chose Discard           → true
        ///   • user chose Save & Continue   → true after a successful save
        ///   • user chose Cancel / save failed → false (stay put)
        /// </summary>
        public static async Task<bool> ConfirmLeaveAsync(
            Func<bool> isDirty,
            Func<Task<bool>> saveAsync,
            string documentDescription)
        {
            if (isDirty == null || !isDirty())
                return true;

            var choice = UnsavedChangesDialog.Show(
                System.Windows.Application.Current?.MainWindow,
                documentDescription);

            switch (choice)
            {
                case UnsavedChangesChoice.Discard:
                    return true;

                case UnsavedChangesChoice.SaveAndContinue:
                    try
                    {
                        bool saved = saveAsync != null && await saveAsync();

                        if (!saved)
                        {
                            System.Windows.MessageBox.Show(
                                "The save did not complete — fix the issue " +
                                "or choose Discard.",
                                "Unsaved Changes",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Warning);
                        }

                        return saved;
                    }
                    catch
                    {
                        return false;
                    }

                default:
                    return false;   // Cancel — stay on the screen
            }
        }
    }
}
