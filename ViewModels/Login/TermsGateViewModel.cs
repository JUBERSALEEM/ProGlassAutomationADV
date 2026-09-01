// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/Login/TermsGateViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Splash — first-run terms-of-use gate.
//
// 2026-09-02 (Phase 3.7): the terms accept bar's handlers moved out of
// Views/Splash/SplashView.xaml.cs into this small VM (scoped as the
// DataContext of the terms Border in SplashView.xaml). The view keeps
// the side effects that touch window state (hiding the panel, Close) via
// the RequestContinue / RequestDecline callbacks.
//
// Defence in depth preserved: the Continue command re-checks the
// checkbox state instead of trusting the button's IsEnabled alone.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using System;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.Login
{
    /// <summary>First-run terms acceptance: checkbox → continue enable,
    /// Continue / Decline commands.</summary>
    public sealed class TermsGateViewModel : ViewModelBase
    {
        private bool _acceptTerms;

        /// <summary>TwoWay-bound to the accept checkbox.</summary>
        public bool AcceptTerms
        {
            get => _acceptTerms;
            set
            {
                if (value == _acceptTerms) return;
                _acceptTerms = value;
                OnPropertyChanged();
                IsContinueEnabled = value;
            }
        }

        private bool _isContinueEnabled;
        public bool IsContinueEnabled
        {
            get => _isContinueEnabled;
            private set { _isContinueEnabled = value; OnPropertyChanged(); }
        }

        /// <summary>True after the user explicitly continued.</summary>
        public bool TermsAccepted { get; private set; }

        /// <summary>View wiring: RecordAcceptance + hide the terms panel +
        /// fire TermsResolved (SplashView side effects).</summary>
        public Action? RequestContinue { get; set; }

        /// <summary>View wiring: fire TermsResolved + Close the window.</summary>
        public Action? RequestDecline { get; set; }

        public ICommand ContinueCommand { get; }
        public ICommand DeclineCommand { get; }

        public TermsGateViewModel()
        {
            ContinueCommand = new RelayCommand(_ => Continue());
            DeclineCommand = new RelayCommand(_ => Decline());
        }

        private void Continue()
        {
            // Defence in depth: the button is disabled until the box is
            // ticked, but never rely on UI state alone for a consent gate.
            if (!AcceptTerms) return;

            TermsAccepted = true;
            RequestContinue?.Invoke();
        }

        private void Decline()
        {
            TermsAccepted = false;
            RequestDecline?.Invoke();
        }
    }
}
