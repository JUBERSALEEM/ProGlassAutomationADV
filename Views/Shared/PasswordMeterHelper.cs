// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Shared/PasswordMeterHelper.cs
// PROJECT: ProGlassAutomation
// MODULE: Authentication — password strength meter (view helper)
//
// WHY A HELPER
// ──────────────
// WPF cannot bind PasswordBox.Password, so every password screen drives
// its meter from code-behind PasswordChanged handlers. Without this
// helper the score→colour→label mapping would be copy-pasted into four
// code-behinds (UserCenter, first-run setup, reset, forced rotation) and
// the four would drift. This keeps ONE mapping; each screen only passes
// its named elements plus the identity fields the policy checks against.
//
// CONTRACT WITH THE XAML
// ──────────────
//   segmentsHost — a Panel (usually a horizontal StackPanel) holding the
//                  meter segments as Borders. The first four are lit by
//                  score; extras are ignored, fewer still work.
//   strengthLabel — shows "Weak" / "Fair" / "Good" / "Strong" / "Too short".
//   guidanceText  — one line under the meter: the rejection reason while
//                   the password is rejected, the improvement hint while
//                   accepted-but-weak, hidden when strong or empty.
// Never throws: a meter must never break a password screen.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProGlassAutomation.Views.Shared
{
    public static class PasswordMeterHelper
    {
        public static void Update(
            Panel segmentsHost,
            TextBlock strengthLabel,
            TextBlock? guidanceText,
            string password,
            string? username = null,
            string? displayName = null)
        {
            try
            {
                if (segmentsHost == null || strengthLabel == null)
                    return;

                var eval = PasswordPolicy.Evaluate(password, username, displayName);
                bool empty = string.IsNullOrEmpty(password);

                Brush active = BrushForScore(eval.Score);
                Brush idle = TryFindBrush("OutlineBrush") ?? Brushes.Gray;
                Brush mutedFg = TryFindBrush("MutedForegroundBrush") ?? Brushes.Gray;

                int index = 0;
                foreach (var segment in segmentsHost.Children.OfType<Border>())
                {
                    segment.Background = !empty && index < eval.Score
                        ? active
                        : idle;
                    index++;
                }

                strengthLabel.Text = empty ? "" : eval.StrengthLabel;
                strengthLabel.Foreground = empty ? mutedFg : active;

                if (guidanceText != null)
                {
                    string message = empty
                        ? ""
                        : eval.Accepted ? eval.Guidance : eval.RejectionReason;
                    guidanceText.Text = message;
                    guidanceText.Visibility = string.IsNullOrEmpty(message)
                        ? Visibility.Collapsed
                        : Visibility.Visible;
                }
            }
            catch (Exception meterEx)
            {
                // A meter failure must never surface on a password
                // screen — but it is still worth a debug line.
                AppLogService.Debug(
                    $"[PasswordMeter] update failed: {meterEx.Message}");
            }
        }

        private static Brush BrushForScore(int score)
        {
            // 0–1 danger, 2 warning, 3 info, 4 success — resolved from the
            // active theme so the meter follows light/dark like the rest.
            string key = score switch
            {
                <= 1 => "DangerBrush",
                2 => "WarningBrush",
                3 => "InfoBrush",
                _ => "SuccessBrush"
            };
            return TryFindBrush(key) ?? Brushes.Gray;
        }

        private static Brush? TryFindBrush(string key)
        {
            try
            {
                return Application.Current?.TryFindResource(key) as Brush;
            }
            catch
            {
                return null;
            }
        }
    }
}
