using System;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.ViewModels.Dashboard
{
    /// <summary>
    /// Base class for all module dashboard sub-pages.
    /// Provides: IsLoading, HasError, ErrorMessage, LastRefreshed,
    /// TabTitle, TabIcon, and a reentrancy-guarded LoadAsync().
    /// Sub-pages override LoadCoreAsync() with their own data logic.
    /// </summary>
    public abstract class DashboardPageBase : ViewModelBase
    {
        private int _loadingFlag = 0;

        // ── loading state ─────────────────────────────────────────────────
        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            protected set { if (_isLoading == value) return; _isLoading = value; OnPropertyChanged(); }
        }

        private bool _hasError;
        public bool HasError
        {
            get => _hasError;
            protected set { if (_hasError == value) return; _hasError = value; OnPropertyChanged(); }
        }

        private string _errorMessage = string.Empty;
        public string ErrorMessage
        {
            get => _errorMessage;
            protected set
            {
                if (_errorMessage == value) return;
                _errorMessage = value;
                OnPropertyChanged();
            }
        }

        private string _lastRefreshed = string.Empty;
        public string LastRefreshed
        {
            get => _lastRefreshed;
            protected set
            {
                if (_lastRefreshed == value) return;
                _lastRefreshed = value;
                OnPropertyChanged();
            }
        }

        // ── tab identity (implemented by each sub-page) ───────────────────
        public abstract string TabTitle { get; }
        public abstract string TabIcon { get; }

        // ── public load entry point ───────────────────────────────────────
        /// <summary>
        /// Called by DashboardViewModel when this tab becomes active,
        /// and by DashboardView.OnLoaded to refresh on navigation.
        /// Reentrancy guard prevents parallel loads.
        /// </summary>
        public async Task LoadAsync()
        {
            if (Interlocked.CompareExchange(ref _loadingFlag, 1, 0) != 0)
                return;

            try
            {
                IsLoading = true;
                HasError = false;
                ErrorMessage = string.Empty;

                await LoadCoreAsync();

                LastRefreshed = $"Updated {DateTime.Now:HH:mm:ss}";
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Failed to load data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                Interlocked.Exchange(ref _loadingFlag, 0);
            }
        }

        /// <summary>
        /// Override in each sub-page to load and push data to properties.
        /// Do NOT call LoadAsync() from here — already inside the guard.
        /// </summary>
        protected abstract Task LoadCoreAsync();
    }
}