// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/About/Sections/AboutCrashReportsCard.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: About — recent crash reports card code-behind
//
// The card owns its own AboutCrashReportsViewModel, attached to the card's
// INNER container (x:Name="CardRoot") — never to the UserControl ROOT: a
// root assignment re-targets every binding the host declared on this
// element (the About → License blank-page fault, 2026-09-15).
//
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

            // Inner container only — see the header note.
            CardRoot.DataContext = new AboutCrashReportsViewModel();
        }

        }
}
