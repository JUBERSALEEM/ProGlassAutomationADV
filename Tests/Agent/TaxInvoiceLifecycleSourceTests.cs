using System;
using System.IO;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class TaxInvoiceLifecycleSourceTests
    {
        [Fact]
        public void Result_delete_path_reports_void_and_delete_as_distinct_outcomes()
        {
            string source = ReadRepoFile("Services/TaxInvoice/Repository/TaxInvoiceRepository.cs");

            Assert.Contains("DeleteOutcome.Voided", source);
            Assert.Contains("DeleteOutcome.Deleted", source);
            Assert.Contains("Operation = wasVoided ? \"Void\" : \"Delete\"", source);
            Assert.Contains("TI voided by delete request", source);
        }

        [Fact]
        public void Retried_void_retries_the_cross_database_unlink()
        {
            string source = ReadRepoFile("Services/TaxInvoice/Repository/TaxInvoiceRepository.cs");
            int voidStart = source.IndexOf("public async Task VoidAsync(", StringComparison.Ordinal);
            Assert.True(voidStart >= 0);
            int voidEnd = source.IndexOf("private static async Task<bool> HasLinkedDeliveryNotesAsync", voidStart,
                StringComparison.Ordinal);
            Assert.True(voidEnd > voidStart);

            string method = source.Substring(voidStart, voidEnd - voidStart);
            int alreadyVoid = method.IndexOf("if (entity.IsVoid)", StringComparison.Ordinal);
            int clearLinks = method.IndexOf("ClearLinkedDeliveryNotesAsync(invoiceNumber)", StringComparison.Ordinal);
            int versionCheck = method.IndexOf("if (entity.RowVersion != expectedRowVersion)",
                StringComparison.Ordinal);
            Assert.True(alreadyVoid >= 0 && clearLinks > alreadyVoid
                && versionCheck > clearLinks,
                "An already-void invoice must retry its DN unlink after a partial commit, " +
                "even when the original caller's version is now stale.");
        }

        [Fact]
        public void Startup_reconciler_skips_void_invoices_but_does_not_silently_skip_empty_reference_stale_links()
        {
            string source = ReadRepoFile("Services/TaxInvoice/Repository/TaxInvoiceDnReconciler.cs");

            int voidCheck = source.IndexOf("if (inv.IsVoid || DocumentLifecyclePolicy.IsVoidTerminal(",
                StringComparison.Ordinal);
            Assert.True(voidCheck >= 0);
            int voidStaleCheck = source.IndexOf("if (retainedVoidLinks.Count > 0)", voidCheck,
                StringComparison.Ordinal);
            int staleCheck = source.IndexOf("if (staleLinks.Count > 0)", StringComparison.Ordinal);
            int emptyReferenceContinue = source.IndexOf("if (dnIds.Count == 0) continue", StringComparison.Ordinal);
            Assert.True(voidCheck >= 0 && voidStaleCheck > voidCheck,
                "Voided invoices must not be relinked, and any leftover ownership must be reported.");
            Assert.True(staleCheck >= 0 && emptyReferenceContinue > staleCheck,
                "Stale links must be reported before an empty reference list is skipped.");
            Assert.Contains("Voided Tax Invoice", source);
            Assert.Contains("No automatic relink or unlink was attempted", source);
        }

        [Fact]
        public void Tax_invoice_editor_uses_status_only_transition_after_body_update_boundary()
        {
            string source = ReadRepoFile(
                "ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Save.cs");

            Assert.Contains("DocumentLifecyclePolicy.CanEditBody", source);
            Assert.Contains("TaxInvoiceMapper.SetHeaderStatus(updated, entity.Status)", source);
            Assert.Contains("entity.InvoiceNumber, targetStatus, _loadedRowVersion.Value", source);
            Assert.Contains("bool statusOnlyTransition = false", source);
            Assert.Contains("if (!statusOnlyTransition)", source);
            Assert.Contains("bodySaveCommitted = true", source);
            Assert.Contains("statusTransitionCommitted", source);
            Assert.Contains("Tax Invoice body was saved, but the status transition failed", source);
        }

        [Fact]
        public void Startup_reconciliation_waits_for_an_authorized_session_and_can_retry_failures()
        {
            string startup = ReadRepoFile("App.StartupCore.cs");
            string authHandler = ReadRepoFile("App.AuthHandlers.cs");

            Assert.DoesNotContain("TaxInvoiceDnReconciler.ReconcileWithResultAsync", startup);
            Assert.Contains("_taxInvoiceReconciliationScheduled", authHandler);
            Assert.Contains("AuditService.CurrentUser?.IsActive == true", authHandler);
            Assert.Contains("PermissionService.Can(AppPermissions.DeliveryNoteInvoiceLink)", authHandler);
            Assert.Contains("Interlocked.CompareExchange", authHandler);
            Assert.Contains("ref _taxInvoiceReconciliationScheduled, 0", authHandler);
            Assert.Contains("Post-login TI↔DN reconciliation failed", authHandler);

            int presenceFailure = startup.IndexOf("Presence service unavailable", StringComparison.Ordinal);
            int userChangedSubscription = startup.IndexOf(
                "AuditService.CurrentUserChanged += OnCurrentUserChanged", StringComparison.Ordinal);
            Assert.True(presenceFailure >= 0 && userChangedSubscription > presenceFailure,
                "Auth lifecycle handlers must subscribe even if optional Presence startup fails.");
        }

        [Fact]
        public void Tax_invoice_insert_canonicalizes_snapshot_header_and_normalized_item_keys()
        {
            string source = ReadRepoFile("Services/TaxInvoice/Repository/TaxInvoiceRepository.cs");
            int insertStart = source.IndexOf("public async Task<SavedTaxInvoice> InsertAsync(",
                StringComparison.Ordinal);
            int updateStart = source.IndexOf("public async Task UpdateAsync(\n            SavedTaxInvoice entity,\n            int expectedRowVersion)",
                insertStart, StringComparison.Ordinal);
            Assert.True(insertStart >= 0 && updateStart > insertStart);

            string method = source.Substring(insertStart, updateStart - insertStart);
            Assert.Contains("TaxInvoiceMapper.SynchronizeSnapshotHeader(entity)", method);
            Assert.Contains("item.InvoiceNumber = entity.InvoiceNumber", method);
            Assert.Contains("item.ProformaId = entity.ProformaId", method);
        }

        [Fact]
        public void Credit_note_insert_stamps_the_assigned_number_on_normalized_items()
        {
            string source = ReadRepoFile("Services/CreditNote/Repository/CreditNoteRepository.cs");
            int insertStart = source.IndexOf("public async Task<SavedCreditNote> InsertAsync(",
                StringComparison.Ordinal);
            int updateStart = source.IndexOf("public async Task UpdateAsync(SavedCreditNote entity)",
                insertStart, StringComparison.Ordinal);
            Assert.True(insertStart >= 0 && updateStart > insertStart);

            string method = source.Substring(insertStart, updateStart - insertStart);
            Assert.Contains("item.CreditNoteNumber = entity.CreditNoteNumber", method);
            Assert.Contains("item.TaxInvoiceReference = entity.TaxInvoiceReference", method);
            Assert.Contains("item.ProformaId = entity.ProformaId", method);
        }

        [Fact]
        public void Detached_tax_invoice_update_cannot_forge_void_fields_or_rekey_the_invoice()
        {
            string source = ReadRepoFile("Services/TaxInvoice/Repository/TaxInvoiceRepository.cs");
            int updateStart = source.IndexOf(
                "public async Task UpdateAsync(\n            SavedTaxInvoice entity,\n            int expectedRowVersion)",
                StringComparison.Ordinal);
            int deleteStart = source.IndexOf(
                "public async Task DeleteAsync(int id, int expectedRowVersion)", updateStart,
                StringComparison.Ordinal);
            Assert.True(updateStart >= 0 && deleteStart > updateStart);

            string method = source.Substring(updateStart, deleteStart - updateStart);
            Assert.Contains("entity.IsVoid != stored.IsVoid", method);
            Assert.Contains("entity.VoidedAtUtc != stored.VoidedAtUtc", method);
            Assert.Contains("stored.ProformaId, entity.ProformaId", method);
            Assert.Contains("stored.SequenceNumber != entity.SequenceNumber", method);
            Assert.Contains("TaxInvoiceMapper.SynchronizeSnapshotHeader(entity)", method);
            Assert.Contains("ctx.Entry(stored).CurrentValues.SetValues(entity)", method);
            Assert.DoesNotContain("ctx.TaxInvoices.Update(entity)", method);
        }

        [Fact]
        public void Tax_invoice_delete_and_void_require_versions_at_every_repository_boundary()
        {
            string contract = ReadRepoFile("Services/TaxInvoice/Repository/ITaxInvoiceRepository.cs");
            string repository = ReadRepoFile("Services/TaxInvoice/Repository/TaxInvoiceRepository.cs");
            string coordinator = ReadRepoFile("Services/Workflow/DocumentOperationCoordinator.cs");
            string list = ReadRepoFile("ViewModels/TaxInvoice/TaxInvoiceList/TaxInvoiceListViewModel.cs");
            string detail = ReadRepoFile("ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.cs");
            string commands = ReadRepoFile("ViewModels/TaxInvoice/TaxInvoiceDetail/TaxInvoiceDetailViewModel.Commands.cs");

            Assert.Contains("Task DeleteAsync(int id, int expectedRowVersion);", contract);
            Assert.Contains("Task VoidAsync(int id, int expectedRowVersion, string reason);", contract);
            Assert.DoesNotContain("Task DeleteAsync(int id);", contract);
            Assert.DoesNotContain("Task VoidAsync(int id, string reason);", contract);
            Assert.Contains("DeleteCoreAsync(id, expectedRowVersion)", repository);
            Assert.Contains("entity.RowVersion != expectedRowVersion", repository);
            Assert.Contains("DeleteWithResultAsync(\n            int id, int expectedRowVersion", repository);
            Assert.Contains("int expectedRowVersion, string invoiceNumber", coordinator);
            Assert.Contains("m.Id, m.RowVersion", list);
            Assert.Contains("_loadedRowVersion.Value", detail);
            Assert.Contains("_loadedRowVersion.Value, reason", commands);
        }

        [Fact]
        public void Tax_invoice_status_and_void_writes_keep_snapshot_headers_canonical()
        {
            string source = ReadRepoFile("Services/TaxInvoice/Repository/TaxInvoiceRepository.cs");
            Assert.Contains("snapshot is missing a readable header; the void was not saved", source);
            Assert.Contains("snapshot is missing a readable header; the status change was not saved", source);
            Assert.True(source.Split(
                    new[] { "TaxInvoiceMapper.SynchronizeSnapshotHeader(entity)" },
                    StringSplitOptions.None).Length >= 5,
                "Insert, body update, status-only update, and void should canonicalize the snapshot header.");
        }

        [Fact]
        public void Detached_credit_note_update_cannot_forge_void_fields_or_rekey_the_family()
        {
            string source = ReadRepoFile("Services/CreditNote/Repository/CreditNoteRepository.cs");
            int updateStart = source.IndexOf(
                "public async Task UpdateAsync(\n            SavedCreditNote entity,\n            int expectedRowVersion)",
                StringComparison.Ordinal);
            int deleteStart = source.IndexOf("public async Task DeleteAsync(int id)", updateStart,
                StringComparison.Ordinal);
            Assert.True(updateStart >= 0 && deleteStart > updateStart);

            string method = source.Substring(updateStart, deleteStart - updateStart);
            Assert.Contains("entity.IsVoid != stored.IsVoid", method);
            Assert.Contains("entity.VoidedAtUtc != stored.VoidedAtUtc", method);
            Assert.Contains("stored.TaxInvoiceReference, entity.TaxInvoiceReference", method);
            Assert.Contains("stored.ProformaId, entity.ProformaId", method);
            Assert.Contains("stored.JobOrderId, entity.JobOrderId", method);
            Assert.Contains("ctx.Entry(stored).CurrentValues.SetValues(entity)", method);
            Assert.DoesNotContain("ctx.CreditNotes.Update(entity)", method);
        }

        [Fact]
        public void Job_order_update_canonicalizes_snapshot_identity_status_and_embedded_pi()
        {
            string source = ReadRepoFile("Services/JobOrder/Repository/JobOrderRepository.cs");
            int updateStart = source.IndexOf(
                "public async Task UpdateAsync(SavedJobOrder jobOrder, int expectedRowVersion)",
                StringComparison.Ordinal);
            int statusStart = source.IndexOf("public async Task<SavedJobOrder?> UpdateStatusAsync(",
                updateStart, StringComparison.Ordinal);
            Assert.True(updateStart >= 0 && statusStart > updateStart);

            string method = source.Substring(updateStart, statusStart - updateStart);
            Assert.Contains("incomingSnapshot.Production.Status = stored.Status", method);
            Assert.Contains("incomingSnapshot.EmbeddedInvoice = storedSnapshot.EmbeddedInvoice", method);
            Assert.Contains("stored.FullSnapshotJson = JobOrderMapper.SerializeSnapshot(incomingSnapshot)", method);
        }

        [Fact]
        public void Every_document_void_repository_enforces_the_status_transition_table()
        {
            string[] paths =
            {
                "Services/ProformaInvoice/ProformaInvoiceRepository/ProformaInvoiceRepository.cs",
                "Services/JobOrder/Repository/JobOrderRepository.cs",
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.Part2.cs",
                "Services/TaxInvoice/Repository/TaxInvoiceRepository.cs",
                "Services/CreditNote/Repository/CreditNoteRepository.cs"
            };

            foreach (string path in paths)
                Assert.Contains("DocumentLifecyclePolicy.CanVoid", ReadRepoFile(path));
        }

        [Fact]
        public void Generic_delivery_note_insert_cannot_forge_internal_master_trackers()
        {
            string source = ReadRepoFile(
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs");
            int insertStart = source.IndexOf("public async Task<SavedDeliveryNote> InsertAsync(",
                StringComparison.Ordinal);
            int nextMethod = source.IndexOf("public async Task UpdateAsync(SavedDeliveryNote entity)",
                insertStart, StringComparison.Ordinal);
            Assert.True(insertStart >= 0 && nextMethod > insertStart);

            string method = source.Substring(insertStart, nextMethod - insertStart);
            Assert.Contains("if (entity.IsMasterDn)", method);
            Assert.Contains("must be created through CreateMasterDnAsync", method);
            Assert.DoesNotContain("masterCtx.DeliveryNotes.Add(entity)", method);
        }

        [Fact]
        public void Delivery_note_invoice_links_require_an_existing_live_tax_invoice_owner()
        {
            string source = ReadRepoFile(
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs");
            Assert.Contains("EnsureTaxInvoiceOwnerAsync(invoiceKey, requireLive: true)", source);
            Assert.Contains("GetByInvoiceNumberAsync(invoiceKey)", source);
            Assert.Contains("Cannot link Delivery Notes to missing Tax Invoice", source);
            Assert.Contains("DocumentLifecyclePolicy.IsVoidTerminal", source);
            Assert.Contains("requireLive: ids.Count > 0", source);
        }

        [Fact]
        public void Delivery_note_body_and_status_writes_use_separate_lifecycle_boundaries()
        {
            string source = ReadRepoFile("Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs");
            int updateStart = source.IndexOf(
                "public async Task UpdateAsync(SavedDeliveryNote entity, int expectedRowVersion)",
                StringComparison.Ordinal);
            int statusStart = source.IndexOf(
                "public async Task<SavedDeliveryNote?> UpdateStatusAsync(",
                StringComparison.Ordinal);
            Assert.True(updateStart >= 0 && statusStart > updateStart);

            string bodyMethod = source.Substring(updateStart, statusStart - updateStart);
            Assert.Contains("DocumentLifecyclePolicy.CanEditBody", bodyMethod);
            Assert.Contains("stored.HasTaxInvoice", bodyMethod);
            Assert.Contains("StatusTransitions.Validate", bodyMethod);
            Assert.Contains("stored.IsVoid", bodyMethod);
            Assert.Contains("expectedRowVersion", bodyMethod);
            Assert.Contains("ConcurrencyConflictException", bodyMethod);

            string statusMethod = source.Substring(statusStart);
            Assert.Contains("int id, string newStatus, int expectedRowVersion", statusMethod);
            Assert.Contains("OperationGuard.Demand", statusMethod);
            Assert.Contains("ReadOnlyGate.Demand", statusMethod);
            Assert.Contains("StatusTransitions.Validate", statusMethod);
            Assert.Contains("ConcurrencyConflictException", statusMethod);
            Assert.Contains("AuditService.Log", statusMethod);
        }

        [Fact]
        public void Delivery_note_attachments_are_removed_only_after_physical_row_deletion()
        {
            string source = ReadRepoFile(
                "ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.Part3.cs");
            int delete = source.IndexOf("await _deliveryNotes.DeleteAsync(_entity.Id)",
                StringComparison.Ordinal);
            int retainedCheck = source.IndexOf("await _deliveryNotes.GetByIdAsync(_entity.Id)",
                delete, StringComparison.Ordinal);
            int fileDelete = source.IndexOf("_attachmentService.DeleteFile(att.FilePath)",
                retainedCheck, StringComparison.Ordinal);

            Assert.True(delete >= 0 && retainedCheck > delete && fileDelete > retainedCheck);
            Assert.Contains("if (retained != null)", source);
        }

        [Fact]
        public void Delivery_note_status_ui_paths_call_the_status_only_repository_method()
        {
            string listSource = ReadRepoFile(
                "ViewModels/DeliveryNote/DeliveryNoteList/DeliveryNoteListViewModel.Part2.cs");
            string viewSource = ReadRepoFile(
                "Views/DeliveryNote/DeliveryNoteList/DeliveryNoteListView.xaml.cs");

            Assert.Contains("entity.Id, newStatus, entity.RowVersion", listSource);
            Assert.Contains("latestDn.Id, s, latestDn.RowVersion", viewSource);

            string detailSource = ReadRepoFile(
                "ViewModels/DeliveryNote/DeliveryNoteDetail/DeliveryNoteDetailViewModel.Part2.cs");
            Assert.Contains("UpdateAsync(_entity, _entity.RowVersion)", detailSource);
            Assert.Contains("_entity.Id, Status, _entity.RowVersion", detailSource);
        }

        [Fact]
        public void Delivery_note_number_allocation_is_serialized_and_copies_the_final_identity()
        {
            string repository = ReadRepoFile(
                "Services/DeliveryNote/Repository/DeliveryNoteRepository.Part1.cs");
            string dbContext = ReadRepoFile(
                "Services/DeliveryNote/Database/DeliveryNotesDbContext.cs");
            string createService = ReadRepoFile(
                "Services/DeliveryNote/DeliveryNoteCreateService.cs");

            Assert.Contains(
                "BeginTransactionAsync(System.Data.IsolationLevel.Serializable)", repository);
            Assert.Contains("SchemaMigrationSql.RequireIndex(", repository);
            Assert.Contains("MaxAsync(x => (int?)x.SequenceNumber)", repository);
            Assert.Contains("entity.DeliveryNoteId = BuildDeliveryNoteId(entity.JobOrderId, sequence)", repository);
            Assert.Contains("PrepareAllocatedIdentity(entity)", repository);
            Assert.Contains("HasIndex(x => new { x.JobOrderId, x.SequenceNumber })", dbContext);
            Assert.Contains("IsUnique()", dbContext);
            Assert.DoesNotContain("ExistsByDeliveryNoteIdAsync", createService);
        }

        private static string ReadRepoFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (File.Exists(Path.Combine(directory.FullName, "ProGlassAutomation.csproj")))
                    return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate repository root from " + AppContext.BaseDirectory);
        }
    }
}
