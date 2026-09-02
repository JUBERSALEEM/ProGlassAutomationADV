// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Settings/SettingsViewModel.Features.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.1 — SettingsViewModel partial split. This part owns the Features
// members; the class declaration, fields and constructor live in
// SettingsViewModel.cs.
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
        /// Nested VM for the Settings ‣ Features ‣ Proforma Invoice
        /// page. Owns the pill toggles that show / hide individual
        /// fields in the Proforma Invoice wizard's Steps 1-3.
        /// Exposed as a public property so the XAML section can
        /// bind directly to <c>ProformaFeatures.ShowXxx</c>.
        /// </summary>
        public ProformaInvoiceFeaturesViewModel ProformaFeatures { get; }
            = new ProformaInvoiceFeaturesViewModel();

        /// <summary>
        /// Per-field visibility toggles for the Inquiry editor.
        /// Same design as <see cref="ProformaFeatures"/>: a non-coder
        /// operator can hide fields they never fill in without
        /// touching XAML.
        /// </summary>
        public InquiryFeaturesViewModel InquiryFeatures { get; }
            = new InquiryFeaturesViewModel();

        /// <summary>
        /// Per-field visibility toggles for the Job Order production
        /// panel. Same opt-out design as <see cref="ProformaFeatures"/>.
        /// </summary>
        public JobOrderFeaturesViewModel JobOrderFeatures { get; }
            = new JobOrderFeaturesViewModel();

        /// <summary>
        /// Per-field visibility toggles for the Delivery Note detail.
        /// Same opt-out design as <see cref="ProformaFeatures"/>.
        /// </summary>
        public DeliveryNoteFeaturesViewModel DeliveryNoteFeatures { get; }
            = new DeliveryNoteFeaturesViewModel();

        /// <summary>
        /// Per-field visibility toggles for the Tax Invoice detail.
        /// Same opt-out design as <see cref="ProformaFeatures"/>.
        /// </summary>
        public TaxInvoiceFeaturesViewModel TaxInvoiceFeatures { get; }
            = new TaxInvoiceFeaturesViewModel();

        // ══════════════════════════════════════════════════════════════
        //  FEATURES — per-module tab switch (Aug 2026)
        //
        //  The Features page used to stack all five modules' toggles in one
        //  long scroll. The user asked for a "module switch" so each module
        //  is a separate tab — only the selected module's field toggles are
        //  shown at once. This mirrors the Settings shell navigation pattern:
        //  a string + a command + one IsX flag per module.
        // ══════════════════════════════════════════════════════════════

        private string _featuresModule = "Proforma";
        /// <summary>Which module's feature toggles are visible on Features.</summary>
        public string FeaturesModule
        {
            get => _featuresModule;
            private set
            {
                if (_featuresModule == value) return;
                _featuresModule = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsProformaFeaturesModule));
                OnPropertyChanged(nameof(IsInquiryFeaturesModule));
                OnPropertyChanged(nameof(IsJobOrderFeaturesModule));
                OnPropertyChanged(nameof(IsDeliveryNoteFeaturesModule));
                OnPropertyChanged(nameof(IsTaxInvoiceFeaturesModule));
            }
        }

        public bool IsProformaFeaturesModule    => FeaturesModule == "Proforma";
        public bool IsInquiryFeaturesModule     => FeaturesModule == "Inquiry";
        public bool IsJobOrderFeaturesModule    => FeaturesModule == "JobOrder";
        public bool IsDeliveryNoteFeaturesModule => FeaturesModule == "DeliveryNote";
        public bool IsTaxInvoiceFeaturesModule  => FeaturesModule == "TaxInvoice";

        /// <summary>Switches which module's field-toggles show on Features.</summary>
        public ICommand SwitchFeaturesModuleCommand { get; private set; } = null!;

        /// <summary>
        /// Sign the current operator out and clear the "Skip login
        /// until update" DPAPI blob. Exposed here so the Settings ►
        /// Features page can host the affordance without depending on
        /// the main shell's DataContext.
        /// See <see cref="Services.Security.AuthSessionService"/>.
        /// </summary>
        public ICommand SignOutCommand { get; } = new Commands.RelayCommand(_ =>
            Utilities.AsyncUtility.SafeFireAndForget(
                Services.Security.AuthSessionService.SignOutAsync,
                "Settings SignOut"));

        /// <summary>
        /// Revoke the auto-sign-in credentials on this device WITHOUT
        /// ending the current session. Bound to the "Forget this
        /// device" affordance in Settings.
        /// </summary>
        public ICommand ForgetThisDeviceCommand { get; } = new Commands.RelayCommand(_ =>
            Services.Security.AuthSessionService.ClearSavedCredentials());
    }
}
