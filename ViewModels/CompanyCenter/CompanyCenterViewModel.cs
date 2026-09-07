// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/CompanyCenter/CompanyCenterViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: Company Center (Phase 1 — master data foundation)
//
// Owns the company profiles used across the business:
//   • Multiple companies, one DEFAULT (pre-selected in PI Step 1)
//   • Full identity: logo, TRN, representative, mobile, e-mail,
//     bank account — everything invoices may need
//   • Persists to masterdata.db via MasterDataRepository
//   • Every change is audit-logged with the active user
//
// Proforma Invoice Step 1 reads the same records as selectable cards.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;

using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.MasterData.Repository;
using Microsoft.Extensions.DependencyInjection;
namespace ProGlassAutomation.ViewModels.CompanyCenter
{
    /// <summary>One company card on the Company Center page.</summary>
    public sealed class CompanyCard : ViewModelBase
    {
        public int Id { get; set; }
        public string CompanyName { get; set; } = "";
        public string CompanyTrn { get; set; } = "";
        public string CompanyLocation { get; set; } = "";
        public string LogoPath { get; set; } = "";
        public string Representative { get; set; } = "";
        // IsDefault removed 2026-09-04 (default-mark mechanism deleted).
        public int UsageCount { get; set; }

        // v3 — branding carried onto the card so the editor hydrates it
        // without a second DB round-trip.
        public CompanyLogoLayout LogoLayout { get; set; } = CompanyLogoLayout.None;
        public double LogoWidthMm { get; set; } = 22;
        public double LogoHeightMm { get; set; } = 26;

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }
    }

    public sealed class CompanyCenterViewModel : ViewModelBase
    {
        private readonly IDialogService _dialogs = null!;
        private readonly IMasterDataRepository _masterData = null!;

        public CompanyCenterViewModel(IDialogService? dialogs, IMasterDataRepository? masterData)
        {
            _dialogs = dialogs!;
            _masterData = masterData!;

            // BUG FIX (2026-09-07): command wiring + first load MUST live in
            // this constructor. The container resolves the GREEDY overload
            // (IDialogService + IMasterDataRepository are both registered), so
            // the parameterless ctor was never reached through DI — leaving
            // every command null and the card list empty. That is why "Save"
            // appeared to do nothing: SaveCompanyCommand was bound to null.
            Initialize();
        }

        private readonly ObservableCollection<CompanyCard> _companies = new();
        public ObservableCollection<CompanyCard> Companies => _companies;

        // ── editor state ─────────────────────────────────────────────
        private int _editingId;
        private string _name = "";
        private string _trn = "";
        private string _location = "";
        private string _logoPath = "";
        private string _representative = "";
        private string _mobile = "";
        private string _email = "";
        private string _account = "";
        private string _bank = "";
        // 2026-09-04: `_isDefault` / IsDefaultCompany REMOVED — the
        // mark-to-use-in-invoice authority is gone; Step 1 selection is the
        // only way a company lands on an invoice. The DB column is kept
        // (storage-compat, inert); see repository notes.
        private string _status = "";

        public string EditorTitle => _editingId == 0
            ? "NEW COMPANY" : "EDIT COMPANY";

        public string CompanyName
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }
        public string CompanyTrn
        {
            get => _trn;
            set { _trn = value; OnPropertyChanged(); }
        }
        public string CompanyLocation
        {
            get => _location;
            set { _location = value; OnPropertyChanged(); }
        }
        public string LogoPath
        {
            get => _logoPath;
            set { _logoPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasLogo)); }
        }
        public bool HasLogo => !string.IsNullOrEmpty(_logoPath);

        // v3 — logo placement controls (two industrial layouts + mm sizing).
        private CompanyLogoLayout _logoLayout = CompanyLogoLayout.None;
        public CompanyLogoLayout LogoLayout
        {
            get => _logoLayout;
            set
            {
                if (_logoLayout == value) return;
                _logoLayout = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsNoLogoLayout));
                OnPropertyChanged(nameof(IsLeftPortraitLayout));
                OnPropertyChanged(nameof(IsTopPanelLayout));
                OnPropertyChanged(nameof(LogoLayoutLabel));
            }
        }

        /// <summary>Human label for the chosen logo layout, bound by the masthead.</summary>
        public string LogoLayoutLabel => LayoutLabel(_logoLayout);

        /// <summary>True when no logo (text-only letterhead) is chosen.</summary>
        public bool IsNoLogoLayout
        {
            get => _logoLayout == CompanyLogoLayout.None;
            set { if (value) LogoLayout = CompanyLogoLayout.None; }
        }

        /// <summary>True when the "left small portrait logo" layout is chosen.</summary>
        public bool IsLeftPortraitLayout
        {
            get => _logoLayout == CompanyLogoLayout.LeftPortrait;
            set { if (value) LogoLayout = CompanyLogoLayout.LeftPortrait; }
        }

        /// <summary>True when the "top panel" industrial layout is chosen.</summary>
        public bool IsTopPanelLayout
        {
            get => _logoLayout == CompanyLogoLayout.TopPanel;
            set { if (value) LogoLayout = CompanyLogoLayout.TopPanel; }
        }

        private double _logoWidthMm = 22;
        public double LogoWidthMm
        {
            get => _logoWidthMm;
            set
            {
                if (_logoWidthMm == value) return;
                _logoWidthMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LogoWidthDisplay));
            }
        }

        private double _logoHeightMm = 26;
        public double LogoHeightMm
        {
            get => _logoHeightMm;
            set
            {
                if (_logoHeightMm == value) return;
                _logoHeightMm = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LogoHeightDisplay));
            }
        }

        /// <summary>Display text for the logo width spin — "22 mm".</summary>
        public string LogoWidthDisplay => $"{_logoWidthMm:0.#} mm";
        public string LogoHeightDisplay => $"{_logoHeightMm:0.#} mm";

        /// <summary>
        /// Sanitised width used when persisting: outside 8–60 mm the value is
        /// clamped so a typo can never produce a degenerate PDF box.
        /// </summary>
        public double SanitizedLogoWidthMm => Math.Clamp(
            _logoWidthMm > 0 ? _logoWidthMm : 22, 8, 60);
        public double SanitizedLogoHeightMm => Math.Clamp(
            _logoHeightMm > 0 ? _logoHeightMm : 26, 8, 60);

        /// <summary>Human label for a layout value, used by the selector.</summary>
        public static string LayoutLabel(CompanyLogoLayout layout) => layout switch
        {
            CompanyLogoLayout.LeftPortrait => "Left portrait logo",
            CompanyLogoLayout.TopPanel => "Top panel logo",
            _ => "No logo (text only)"
        };
        public string Representative
        {
            get => _representative;
            set { _representative = value; OnPropertyChanged(); }
        }
        public string RepresentativeMobile
        {
            get => _mobile;
            set { _mobile = value; OnPropertyChanged(); }
        }
        public string CompanyEmail
        {
            get => _email;
            set { _email = value; OnPropertyChanged(); }
        }
        public string AccountNumber
        {
            get => _account;
            set { _account = value; OnPropertyChanged(); }
        }
        public string BankName
        {
            get => _bank;
            set { _bank = value; OnPropertyChanged(); }
        }
        public string Status
        {
            get => _status;
            private set { _status = value; OnPropertyChanged(); }
        }

        // ── commands ─────────────────────────────────────────────────
        private string _reasonNote = "";
        /// <summary>
        /// PHASE D — optional WHY note attached to the next save. The
        /// audit trail already records WHICH fields changed; this records
        /// the reason, which a reviewer would otherwise have to guess.
        /// Cleared after every successful save so a note is never reused
        /// accidentally on an unrelated edit.
        /// </summary>
        public string ReasonNote
        {
            get => _reasonNote;
            set { _reasonNote = value ?? ""; OnPropertyChanged(); }
        }

        public ICommand NewCompanyCommand { get; private set; }
        public ICommand SaveCompanyCommand { get; private set; }
        public ICommand DeleteCompanyCommand { get; private set; }
        public ICommand SelectCompanyCommand { get; private set; }
        public ICommand PickLogoCommand { get; private set; }
        public ICommand ClearLogoCommand { get; private set; }
        public ICommand RefreshCommand { get; private set; }

        public CompanyCenterViewModel()
         : this(
            App.ServiceProvider?.GetService<IDialogService>()?? new DialogService(),
            App.ServiceProvider?.GetService<IMasterDataRepository>()!)
        {
        }

        // ══════════════════════════════════════════════════════════════
        //  INITIALIZATION — commands + first load. Shared by both ctors.
        // ══════════════════════════════════════════════════════════════
        private void Initialize()
        {
            NewCompanyCommand = new RelayCommand(_ => BeginEdit(null));
            SelectCompanyCommand = new RelayCommand(p =>
                BeginEdit(p as CompanyCard));
            PickLogoCommand = new RelayCommand(_ => PickLogo());
            ClearLogoCommand = new RelayCommand(_ => LogoPath = "");
            RefreshCommand = new RelayCommand(async _ => await LoadAsync());

            SaveCompanyCommand = new RelayCommand(async _ =>
            {
                if (string.IsNullOrWhiteSpace(CompanyName))
                {
                    Status = "Company name is required.";
                    return;
                }

                try
                {
                    if (_masterData == null)
                    {
                        Status = "Master data service unavailable.";
                        return;
                    }

                    var entity = new SavedCompany
                    {
                        Id = _editingId,
                        CompanyName = CompanyName.Trim(),
                        CompanyTrn = CompanyTrn.Trim(),
                        CompanyLocation = CompanyLocation.Trim(),
                        LogoPath = LogoPath,
                        Representative = Representative.Trim(),
                        RepresentativeMobile = RepresentativeMobile.Trim(),
                        CompanyEmail = CompanyEmail.Trim(),
                        AccountNumber = AccountNumber.Trim(),
                        BankName = BankName.Trim(),
                        LogoLayout = LogoLayout,
                        LogoWidthMm = SanitizedLogoWidthMm,
                        LogoHeightMm = SanitizedLogoHeightMm,
                        UsageCount = 1,
                        LastUsedAt = DateTime.Now,
                        CreatedAt = DateTime.Now
                    };

                    // 2026-09-04: entity has no IsDefault assignment — the
                    // mark mechanism (single-default clearing loop) is
                    // removed with the checkbox. Step 1 selection is the
                    // only authority for "which company is this invoice".

                    // PHASE D: capture the BEFORE state so the audit
                    // entry can carry a real field-level diff. Read before
                    // the write, and never let a diff failure block a save.
                    List<AuditChange> changes = new();
                    if (_editingId != 0)
                    {
                        try
                        {
                            var previous = (await _masterData
                                    .GetAllCompaniesAsync())
                                .FirstOrDefault(c => c.Id == _editingId);

                            if (previous != null)
                                changes = BuildCompanyDiff(previous, entity);
                        }
                        catch (Exception diffEx)
                        {
                            AppLogService.Debug(
                                "[CompanyCenter] diff skipped: " + diffEx.Message);
                        }
                    }

                    await _masterData.SaveOrUpdateCompanyAsync(entity);

                    string reason = ReasonNote.Trim();

                    AuditService.LogWithChanges(
                        AuditSections.Settings,
                        _editingId == 0 ? "Company created" : "Company updated",
                        entity.CompanyName,
                        "COMPANY",
                        entity.Id.ToString(),
                        changes,
                        reason);

                    Status = "Saved ✔ " + entity.CompanyName;
                    ReasonNote = "";
                    BeginEdit(null);
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    Status = "Save failed: " + ExceptionMessages.Deep(ex);
                }
            });

            DeleteCompanyCommand = new RelayCommand(async p =>
            {
                if (p is not CompanyCard card) return;

                var result = _dialogs.Show(
                    string.Format(LocalizationService.Instance.T("Msg.DeleteCompanyXExistingInvoices"), card.CompanyName),
                    LocalizationService.Instance.T("Msg.CapDeleteCompany"),
                    System.Windows.MessageBoxButton.YesNo,
                    System.Windows.MessageBoxImage.Warning);

                if (result != System.Windows.MessageBoxResult.Yes) return;

                try
                {
                    if (_masterData == null) return;
                    await _masterData.DeleteCompanyAsync(card.Id);
                    AuditService.Log(AuditSections.Settings,
                        "Company deleted", card.CompanyName);
                    Status = "Deleted: " + card.CompanyName;
                    await LoadAsync();
                }
                catch (Exception ex)
                {
                    Status = "Delete failed: " + ExceptionMessages.Deep(ex);
                }
            });

            _ = LoadAsync();
        }

        // ══════════════════════════════════════════════════════════════
        //  PHASE D — field-level diff for the audit trail
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Compares the stored company against the edited one and returns
        /// one AuditChange per field that actually moved. Unchanged fields
        /// are omitted so the diff viewer shows signal, not noise.
        /// </summary>
        private static List<AuditChange> BuildCompanyDiff(
            SavedCompany before, SavedCompany after)
        {
            var changes = new List<AuditChange>();

            void Compare(string field, string? oldValue, string? newValue)
            {
                oldValue ??= "";
                newValue ??= "";
                if (string.Equals(oldValue, newValue, StringComparison.Ordinal))
                    return;
                changes.Add(AuditChangeSerializer.Modified(
                    field, oldValue, newValue));
            }

            Compare("Name", before.CompanyName, after.CompanyName);
            Compare("TRN", before.CompanyTrn, after.CompanyTrn);
            Compare("Location", before.CompanyLocation, after.CompanyLocation);
            Compare("Logo", before.LogoPath, after.LogoPath);
            Compare("Logo layout",
                before.LogoLayout.ToString(), after.LogoLayout.ToString());
            Compare("Logo width (mm)",
                before.LogoWidthMm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                after.LogoWidthMm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            Compare("Logo height (mm)",
                before.LogoHeightMm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                after.LogoHeightMm.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture));
            Compare("Representative", before.Representative, after.Representative);
            Compare("Mobile", before.RepresentativeMobile, after.RepresentativeMobile);
            Compare("Email", before.CompanyEmail, after.CompanyEmail);
            Compare("Account", before.AccountNumber, after.AccountNumber);
            Compare("Bank", before.BankName, after.BankName);
            return changes;
        }

        // ── loading / editing ────────────────────────────────────────

        public async System.Threading.Tasks.Task LoadAsync()
        {
            _companies.Clear();
            if (_masterData == null) return;

            try
            {
                // 2026-09-04: default-first ordering removed with the mark —
                // plain most-used-first (repository's canonical order).
                foreach (var c in (await _masterData.GetAllCompaniesAsync())
                    .OrderByDescending(x => x.UsageCount))
                {
                    _companies.Add(new CompanyCard
                    {
                        Id = c.Id,
                        CompanyName = c.CompanyName,
                        CompanyTrn = c.CompanyTrn,
                        CompanyLocation = c.CompanyLocation,
                        LogoPath = c.LogoPath ?? "",
                        Representative = c.Representative ?? "",
                        UsageCount = c.UsageCount,
                        LogoLayout = c.LogoLayout,
                        LogoWidthMm = c.LogoWidthMm > 0 ? c.LogoWidthMm : 22,
                        LogoHeightMm = c.LogoHeightMm > 0 ? c.LogoHeightMm : 26
                    });
                }
            }
            catch (Exception ex)
            {
                Status = "Load failed: " + ExceptionMessages.Deep(ex);
            }
        }

        private void BeginEdit(CompanyCard? card)
        {
            _editingId = card?.Id ?? 0;
            CompanyName = card?.CompanyName ?? "";
            CompanyTrn = card?.CompanyTrn ?? "";
            CompanyLocation = card?.CompanyLocation ?? "";
            LogoPath = card?.LogoPath ?? "";
            Representative = card?.Representative ?? "";
            RepresentativeMobile = "";
            CompanyEmail = "";
            AccountNumber = "";
            BankName = "";
            // Branding: card carries it; new records start with the
            // industrial defaults (text-only until the operator adds a logo).
            LogoLayout = card?.LogoLayout ?? CompanyLogoLayout.None;
            LogoWidthMm = card != null && card.LogoWidthMm > 0 ? card.LogoWidthMm : 22;
            LogoHeightMm = card != null && card.LogoHeightMm > 0 ? card.LogoHeightMm : 26;
            // mark-to-use-in-invoice removed 2026-09-04: no IsDefault hydration.
            OnPropertyChanged(nameof(EditorTitle));

            // When editing, hydrate the remaining profile fields.
            if (card != null && _masterData != null)
            {
                _ = HydrateAsync(card.Id);
            }

            foreach (var c in _companies)
                c.IsSelected = c.Id == _editingId && _editingId != 0;
        }

        private async System.Threading.Tasks.Task HydrateAsync(int id)
        {
            try
            {
                var all = await _masterData!.GetAllCompaniesAsync();
                var entity = all.FirstOrDefault(x => x.Id == id);
                if (entity == null) return;

                RepresentativeMobile = entity.RepresentativeMobile ?? "";
                CompanyEmail = entity.CompanyEmail ?? "";
                AccountNumber = entity.AccountNumber ?? "";
                BankName = entity.BankName ?? "";
                // Hydration must not stomp values BeginEdit already took from
                // the card; it only tops up the fields the card didn't carry.
                LogoLayout = entity.LogoLayout;
                if (entity.LogoWidthMm > 0) LogoWidthMm = entity.LogoWidthMm;
                if (entity.LogoHeightMm > 0) LogoHeightMm = entity.LogoHeightMm;
            }
            catch (System.Exception ex)
            {
                // editor stays partially filled — harmless
                ProGlassAutomation.Services.Logging.AppLogService.Debug("[CompanyCenterViewModel] Company profile partial-fill failed: " + ExceptionMessages.Deep(ex));
            }
        }

        private void PickLogo()
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Company logo",
                Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp|All files|*.*"
            };
            if (dialog.ShowDialog() == true)
                LogoPath = dialog.FileName;
        }
    }
}
