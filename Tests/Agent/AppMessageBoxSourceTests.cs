using System;
using System.IO;
using Xunit;
using System.Linq;

namespace ProGlassAutomation.Tests.Agent
{
    // ═══════════════════════════════════════════════════════════════════
    // FILE: Tests/Agent/AppMessageBoxSourceTests.cs
    // FIX-2026-09-09 pins — one professional dialog shell, app-wide.
    //
    // Surface defect class (owner report): every "quick message" was a
    // Windows OS MessageBox — small, chrome-bound, theme-deaf. The fix is
    // a shell + a service route: Views/Shared/AppMessageBox owned by
    // DialogService, with the native box kept ONLY as the crash fallback.
    //
    // These pins keep the fix structural and permanent:
    //   1. The shell exists, owns the severity-chips + action styles.
    //   2. DialogService routes through AppMessageBox.ShowBox and can call
    //      the native MessageBox.Show EXACTLY ONCE (the fallback branch).
    //   3. Shell invariants: no hardcoded background literal in the view
    //      (theme binds only), result mapping for every button variant,
    //      CenterOwner+transparent+NoTaskbar chrome flags.
    //   4. Localized button labels (Action.*/Common.* keys), no invented
    //      English literals on actions.
    // ═══════════════════════════════════════════════════════════════════
    public sealed class AppMessageBoxSourceTests
    {
        [Fact]
        public void Shell_exists_with_the_severity_chip_and_action_styles()
        {
            string view = File.ReadAllText(Path.Combine(
                LocateProjectRoot(), "Views/Shared/AppMessageBox.xaml"));

            // One tinted chip per severity — surfaced as a bound surface,
            // never a literal color in the view file.
            Assert.Contains("IconTint, RelativeSource={RelativeSource AncestorType=Window}", view);
            Assert.Contains("IconGlyph, RelativeSource={RelativeSource AncestorType=Window}", view);

            // One accent + one ghost action family.
            Assert.Contains("x:Key=\"PrimaryActionButton\"", view);
            Assert.Contains("x:Key=\"GhostActionButton\"", view);
        }

        // ── ERR-36432A ────────────────────────────────────────────────
        //
        // The shell shipped with NO constructor. A partial WPF Window
        // compiles perfectly without one — the implicit default ctor is
        // generated — but that generated ctor does NOT call
        // InitializeComponent(). So the visual tree was never loaded and
        // every x:Name field (OkButton, YesButton, NoButton,
        // CancelButton) stayed null. ConfigureButtons() then threw a bare
        // NullReferenceException on its first line.
        //
        // The blast radius was the whole app: AppMessageBox is how every
        // error is reported, so ANY code path that tried to show a
        // message crashed instead of explaining itself. Real bugs hid
        // behind it — the user hit it by clicking USE on a zero-balance
        // sheet, where the app was correctly trying to say "No stock
        // available to use."
        //
        // Nothing warns about this: no compiler error, no analyzer, and
        // the class is only reachable through the static ShowBox()
        // factory, so the omission looked deliberate.
        [Fact]
        public void Shell_calls_InitializeComponent_so_named_elements_resolve()
        {
            string code = File.ReadAllText(Path.Combine(
                LocateProjectRoot(), "Views/Shared/AppMessageBox.xaml.cs"));

            Assert.Contains("public AppMessageBox()", code);
            Assert.Contains("InitializeComponent();", code);

            // The named buttons the code-behind drives must exist in the
            // view, or InitializeComponent cannot populate them.
            string view = File.ReadAllText(Path.Combine(
                LocateProjectRoot(), "Views/Shared/AppMessageBox.xaml"));

            foreach (var name in new[] { "OkButton", "YesButton", "NoButton", "CancelButton" })
            {
                Assert.Contains($"x:Name=\"{name}\"", view);
                Assert.Contains(name, code);
            }
        }

        // Repo-wide guard: the same omission anywhere else is the same
        // silent, total failure of that window.
        [Fact]
        public void Every_xaml_code_behind_calls_InitializeComponent()
        {
            string root = LocateProjectRoot();
            var offenders = new System.Collections.Generic.List<string>();

            foreach (var xaml in Directory.GetFiles(
                         Path.Combine(root, "Views"), "*.xaml", SearchOption.AllDirectories))
            {
                string codeBehind = xaml + ".cs";
                if (!File.Exists(codeBehind)) continue;

                // Only files that declare a partial class need the call.
                if (!File.ReadAllText(xaml).Contains("x:Class")) continue;

                if (!File.ReadAllText(codeBehind).Contains("InitializeComponent"))
                    offenders.Add(Path.GetFileName(codeBehind));
            }

            Assert.True(offenders.Count == 0,
                "These code-behind files never call InitializeComponent(), so their " +
                "x:Name fields stay null and the window loads empty: " +
                string.Join(", ", offenders));
        }

        [Fact]
        public void Service_routes_through_the_shell_with_one_native_fallback()
        {
            string svc = File.ReadAllText(Path.Combine(
                LocateProjectRoot(), "Services/Dialogs/DialogService.cs"));

            Assert.Contains("AppMessageBox.ShowBox(", svc);
            Assert.Contains("Owner = System.Windows.Application.Current?.MainWindow", svc);

            // Native box may survive only once: the crash-path fallback.
            int nativeCalls = svc.Split(
                new[] { "MessageBox.Show(" }, StringSplitOptions.None).Length - 1;
            Assert.True(nativeCalls == 1,
                $"Native MessageBox must remain exactly one fallback call, found {nativeCalls}.");
        }

        [Fact]
        public void Shell_keeps_MessageBox_semantics_results_and_chrome_flags()
        {
            string cs = File.ReadAllText(Path.Combine(
                LocateProjectRoot(), "Views/Shared/AppMessageBox.xaml.cs"));
            string view = File.ReadAllText(Path.Combine(
                LocateProjectRoot(), "Views/Shared/AppMessageBox.xaml"));

            // Every button variant answers its result (contract).
            Assert.Contains("MessageBoxResult.OK;", cs);
            Assert.Contains("MessageBoxResult.Yes;", cs);
            Assert.Contains("MessageBoxResult.No;", cs);
            Assert.Contains("MessageBoxResult.Cancel;", cs);

            // ShowBox keeps the owner's-signature semantics.
            Assert.Contains("public static MessageBoxResult ShowBox(", cs);

            // Chrome flags — the OS-styled window must never re-appear.
            Assert.Contains("WindowStyle=\"None\"", view);
            Assert.Contains("AllowsTransparency=\"True\"", view);
            Assert.Contains("ShowInTaskbar=\"False\"", view);
            Assert.Contains("WindowStartupLocation=\"CenterOwner\"", view);
        }

        [Fact]
        public void Buttons_use_localized_labels_and_wrap_the_message()
        {
            string view = File.ReadAllText(Path.Combine(
                LocateProjectRoot(), "Views/Shared/AppMessageBox.xaml"));

            // Action labels: existing localization keys only (no invented
            // English literals that one day get translated by accident).
            Assert.Contains("{DynamicResource Action.OK}", view);
            Assert.Contains("{DynamicResource Action.Yes}", view);
            Assert.Contains("{DynamicResource Action.No}", view);
            Assert.Contains("{DynamicResource Action.Cancel}", view);

            // Body wraps (the old box let long errors fly off-screen).
            Assert.Contains("TextWrapping=\"Wrap\"", view);
        }

        // ── helpers ──────────────────────────────────────────────────

        private static string LocateProjectRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var probe = Path.Combine(dir.FullName,
                    "Views", "ProformaInvoice", "Step5DataGrid", "DataGridView.xaml");
                if (File.Exists(probe)) return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException(
                "Could not locate the project root above " + AppContext.BaseDirectory);
        }
    }
}
