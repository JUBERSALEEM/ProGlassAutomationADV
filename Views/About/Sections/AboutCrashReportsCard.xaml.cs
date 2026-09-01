// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/Sections/AboutCrashReportsCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About — recent crash reports card code-behind
//
// The card owns its own AboutCrashReportsViewModel.
// The Open button on each row reads the row's
// FilePath from its Tag and routes through
// ProcessStartGuard.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Security;
using ProGlassAutomation.ViewModels.About;

namespace ProGlassAutomation.Views.About.Sections
{
    public partial class AboutCrashReportsCard : UserControl
    {
        public AboutCrashReportsCard()
        {
            InitializeComponent();
            DataContext = new AboutCrashReportsViewModel();
        }

        }
}
