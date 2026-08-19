// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Login/LoginWindow.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Authentication
//
// Standalone host for LoginPanel. Startup no longer uses this window —
// sign-in lives on the splash so the two screens cannot desync.
// ═══════════════════════════════════════════════════════════════════════

using System.Windows;

namespace ProGlassAutomation.Views.Login
{
    public partial class LoginWindow : Window
    {
        public bool SignedIn { get; private set; }

        public LoginWindow()
        {
            InitializeComponent();
            Panel.Completed += OnPanelCompleted;
        }

        private void OnPanelCompleted(bool signedIn)
        {
            SignedIn = signedIn;
            Close();
        }
    }
}
