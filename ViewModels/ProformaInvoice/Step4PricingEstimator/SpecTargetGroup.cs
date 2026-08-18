// ═══════════════════════════════════════════════════════════════════════
// FILE: ProformaInvoice/Step4PricingEstimator/SpecTargetGroup.cs
// PROJECT: ProGlassAutomation
//
// SpecTargetGroup extracted from PricingEstimatorViewModel.cs (Phase 11/12
// God-VM split) — behaviour unchanged.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.ProformaInvoice.SpecificationsRepository;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.ProformaInvoice.Step4PricingEstimator
{
    public class SpecTargetGroup : ViewModelBase
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }
}
