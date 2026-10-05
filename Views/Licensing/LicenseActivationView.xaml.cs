// ═══════════════════════════════════════════════════════════════════════
// FILE: Views/Licensing/LicenseActivationView.xaml.cs
// PROJECT: ProGlassAutomation
// MODULE: Licensing page — key-paste activation (2026 simple flow)
//
// ONE ACTION: the user pastes the PGKEY1 license key their vendor sent
// and clicks Apply. LicenseService.InstallKeyText verifies the vendor
// signature against the embedded trusted-key ring BEFORE storing
// anything; a key that fails verification changes nothing and the
// automatic trial keeps the app running.
//
// CODE-ONLY CONTROL (2026-08-29):
//   This view was converted from XAML + code-behind to a fully
//   code-constructed UserControl. The XAML-generated partial class
//   (InitializeComponent + the x:Name fields) proved to be a recurring
//   build breakage point: any local XAML/csproj divergence silently
//   removed the generated members and produced a wall of CS0103
//   errors (InitializeComponent, HeadlineText, SubheadText, ...).
//   A code-only control has NO generated partial, so that entire
//   failure class is structurally impossible — the same pattern as
//   LicenseActivatedDialog. The public surface (IsClosable,
//   Activated/Closed events) is unchanged for MainWindow and the
//   Settings license section.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Logging;
using ThemeManager = ProGlassAutomation.Services.ThemeManager;
// NOTE: ProGlass.Licensing is deliberately NOT imported here — the
// contract defines a LicenseService type that would collide with the
// app's own LicenseService (CS0104). Contract types used below are
// written fully qualified (ProGlass.Licensing.*).

namespace ProGlassAutomation.Views.Licensing
{
    /// <summary>
    /// In-app license page: paste the key → instant activation.
    /// Code-only control — see the file header for why.
    ///
    /// Declared PARTIAL on purpose: if a stale copy of the deleted
    /// LicenseActivationView.xaml still lingers on a machine (a sync
    /// that never deletes files), the WPF compiler generates a second
    /// partial declaration from it. Partial merging keeps that
    /// scenario COMPILABLE — and this code never calls
    /// InitializeComponent or any x:Name field, so the stale
    /// generated members are simply unused. With or without the
    /// stale XAML, the build succeeds.
    /// </summary>
    public partial class LicenseActivationView : UserControl
    {
    }
}
