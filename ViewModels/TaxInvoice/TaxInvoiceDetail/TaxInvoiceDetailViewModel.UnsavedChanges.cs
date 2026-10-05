// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.UnsavedChanges.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3.4 — partial split. This part owns the ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.UnsavedChanges members.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Models.TaxInvoice.Attachment;
using ProGlassAutomation.Models.TaxInvoice.SavedTaxInvoice;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.TaxInvoice;
using ProGlassAutomation.Services.TaxInvoice.Mapper;
using ProGlassAutomation.Utilities;
using ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceList;
using ProGlassAutomation.Views.TaxInvoice.TaxInvoiceDetail;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.Localization;
using ProGlassAutomation.Services.DeliveryNote.Repository;
using ProGlassAutomation.Services.Dialogs;
using ProGlassAutomation.Services.ProformaInvoice.ProformaInvoiceRepository;
using ProGlassAutomation.Services.TaxInvoice.Repository;
using Microsoft.Extensions.DependencyInjection;

namespace ProGlassAutomation.ViewModels.TaxInvoice.TaxInvoiceDetail
{
    public partial class TaxInvoiceDetailViewModel : ViewModelBase
    {
        // ══════════════════════════════════════════════════════════════
        //  v2 §28 — UNSAVED-CHANGES DETECTION
        //
        //  TI has dozens of editable fields; instead of wiring every
        //  setter, we serialize the wizard state into a signature after
        //  each load/save and compare on demand. One mechanism covers
        //  every field — nothing can be missed.
        // ══════════════════════════════════════════════════════════════

        private string _cleanStateSignature;

        private string BuildStateSignature()
        {
            try
            {
                return System.Text.Json.JsonSerializer
                    .Serialize(BuildWizardState());
            }
            catch
            {
                // Never block on a signature failure — treat as changed.
                return Guid.NewGuid().ToString();
            }
        }

        /// <summary>Call after a successful load/save.</summary>
        private void CaptureCleanState() =>
            _cleanStateSignature = BuildStateSignature();

        /// <summary>True when the editor differs from the last load/save.</summary>
        public bool IsDirtyNow()
        {
            if (IsNewInvoice) return true;
            if (_cleanStateSignature == null) return false;
            return _cleanStateSignature != BuildStateSignature();
        }

        /// <summary>v2 §28: save entry used by the unsaved-changes guard.</summary>
        public async Task<bool> TrySaveAsync()
        {
            await SaveAsync();
            return !IsDirtyNow();
        }

        /// <summary>
        /// v2 §24: loads an old version's snapshot into the editor.
        /// The database row is untouched until the user saves — saving
        /// appends a NEW version (history is never erased).
        /// </summary>
        public async Task<bool> RestoreVersionAsync(int versionId)
        {
            try
            {
                var version = await ProGlassAutomation.Services.Audit
                    .VersionHistoryService.GetByIdAsync(versionId);

                if (version == null ||
                    string.IsNullOrEmpty(version.SnapshotJson) ||
                    _taxInvoices == null)
                    return false;

                var entity = await _taxInvoices
                    .GetByInvoiceNumberAsync(version.EntityId);
                if (entity == null) return false;

                // Swap in the old snapshot and re-hydrate the UI.
                entity.FullSnapshotJson = version.SnapshotJson;
                var snapshot = TaxInvoiceMapper.DeserializeSnapshot(entity);
                if (snapshot == null) return false;

                _editingId = entity.Id;
                _editingInvoiceNumber = entity.InvoiceNumber;
                _loadedRowVersion = entity.RowVersion;   // Phase 5: concurrency token
                IsNewInvoice = false;

                RestoreFromSnapshot(entity, snapshot);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.TaxInvoice,
                    "Version restored",
                    $"{entity.InvoiceNumber} → version {version.VersionNo} " +
                    "(pending save)",
                    "TI", entity.InvoiceNumber);

                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
