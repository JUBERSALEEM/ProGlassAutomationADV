// ═══════════════════════════════════════════════════════════════════════
// FILE: DeliveryNoteRepository.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.DeliveryNote.Common.DeliveryNoteStatus;
using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Services.Database.SchemaMigrations;
using ProGlassAutomation.Services.DeliveryNote.Database;
using ProGlassAutomation.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Common;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Licensing;
using ProGlassAutomation.Services.Workflow;

namespace ProGlassAutomation.Services.DeliveryNote.Repository
{
        public partial class DeliveryNoteRepository : IDeliveryNoteRepository
    {
            /// <summary>
        /// Shared, EF-translatable projection to the list item. Every list
        /// method uses it so the FullSnapshotJson blob column is never read
        /// for a list query. This is the single source of truth for the list
        /// shape (mirrors ToListItem, but runs at the SQL level).
        /// </summary>
        private static readonly System.Linq.Expressions.Expression<
            Func<SavedDeliveryNote, DeliveryNoteListItem>> ToListItemProjection =
            x => new DeliveryNoteListItem
            {
                Id = x.Id,
                DeliveryNoteId = x.DeliveryNoteId,
                JobOrderId = x.JobOrderId,
                ProformaId = x.ProformaId,
                CustomerName = x.CustomerName,
                ProjectName = x.ProjectName,
                Status = x.Status,
                DeliveryType = x.DeliveryType,
                IsFoc = x.IsFoc,
                IsMasterDn = x.IsMasterDn,
                DriverName = x.DriverName,
                VehicleNumber = x.VehicleNumber,
                TotalOrderedQty = x.TotalOrderedQty,
                TotalDeliveredQty = x.TotalDeliveredQty,
                TotalBalanceQty = x.TotalBalanceQty,
                TotalPreviouslyDeliveredQty = x.TotalPreviouslyDeliveredQty,
                SequenceNumber = x.SequenceNumber,
                DeliveryDate = x.DeliveryDate,
                DeliveryTime = x.DeliveryTime,
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt,
                RowVersion = x.RowVersion,
                TaxInvoiceId = x.TaxInvoiceId,
                HasTaxInvoice = x.HasTaxInvoice,
                IsVoid = x.IsVoid
            };

        public async Task InitializeAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            await ctx.Database.EnsureCreatedAsync();

            // Additive schema upgrades for an existing deliverynotes.db.
            SchemaMigrationRunner.Upgrade(
                DeliveryNotesDbContext.DatabaseFilePath,
                AppSchemaMigrations.DeliveryNotes,
                "Delivery Note");
        }

        // ── Single DN retrieval ──────────────────────────────────────────

        public async Task<SavedDeliveryNote?> GetByIdAsync(int id)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id).ConfigureAwait(false);
        }

        public async Task<SavedDeliveryNote?> GetByDeliveryNoteIdAsync(
            string deliveryNoteId)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.DeliveryNoteId == deliveryNoteId).ConfigureAwait(false);
        }

        // ── CHAT 19 — Master DN methods ──────────────────────────────────

        public async Task<SavedDeliveryNote?> GetMasterDnForJobOrderAsync(
            string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x =>
                    x.JobOrderId == jobOrderId &&
                    x.IsMasterDn).ConfigureAwait(false);
        }

        public async Task<List<DeliveryNoteListItem>> GetMasterDnsAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            // PERF: project to the list item at the SQL level so the
            // FullSnapshotJson blob is never read for a list query.
            return await ctx.DeliveryNotes
                .Where(x => x.IsMasterDn)
                .AsNoTracking()
                .OrderByDescending(x => x.UpdatedAt)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<SavedDeliveryNote?> CreateMasterDnAsync(
            string jobOrderId,
            string proformaId,
            string customerName,
            string projectName,
            string masterDnId,
            List<DeliveryNoteItem> items)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "create a master delivery note");
            ReadOnlyGate.Demand("create a master delivery note");

            using var ctx = new DeliveryNotesDbContext();

            bool exists = await ctx.DeliveryNotes
                .AnyAsync(x => x.JobOrderId == jobOrderId && x.IsMasterDn).ConfigureAwait(false);
            if (exists) return null;

            var now = DateTime.Now;
            int totalQty = items.Sum(i => i.OrderedQty);

            var masterDn = new SavedDeliveryNote
            {
                DeliveryNoteId = masterDnId,
                JobOrderId = jobOrderId,
                ProformaId = proformaId,
                SequenceNumber = 0,
                CustomerName = customerName,
                ProjectName = projectName,
                Status = "Draft",
                DeliveryType = "Dummy",
                IsMasterDn = true,
                LastUpdatedFromDnId = "",
                IsFoc = false,
                DriverName = "",
                VehicleNumber = "",
                ReceiverName = "",
                DeliveryDate = now,
                DeliveryTime = now.ToString("HH:mm"),
                Notes = "Master DN — auto-generated reference tracker",
                TotalOrderedQty = totalQty,
                TotalDeliveredQty = 0,
                TotalBalanceQty = totalQty,
                TotalPreviouslyDeliveredQty = 0,
                TaxInvoiceId = "",
                HasTaxInvoice = false,
                FullSnapshotJson = "{}",
                CreatedAt = now,
                UpdatedAt = now,
                Items = items
            };

            ctx.DeliveryNotes.Add(masterDn);
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            return masterDn;
        }

        public async Task UpdateMasterDnAsync(
            string jobOrderId,
            string triggeredByDnId)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "update a master delivery note");
            ReadOnlyGate.Demand("update a master delivery note");

            using var ctx = new DeliveryNotesDbContext();

            var masterDn = await ctx.DeliveryNotes
                .Include(x => x.Items)
                .FirstOrDefaultAsync(x =>
                    x.JobOrderId == jobOrderId &&
                    x.IsMasterDn).ConfigureAwait(false);

            if (masterDn == null) return;

            // Confirmed deliveries only. Dispatching/Draft qty is in-progress
            // and must not consume Master remaining until SAVE → Dispatched.
            var realDnIds = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId &&
                            !x.IsMasterDn &&
                            x.DeliveryType != "Dummy" &&
                            (x.Status == DeliveryNoteStatusValues.Dispatched ||
                             x.Status == DeliveryNoteStatusValues.Delivered ||
                             x.Status == DeliveryNoteStatusValues.Signed ||
                             x.Status == DeliveryNoteStatusValues.Received))
                .Select(x => x.DeliveryNoteId)
                .ToListAsync().ConfigureAwait(false);

            var allRealItems = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId == jobOrderId &&
                            realDnIds.Contains(i.DeliveryNoteId))
                .ToListAsync().ConfigureAwait(false);

            var deliveredMap = BuildDeliveredLookup(allRealItems);

            // Self-heal the master's OrderedQty from the authoritative Job
            // Order snapshot. The master is the template every new DN copies
            // OrderedQty from (DeliveryNoteMapper.FromMasterBalance), so if the
            // master ever stored the balance as OrderedQty, every child DN
            // inherited the wrong number (Ordered=64 instead of 76, Prev=0).
            // Rebuild the true per-line ordered map keyed exactly like
            // FromJobOrder, then correct the master in place below. If the JO
            // can't be read, leave stored values untouched.
            var orderedMap = await BuildJobOrderOrderedMapAsync(jobOrderId)
                .ConfigureAwait(false);

            // Recompute PREV/DELIVERED/BALANCE on the master's OWN tracked
            // children, IN PLACE. EF flags each as Modified and issues UPDATEs.
            //
            // BUG FIX (master DN showing empty rows): the previous version
            // mutated masterDn.Items in place AND THEN did
            //   RemoveRange(existingItems); ctx.DeliveryNotes.Update(masterDn);
            // where `existingItems` were the SAME tracked instances as
            // masterDn.Items. EF resolved that to "Deleted", so SaveChanges
            // wiped every master line and re-inserted nothing — leaving the
            // Master DN with zero rows on the next open. Never re-load, delete
            // and re-add tracked children to "refresh" them: mutate in place.
            foreach (var masterItem in masterDn.Items)
            {
                // Heal OrderedQty from the Job Order (source of truth) first.
                var orderedKey = SpecKey(masterItem);
                if (orderedMap.TryGetValue(orderedKey, out var trueOrdered)
                    && trueOrdered > 0
                    && trueOrdered != masterItem.OrderedQty)
                {
                    masterItem.OrderedQty = trueOrdered;
                }

                int totalDelivered = LookupDelivered(deliveredMap, masterItem);
                masterItem.PreviouslyDeliveredQty = totalDelivered;
                masterItem.DeliveredQty = 0;
                masterItem.BalanceQty = Math.Max(0,
                    masterItem.OrderedQty - totalDelivered);
            }

            masterDn.TotalOrderedQty =
                masterDn.Items.Sum(i => i.OrderedQty);
            masterDn.TotalDeliveredQty =
                masterDn.Items.Sum(i => i.PreviouslyDeliveredQty);
            masterDn.TotalBalanceQty =
                masterDn.Items.Sum(i => i.BalanceQty);
            masterDn.TotalPreviouslyDeliveredQty =
                masterDn.TotalDeliveredQty;
            masterDn.LastUpdatedFromDnId = triggeredByDnId;
            masterDn.UpdatedAt = DateTime.Now;
            masterDn.RowVersion++;

            // The master DN and its Items are already tracked (loaded with
            // Include above), so the change tracker persists the in-place
            // edits — no explicit Update()/RemoveRange() needed.
            await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
        }

        public async Task<string> BuildMasterDnIdAsync(string jobOrderId)
        {
            string core = jobOrderId.StartsWith(
                "JOB-", StringComparison.OrdinalIgnoreCase)
                ? jobOrderId.Substring(4)
                : jobOrderId;

            return await Task.FromResult($"MDN-{core}");
        }

        // ── Tax Invoice ↔ Delivery Note links ───────────────────────────

        /// <summary>Marks one confirmed DN as invoiced by a TI.</summary>
        public Task MarkAsInvoicedAsync(string deliveryNoteId, string taxInvoiceId)
        {
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteInvoiceLink, "mark a delivery note as invoiced");
            ReadOnlyGate.Demand("mark a delivery note as invoiced");
            if (string.IsNullOrWhiteSpace(deliveryNoteId))
                throw new ArgumentException("A Delivery Note id is required.", nameof(deliveryNoteId));
            return MarkMultipleAsInvoicedAsync(new[] { deliveryNoteId }, taxInvoiceId);
        }

        /// <summary>
        /// Adds the specified TI links without clearing other links owned by the
        /// invoice. All target rows are validated before any row is changed.
        /// </summary>
        public async Task MarkMultipleAsInvoicedAsync(
            IEnumerable<string> deliveryNoteIds,
            string taxInvoiceId)
        {
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteInvoiceLink, "mark delivery notes as invoiced");
            ReadOnlyGate.Demand("link delivery notes to a tax invoice");

            string invoiceKey = (taxInvoiceId ?? string.Empty).Trim();
            if (invoiceKey.Length == 0)
                throw new ArgumentException("A Tax Invoice id is required.", nameof(taxInvoiceId));

            var ids = (deliveryNoteIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (ids.Count == 0) return;

            await EnsureTaxInvoiceOwnerAsync(invoiceKey, requireLive: true)
                .ConfigureAwait(false);
            var keys = ids.Select(id => id.ToLowerInvariant()).ToList();
            using var ctx = new DeliveryNotesDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync().ConfigureAwait(false);

            var entities = await ctx.DeliveryNotes
                .Where(x => keys.Contains(x.DeliveryNoteId.ToLower()))
                .ToListAsync().ConfigureAwait(false);
            var entityMap = entities.ToDictionary(
                x => x.DeliveryNoteId, StringComparer.OrdinalIgnoreCase);
            var missing = ids.Where(id => !entityMap.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "Cannot link Tax Invoice " + invoiceKey + ": Delivery Note(s) not found: " +
                    string.Join(", ", missing) + ".");

            foreach (var entity in entities)
            {
                if (entity.IsVoid || entity.IsMasterDn
                    || !DeliveryNoteStatusValues.HasLeftTheBuilding(entity.Status))
                    throw new InvalidOperationException(
                        $"Delivery Note {entity.DeliveryNoteId} is void, internal, or not a confirmed delivery and cannot be invoiced.");

                bool hasExistingLink = entity.HasTaxInvoice
                    || !string.IsNullOrWhiteSpace(entity.TaxInvoiceId);
                if (hasExistingLink
                    && !string.Equals(entity.TaxInvoiceId, invoiceKey,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Delivery Note {entity.DeliveryNoteId} is already linked to Tax Invoice '{entity.TaxInvoiceId}'.");
                if (entity.HasTaxInvoice && string.IsNullOrWhiteSpace(entity.TaxInvoiceId))
                    throw new InvalidOperationException(
                        $"Delivery Note {entity.DeliveryNoteId} has an inconsistent invoice-link flag and must be corrected before invoicing.");
            }

            DateTime now = DateTime.Now;
            bool changed = false;
            foreach (var entity in entities)
            {
                if (entity.HasTaxInvoice
                    && string.Equals(entity.TaxInvoiceId, invoiceKey,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                entity.HasTaxInvoice = true;
                entity.TaxInvoiceId = invoiceKey;
                entity.UpdatedAt = now;
                entity.RowVersion++;
                changed = true;
            }

            if (changed)
            {
                await ctx.SaveChangesAsync().ConfigureAwait(false);
                var verified = await ctx.DeliveryNotes.AsNoTracking()
                    .Where(x => keys.Contains(x.DeliveryNoteId.ToLower()))
                    .Select(x => new { x.DeliveryNoteId, x.HasTaxInvoice, x.TaxInvoiceId })
                    .ToListAsync().ConfigureAwait(false);
                if (verified.Count != ids.Count || verified.Any(x =>
                        !x.HasTaxInvoice
                        || !string.Equals(x.TaxInvoiceId, invoiceKey,
                            StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(
                        $"Tax Invoice {invoiceKey} was not linked to every referenced Delivery Note.");

                await EnsureTaxInvoiceOwnerAsync(invoiceKey, requireLive: true)
                    .ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
                AuditService.Log(
                    AuditSections.DeliveryNote, "Tax Invoice-DN links added",
                    $"Tax Invoice {invoiceKey} linked to {ids.Count} Delivery Note(s): {string.Join(", ", ids)}",
                    entityType: "TaxInvoice", entityId: invoiceKey);
                DocumentLifecycle.Raise();
            }
            else
            {
                await EnsureTaxInvoiceOwnerAsync(invoiceKey, requireLive: true)
                    .ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Replaces this invoice's link set inside deliverynotes.db. It clears
        /// only links currently owned by the same invoice, and validates all
        /// target DNs before changing any row. This transaction cannot include
        /// the separate taxinvoices.db commit; callers surface any partial-save
        /// error rather than claiming the cross-database operation succeeded.
        /// </summary>
        public async Task SynchronizeTaxInvoiceLinksAsync(
            string taxInvoiceId,
            IEnumerable<string> deliveryNoteIds)
        {
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteInvoiceLink, "synchronize Tax Invoice-Delivery Note links");
            ReadOnlyGate.Demand("synchronize Tax Invoice-Delivery Note links");

            string invoiceKey = (taxInvoiceId ?? string.Empty).Trim();
            if (invoiceKey.Length == 0)
                throw new ArgumentException("A Tax Invoice id is required.", nameof(taxInvoiceId));

            var ids = (deliveryNoteIds ?? Array.Empty<string>())
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            await EnsureTaxInvoiceOwnerAsync(invoiceKey, requireLive: ids.Count > 0)
                .ConfigureAwait(false);
            var keys = ids.Select(id => id.ToLowerInvariant()).ToList();
            string invoiceKeyLower = invoiceKey.ToLowerInvariant();

            using var ctx = new DeliveryNotesDbContext();
            await using var tx = await ctx.Database.BeginTransactionAsync().ConfigureAwait(false);

            var ownedLinks = await ctx.DeliveryNotes
                .Where(x => x.TaxInvoiceId.ToLower() == invoiceKeyLower)
                .ToListAsync().ConfigureAwait(false);
            var targets = ids.Count == 0
                ? new List<SavedDeliveryNote>()
                : await ctx.DeliveryNotes
                    .Where(x => keys.Contains(x.DeliveryNoteId.ToLower()))
                    .ToListAsync().ConfigureAwait(false);
            var targetMap = targets.ToDictionary(
                x => x.DeliveryNoteId, StringComparer.OrdinalIgnoreCase);
            var missing = ids.Where(id => !targetMap.ContainsKey(id)).ToList();
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "Cannot synchronize Tax Invoice " + invoiceKey + ": Delivery Note(s) not found: " +
                    string.Join(", ", missing) + ".");

            foreach (var entity in targets)
            {
                if (entity.IsVoid || entity.IsMasterDn
                    || !DeliveryNoteStatusValues.HasLeftTheBuilding(entity.Status))
                    throw new InvalidOperationException(
                        $"Delivery Note {entity.DeliveryNoteId} is void, internal, or not a confirmed delivery and cannot be invoiced.");

                bool hasExistingLink = entity.HasTaxInvoice
                    || !string.IsNullOrWhiteSpace(entity.TaxInvoiceId);
                if (hasExistingLink
                    && !string.Equals(entity.TaxInvoiceId, invoiceKey,
                        StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException(
                        $"Delivery Note {entity.DeliveryNoteId} is already linked to Tax Invoice '{entity.TaxInvoiceId}'.");
                if (entity.HasTaxInvoice && string.IsNullOrWhiteSpace(entity.TaxInvoiceId))
                    throw new InvalidOperationException(
                        $"Delivery Note {entity.DeliveryNoteId} has an inconsistent invoice-link flag and must be corrected before invoicing.");
            }

            var targetSet = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
            DateTime now = DateTime.Now;
            int cleared = 0;
            int added = 0;
            foreach (var entity in ownedLinks)
            {
                if (targetSet.Contains(entity.DeliveryNoteId)) continue;
                entity.HasTaxInvoice = false;
                entity.TaxInvoiceId = string.Empty;
                entity.UpdatedAt = now;
                entity.RowVersion++;
                cleared++;
            }

            foreach (var entity in targets)
            {
                if (entity.HasTaxInvoice
                    && string.Equals(entity.TaxInvoiceId, invoiceKey,
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                entity.HasTaxInvoice = true;
                entity.TaxInvoiceId = invoiceKey;
                entity.UpdatedAt = now;
                entity.RowVersion++;
                added++;
            }

            if (cleared > 0 || added > 0)
            {
                await ctx.SaveChangesAsync().ConfigureAwait(false);
                var verifiedTargets = await ctx.DeliveryNotes.AsNoTracking()
                    .Where(x => keys.Contains(x.DeliveryNoteId.ToLower()))
                    .Select(x => new { x.HasTaxInvoice, x.TaxInvoiceId })
                    .ToListAsync().ConfigureAwait(false);
                if (verifiedTargets.Count != ids.Count || verifiedTargets.Any(x =>
                        !x.HasTaxInvoice
                        || !string.Equals(x.TaxInvoiceId, invoiceKey,
                            StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException(
                        $"Tax Invoice {invoiceKey} link verification failed.");

                bool staleRemain = await ctx.DeliveryNotes.AsNoTracking()
                    .AnyAsync(x => x.TaxInvoiceId.ToLower() == invoiceKeyLower
                        && !keys.Contains(x.DeliveryNoteId.ToLower()))
                    .ConfigureAwait(false);
                if (staleRemain)
                    throw new InvalidOperationException(
                        $"Stale Delivery Note links remain for Tax Invoice {invoiceKey}.");

                await EnsureTaxInvoiceOwnerAsync(invoiceKey, requireLive: ids.Count > 0)
                    .ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
                AuditService.Log(
                    AuditSections.DeliveryNote, "Tax Invoice-DN links synchronized",
                    $"Tax Invoice {invoiceKey}: {added} link(s) assigned, {cleared} stale link(s) cleared.",
                    entityType: "TaxInvoice", entityId: invoiceKey);
                DocumentLifecycle.Raise();
            }
            else
            {
                await EnsureTaxInvoiceOwnerAsync(invoiceKey, requireLive: ids.Count > 0)
                    .ConfigureAwait(false);
                await tx.CommitAsync().ConfigureAwait(false);
            }
        }

        /// <summary>Clears every DN link currently owned by this TI.</summary>
        public Task ClearInvoicedByTaxInvoiceAsync(string taxInvoiceId) =>
            SynchronizeTaxInvoiceLinksAsync(taxInvoiceId, Array.Empty<string>());

        private static async Task EnsureTaxInvoiceOwnerAsync(
            string invoiceKey, bool requireLive)
        {
            if (App.TaxInvoices == null)
                throw new InvalidOperationException(
                    "Cannot validate Delivery Note links because the Tax Invoice repository is unavailable.");

            var invoice = await App.TaxInvoices
                .GetByInvoiceNumberAsync(invoiceKey).ConfigureAwait(false);
            if (invoice == null)
                throw new InvalidOperationException(
                    $"Cannot link Delivery Notes to missing Tax Invoice {invoiceKey}.");

            if (requireLive && (invoice.IsVoid || DocumentLifecyclePolicy.IsVoidTerminal(
                    StatusTransitions.Kind.TaxInvoice, invoice.Status)))
                throw new InvalidOperationException(
                    $"Cannot assign Delivery Notes to void or terminal Tax Invoice {invoiceKey}.");
        }

        /// <summary>Returns all DNs whose stored invoice id matches, including voided DNs.</summary>
        public async Task<List<DeliveryNoteListItem>> GetByTaxInvoiceIdAsync(string taxInvoiceId)
        {
            if (string.IsNullOrWhiteSpace(taxInvoiceId))
                return new List<DeliveryNoteListItem>();
            string key = taxInvoiceId.Trim().ToLowerInvariant();
            using var ctx = new DeliveryNotesDbContext();
            return await ctx.DeliveryNotes
                .Where(x => x.TaxInvoiceId.ToLower() == key)
                .AsNoTracking()
                .OrderBy(x => x.DeliveryNoteId)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        // ── List retrieval ───────────────────────────────────────────────

        public async Task<List<DeliveryNoteSearchRow>> SearchAsync(
            string term, int take)
        {
            if (!SearchPattern.IsUsable(term) || take <= 0)
                return new List<DeliveryNoteSearchRow>();

            string pattern = SearchPattern.Contains(term);

            using var ctx = new DeliveryNotesDbContext();

            return await ctx.DeliveryNotes
                .AsNoTracking()
                // Same row set as GetAllAsync: master notes are excluded, so a
                // search can never surface a note no list screen shows.
                .Where(x => !x.IsMasterDn)
                .Where(x =>
                    EF.Functions.Like(x.DeliveryNoteId ?? "", pattern, SearchPattern.EscapeChar) ||
                    EF.Functions.Like(x.CustomerName ?? "", pattern, SearchPattern.EscapeChar) ||
                    EF.Functions.Like(x.ProjectName ?? "", pattern, SearchPattern.EscapeChar) ||
                    EF.Functions.Like(x.JobOrderId ?? "", pattern, SearchPattern.EscapeChar))
                .OrderByDescending(x => x.CreatedAt)
                .Take(take)
                .Select(x => new DeliveryNoteSearchRow
                {
                    DeliveryNoteId = x.DeliveryNoteId,
                    CustomerName   = x.CustomerName,
                    ProjectName    = x.ProjectName,
                    JobOrderId     = x.JobOrderId,
                    Status         = x.Status
                })
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<DeliveryNoteListItem>> GetAllAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            // PERF: SQL-level projection so the FullSnapshotJson blob column
            // is never read for a list query.
            return await ctx.DeliveryNotes
                .Where(x => !x.IsMasterDn)
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<(List<DeliveryNoteListItem> Items, int TotalCount)> GetPagedAsync(
            int page, int pageSize, string? search = null)
        {
            using var ctx = new DeliveryNotesDbContext();

            var query = ctx.DeliveryNotes
                .Where(x => !x.IsMasterDn)
                .AsNoTracking();

            // Apply search filter if provided
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim();
                query = query.Where(x =>
                    (x.DeliveryNoteId != null && x.DeliveryNoteId.Contains(term)) ||
                    (x.JobOrderId != null && x.JobOrderId.Contains(term)) ||
                    (x.ProformaId != null && x.ProformaId.Contains(term)) ||
                    (x.CustomerName != null && x.CustomerName.Contains(term)) ||
                    (x.ProjectName != null && x.ProjectName.Contains(term)) ||
                    (x.Status != null && x.Status.Contains(term)));
            }

            int totalCount = await query.CountAsync().ConfigureAwait(false);

            var items = await query
                .OrderByDescending(x => x.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);

            return (items, totalCount);
        }

        public async Task<List<DeliveryNoteListItem>> GetByJobOrderIdAsync(
            string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();
            // PERF: SQL-level projection so no snapshot blob is read.
            return await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId &&
                            !x.IsMasterDn)
                .AsNoTracking()
                .OrderBy(x => x.SequenceNumber)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<DeliveryNoteListItem>> GetByProformaIdAsync(
            string proformaId)
        {
            using var ctx = new DeliveryNotesDbContext();

            // PERF: SQL-level projection so no snapshot blob is read.
            //
            // Two things must match GetAllAsync exactly, because this replaces
            // a GetAllAsync() + in-memory filter and the callers must see the
            // same rows:
            //   1. the !IsMasterDn exclusion
            //   2. case-insensitive ProformaId matching — the call sites used
            //      StringComparison.OrdinalIgnoreCase, and SQLite `=` is
            //      case-sensitive, so both sides are lower-cased here.
            var key = (proformaId ?? string.Empty).ToLowerInvariant();

            return await ctx.DeliveryNotes
                .Where(x => !x.IsMasterDn &&
                            x.ProformaId.ToLower() == key)
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<DeliveryNoteListItem>> GetPreparedAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            // PERF: SQL-level projection so no snapshot blob is read.
            return await ctx.DeliveryNotes
                .Where(x => !x.IsMasterDn &&
                            (x.Status == DeliveryNoteStatusValues.Draft ||
                             x.Status == DeliveryNoteStatusValues.Dispatching))
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<DeliveryNoteListItem>> GetDeliveredAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            // PERF: SQL-level projection so no snapshot blob is read.
            return await ctx.DeliveryNotes
                .Where(x => (x.Status == DeliveryNoteStatusValues.Delivered ||
                             x.Status == DeliveryNoteStatusValues.Signed ||
                             x.Status == DeliveryNoteStatusValues.Received) &&
                            !x.IsMasterDn)
                .AsNoTracking()
                .OrderByDescending(x => x.DeliveryDate)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<PendingQtySummary>> GetPendingQtyJobsAsync()
        {
            using var ctx = new DeliveryNotesDbContext();

            var realDnIds = await ctx.DeliveryNotes
                .Where(x => !x.IsMasterDn &&
                            x.DeliveryType != "Dummy")
                .Select(x => x.DeliveryNoteId)
                .ToListAsync().ConfigureAwait(false);

            var allItems = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId != null &&
                            realDnIds.Contains(i.DeliveryNoteId))
                .ToListAsync().ConfigureAwait(false);

            var grouped = allItems
                .GroupBy(i => i.JobOrderId)
                .ToList();

            // PERF (remove N+1): load the latest real DN per job order in ONE
            // query rather than running a FirstOrDefaultAsync per job order
            // inside the loop. SQLite numbers are small but a large backlog
            // should not hammer the DB with a query per job.
            var joIds = grouped.Select(g => g.Key!).Where(k => !string.IsNullOrWhiteSpace(k)).ToList();
            var latestByJo = new Dictionary<string, SavedDeliveryNote>(StringComparer.OrdinalIgnoreCase);

            if (joIds.Count > 0)
            {
                var latestList = await ctx.DeliveryNotes
                    .Where(x => joIds.Contains(x.JobOrderId) && !x.IsMasterDn)
                    .AsNoTracking()
                    .OrderByDescending(x => x.SequenceNumber)
                    .ToListAsync().ConfigureAwait(false);

                foreach (var dn in latestList)
                {
                    if (dn.JobOrderId == null) continue;
                    if (!latestByJo.ContainsKey(dn.JobOrderId))
                        latestByJo[dn.JobOrderId] = dn;
                }
            }

            var result = new List<PendingQtySummary>();

            foreach (var joGroup in grouped)
            {
                string joId = joGroup.Key!;
                if (string.IsNullOrWhiteSpace(joId)) continue;

                var itemGroups = joGroup
                    .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber });

                int totalOrdered = itemGroups.Sum(g => g.Max(i => i.OrderedQty));
                int totalDelivered = itemGroups.Sum(g => g.Sum(i => i.DeliveredQty));
                int totalBalance = Math.Max(0, totalOrdered - totalDelivered);

                if (totalBalance == 0) continue;

                latestByJo.TryGetValue(joId, out var latestDn);

                result.Add(new PendingQtySummary
                {
                    JobOrderId = joId,
                    CustomerName = latestDn?.CustomerName ?? "",
                    ProjectName = latestDn?.ProjectName ?? "",
                    TotalOrdered = totalOrdered,
                    TotalDelivered = totalDelivered,
                    TotalBalance = totalBalance,
                    LastDnId = latestDn?.DeliveryNoteId ?? ""
                });
            }

            return result.OrderByDescending(x => x.TotalBalance).ToList();
        }

        public async Task<List<DeliveryNoteListItem>> GetReworkAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            // PERF: SQL-level projection so no snapshot blob is read.
            return await ctx.DeliveryNotes
                .Where(x => x.DeliveryType == "Rework")
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        public async Task<List<DeliveryNoteListItem>> GetReturnedAsync()
        {
            using var ctx = new DeliveryNotesDbContext();
            // PERF: SQL-level projection so no snapshot blob is read.
            return await ctx.DeliveryNotes
                .Where(x => x.DeliveryType == "Return")
                .AsNoTracking()
                .OrderByDescending(x => x.CreatedAt)
                .Select(ToListItemProjection)
                .ToListAsync().ConfigureAwait(false);
        }

        // ── Delivery history ─────────────────────────────────────────────

        /// <summary>
        /// CHAT 19 INVOICED FIX: Each DeliveryHistoryEntry now carries
        /// HasTaxInvoice and TaxInvoiceId from the DN record.
        /// This allows the history sidebar to show invoiced badge
        /// per DN row without additional DB calls.
        /// </summary>
        public async Task<DeliveryHistorySummary> GetDeliveryHistoryAsync(
            string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();

            var allDns = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId &&
                            !x.IsMasterDn &&
                            x.DeliveryType != "Dummy")
                .OrderBy(x => x.SequenceNumber)
                .ToListAsync().ConfigureAwait(false);

            var realDnIds = allDns.Select(x => x.DeliveryNoteId).ToList();

            var items = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId == jobOrderId &&
                            realDnIds.Contains(i.DeliveryNoteId))
                .ToListAsync().ConfigureAwait(false);

            var itemGroups = items
                .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
                .ToList();

            int totalOrdered = itemGroups.Count > 0
                ? itemGroups.Sum(g => g.Max(i => i.OrderedQty))
                : 0;
            int totalDelivered = itemGroups.Count > 0
                ? itemGroups.Sum(g => g.Sum(i => i.DeliveredQty))
                : 0;
            int totalBalance = Math.Max(0, totalOrdered - totalDelivered);

            if (totalOrdered == 0)
            {
                var masterDn = await ctx.DeliveryNotes
                    .FirstOrDefaultAsync(x =>
                        x.JobOrderId == jobOrderId && x.IsMasterDn).ConfigureAwait(false);
                if (masterDn != null)
                {
                    totalOrdered = masterDn.TotalOrderedQty;
                    totalBalance = totalOrdered;
                }
            }

            var summary = new DeliveryHistorySummary
            {
                JobOrderId = jobOrderId,
                TotalOrdered = totalOrdered,
                TotalDelivered = totalDelivered,
                TotalBalance = totalBalance,
                TotalRework = items.Sum(i => i.ReworkQty),
                TotalReturned = items.Sum(i => i.ReturnedQty)
            };

            int runningPrevTotal = 0;

            foreach (var dn in allDns)
            {
                var dnItems = items
                    .Where(i => i.DeliveryNoteId == dn.DeliveryNoteId);
                int thisDnDelivered = dnItems.Sum(i => i.DeliveredQty);

                int cumulativePrev = runningPrevTotal;
                int cumulativeBalanceAfter = Math.Max(0,
                    totalOrdered - cumulativePrev - thisDnDelivered);

                summary.Entries.Add(new DeliveryHistoryEntry
                {
                    DeliveryNoteId = dn.DeliveryNoteId,
                    DeliveryType = dn.DeliveryType,
                    Status = dn.Status,
                    DeliveredQty = thisDnDelivered,
                    BalanceQty = dn.TotalBalanceQty,
                    DeliveryDate = dn.DeliveryDate,
                    DriverName = dn.DriverName,
                    SequenceNumber = dn.SequenceNumber,
                    OrderedQty = totalOrdered,
                    CumulativePrevQty = cumulativePrev,
                    CumulativeBalanceQty = cumulativeBalanceAfter,

                    // CHAT 19 INVOICED FIX: map invoice state per DN
                    // These drive the invoiced badge in the history sidebar
                    HasTaxInvoice = dn.HasTaxInvoice,
                    TaxInvoiceId = dn.TaxInvoiceId ?? ""
                });

                runningPrevTotal += thisDnDelivered;
            }

            return summary;
        }

        public async Task<int> GetPreviouslyDeliveredQtyAsync(
            string jobOrderId,
            int specGroupNumber,
            int serialNumber)
        {
            using var ctx = new DeliveryNotesDbContext();

            var siblingIds = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId
                         && !x.IsMasterDn
                         && x.DeliveryType != "Dummy"
                         && (x.Status == DeliveryNoteStatusValues.Dispatched ||
                             x.Status == DeliveryNoteStatusValues.Delivered ||
                             x.Status == DeliveryNoteStatusValues.Signed ||
                             x.Status == DeliveryNoteStatusValues.Received))
                .Select(x => x.DeliveryNoteId)
                .ToListAsync().ConfigureAwait(false);

            return await ctx.DeliveryNoteItems
                .Where(i => siblingIds.Contains(i.DeliveryNoteId)
                         && i.JobOrderId == jobOrderId
                         && i.SpecGroupNumber == specGroupNumber
                         && i.SerialNumber == serialNumber)
                .SumAsync(i => i.DeliveredQty).ConfigureAwait(false);
        }

        public async Task<Dictionary<string, int>> GetPreviouslyDeliveredMapAsync(
            string jobOrderId, string excludeDnId)
        {
            using var ctx = new DeliveryNotesDbContext();

            var siblingIds = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId
                         && x.DeliveryNoteId != excludeDnId
                         && !x.IsMasterDn
                         && x.DeliveryType != "Dummy"
                         && (x.Status == DeliveryNoteStatusValues.Dispatched ||
                             x.Status == DeliveryNoteStatusValues.Delivered ||
                             x.Status == DeliveryNoteStatusValues.Signed ||
                             x.Status == DeliveryNoteStatusValues.Received))
                .Select(x => x.DeliveryNoteId)
                .ToListAsync().ConfigureAwait(false);

            if (siblingIds.Count == 0)
                return new Dictionary<string, int>();

            var items = await ctx.DeliveryNoteItems
                .Where(i => siblingIds.Contains(i.DeliveryNoteId)
                         && i.JobOrderId == jobOrderId)
                .ToListAsync().ConfigureAwait(false);

            return BuildDeliveredLookup(items);
        }

        private static Dictionary<string, int> BuildDeliveredLookup(
            IEnumerable<DeliveryNoteItem> items)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in items)
            {
                if (item.DeliveredQty <= 0) continue;
                // Spec+serial only. Glass-dimension fallback attributed
                // identical panels to each other and zeroed remaining qty.
                Acc(map, SpecKey(item), item.DeliveredQty);
            }
            return map;
        }

        private static int LookupDelivered(
            Dictionary<string, int> map, DeliveryNoteItem item)
        {
            if (map.TryGetValue(SpecKey(item), out var bySpec) && bySpec > 0)
                return bySpec;
            return 0;
        }

        private static string SpecKey(DeliveryNoteItem i) =>
            $"{i.SpecGroupNumber}|{i.SerialNumber}";

        /// <summary>
        /// Authoritative per-line ordered quantities for a job order, keyed
        /// (SpecGroupNumber|SerialNumber) exactly like the DN mapper. Read
        /// from the Job Order snapshot — the ERP source of truth for ordered
        /// qty. Empty map (never throws) when the JO is missing/unreadable, so
        /// callers safely fall back to stored values.
        /// </summary>
        private static async Task<Dictionary<string, int>>
            BuildJobOrderOrderedMapAsync(string jobOrderId)
        {
            try
            {
                if (App.Jobs == null || string.IsNullOrWhiteSpace(jobOrderId))
                    return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                var jobOrder = await App.Jobs
                    .GetByJobOrderIdAsync(jobOrderId).ConfigureAwait(false);
                if (jobOrder == null)
                    return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

                var joSnapshot = ProGlassAutomation.Services.JobOrder.Mapper
                    .JobOrderMapper.DeserializeSnapshot(jobOrder);

                return ProGlassAutomation.Services.DeliveryNote.Mapper
                    .DeliveryNoteMapper.BuildOrderedQtyMap(joSnapshot);
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    "[DN] BuildJobOrderOrderedMap skipped: " + ex.Message);
                return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            }
        }

        private static void Acc(Dictionary<string, int> map, string key, int qty)
        {
            if (string.IsNullOrWhiteSpace(key)) return;
            map[key] = map.TryGetValue(key, out var n) ? n + qty : qty;
        }

        // ── SQM aggregation ──────────────────────────────────────────────

        public async Task<Dictionary<string, JobOrderSqmSummary>>
            GetSqmSummariesByJobOrderAsync(IEnumerable<string> jobOrderIds)
        {
            var idList = jobOrderIds.ToList();
            if (idList.Count == 0)
                return new Dictionary<string, JobOrderSqmSummary>();

            using var ctx = new DeliveryNotesDbContext();

            var realDnIds = await ctx.DeliveryNotes
                .Where(x => idList.Contains(x.JobOrderId) &&
                            !x.IsMasterDn &&
                            x.DeliveryType != "Dummy")
                .Select(x => x.DeliveryNoteId)
                .ToListAsync().ConfigureAwait(false);

            var items = await ctx.DeliveryNoteItems
                .Where(i => i.JobOrderId != null
                         && idList.Contains(i.JobOrderId)
                         && realDnIds.Contains(i.DeliveryNoteId))
                .ToListAsync().ConfigureAwait(false);

            var result = new Dictionary<string, JobOrderSqmSummary>();
            var joGroups = items.GroupBy(i => i.JobOrderId!);

            foreach (var joGroup in joGroups)
            {
                var uniqueItems = joGroup
                    .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber });

                decimal orderedSqm = 0m;
                decimal deliveredSqm = 0m;

                foreach (var itemGroup in uniqueItems)
                {
                    var first = itemGroup.First();
                    decimal panelSqm =
                        SqmCalculator.CalculatePanel(first.W1, first.H1);
                    int maxOrdered = itemGroup.Max(i => i.OrderedQty);
                    int totalDelivered = itemGroup.Sum(i => i.DeliveredQty);
                    orderedSqm += panelSqm * maxOrdered;
                    deliveredSqm += panelSqm * totalDelivered;
                }

                result[joGroup.Key!] = new JobOrderSqmSummary
                {
                    OrderedSqm = Math.Round(orderedSqm, 3),
                    DeliveredSqm = Math.Round(deliveredSqm, 3),
                    BalanceSqm = Math.Round(
                        Math.Max(0m, orderedSqm - deliveredSqm), 3)
                };
            }

            return result;
        }

        // ── Numbering ────────────────────────────────────────────────────

        public async Task<int> GetNextSequenceAsync(string jobOrderId)
        {
            using var ctx = new DeliveryNotesDbContext();
            var max = await ctx.DeliveryNotes
                .Where(x => x.JobOrderId == jobOrderId && !x.IsMasterDn)
                .MaxAsync(x => (int?)x.SequenceNumber) ?? 0;
            return max + 1;
        }

        public Task<string> BuildDeliveryNoteIdAsync(
            string jobOrderId, int sequence) =>
            Task.FromResult(BuildDeliveryNoteId(jobOrderId, sequence));

        private static string BuildDeliveryNoteId(string jobOrderId, int sequence)
        {
            string core = jobOrderId.StartsWith(
                "JOB-", StringComparison.OrdinalIgnoreCase)
                ? jobOrderId.Substring(4)
                : jobOrderId;
            return $"DN-{core}-{sequence:D2}";
        }

        // ── CRUD ─────────────────────────────────────────────────────────

        public async Task<SavedDeliveryNote> InsertAsync(SavedDeliveryNote entity)
        {
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "create a delivery note");
            ReadOnlyGate.Demand("create a delivery note");

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));
            if (entity.IsMasterDn)
                throw new InvalidOperationException(
                    "Master Delivery Notes must be created through CreateMasterDnAsync.");
            if (entity.IsVoid
                || DocumentLifecyclePolicy.HasVoidMetadata(
                    entity.VoidedAtUtc, entity.VoidedBy, entity.VoidReason)
                || !DocumentLifecyclePolicy.CanEditBody(
                    StatusTransitions.Kind.DeliveryNote, entity.Status))
                throw new InvalidOperationException(
                    "A new Delivery Note must be a live, editable draft/dispatching document.");
            if (string.IsNullOrWhiteSpace(entity.JobOrderId))
                throw new InvalidOperationException(
                    "A Job Order reference is required to allocate a Delivery Note sequence.");

            const int maxAttempts = 5;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await using var ctx = new DeliveryNotesDbContext();
                    // Microsoft.Data.Sqlite starts this SERIALIZABLE transaction
                    // with BEGIN IMMEDIATE (non-deferred), acquiring the single
                    // writer reservation before MAX(SequenceNumber) is read. All
                    // app instances therefore allocate against a serialized view.
                    await using var transaction = await ctx.Database
                        .BeginTransactionAsync(System.Data.IsolationLevel.Serializable)
                        .ConfigureAwait(false);
                    SchemaMigrationSql.RequireIndex(
                        (Microsoft.Data.Sqlite.SqliteConnection)ctx.Database.GetDbConnection(),
                        "IX_DeliveryNotes_JobOrderId_SequenceNumber_Unique",
                        "DeliveryNotes",
                        unique: true,
                        expectedColumns: new[] { "JobOrderId", "SequenceNumber" });

                    int highest = await ctx.DeliveryNotes
                        .Where(x => x.JobOrderId == entity.JobOrderId)
                        .MaxAsync(x => (int?)x.SequenceNumber)
                        .ConfigureAwait(false) ?? 0;
                    int sequence = checked(highest + 1);
                    entity.SequenceNumber = sequence;
                    entity.DeliveryNoteId = BuildDeliveryNoteId(entity.JobOrderId, sequence);
                    PrepareAllocatedIdentity(entity);

                    DateTime now = DateTime.Now;
                    entity.Id = 0;
                    entity.CreatedAt = now;
                    entity.UpdatedAt = now;
                    entity.RowVersion = 0;
                    ctx.DeliveryNotes.Add(entity);
                    await ctx.SaveChangesAsync().ConfigureAwait(false);
                    await transaction.CommitAsync().ConfigureAwait(false);

                    DocumentLifecycle.Raise();
                    return entity;
                }
                catch (DbUpdateException ex) when (SqliteErrorClassifier.IsUnique(ex))
                {
                    if (attempt == maxAttempts)
                        throw new InvalidOperationException(
                            $"A unique Delivery Note key conflict persisted while allocating for '{entity.JobOrderId}'. " +
                            "No existing records were renumbered.", ex);
                    // A legacy/older client may still race this allocator. The
                    // unique composite index is authoritative; start a fresh
                    // transaction/context and recalculate from committed rows.
                    ResetGeneratedKeys(entity);
                    await Task.Delay(20 * attempt + Random.Shared.Next(0, 30))
                        .ConfigureAwait(false);
                }
                catch (Exception ex) when (SqliteErrorClassifier.IsTransient(ex))
                {
                    if (attempt == maxAttempts) throw;
                    ResetGeneratedKeys(entity);
                    await Task.Delay(25 * attempt + Random.Shared.Next(0, 35))
                        .ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException(
                $"Could not allocate a unique Delivery Note number for Job Order '{entity.JobOrderId}' " +
                $"after {maxAttempts} serialized attempts. No renumbering of existing records was performed.");
        }

        /// <summary>
        /// Rewrites every denormalized copy of the allocated key before insert:
        /// the header, normalized items, optional attachment references, and
        /// JSON snapshot. Snapshot failures abort the transaction.
        /// </summary>
        private static void PrepareAllocatedIdentity(SavedDeliveryNote entity)
        {
            foreach (var item in entity.Items ?? new List<DeliveryNoteItem>())
            {
                item.Id = 0;
                item.DeliveryNoteId = entity.DeliveryNoteId;
                item.JobOrderId = entity.JobOrderId;
                item.DeliveryNote = entity;
            }

            foreach (var attachment in entity.Attachments ?? new List<DeliveryNoteAttachment>())
            {
                attachment.Id = 0;
                attachment.DeliveryNoteId = entity.DeliveryNoteId;
                attachment.DeliveryNote = entity;
            }

            var snapshot = Mapper.DeliveryNoteMapper.DeserializeSnapshot(entity)
                ?? throw new InvalidOperationException(
                    "The Delivery Note snapshot is missing or unreadable; its allocated number cannot be safely propagated.");
            Mapper.DeliveryNoteMapper.RefreshSnapshot(entity, snapshot.JobOrderRef);
        }

        private static void ResetGeneratedKeys(SavedDeliveryNote entity)
        {
            entity.Id = 0;
            foreach (var item in entity.Items ?? new List<DeliveryNoteItem>())
                item.Id = 0;
            foreach (var attachment in entity.Attachments ?? new List<DeliveryNoteAttachment>())
                attachment.Id = 0;
        }

        /// <summary>
        /// Updates a delivery note using the version captured by the caller.
        /// EF's concurrency-token predicate closes the check-then-write race.
        /// </summary>
        public async Task UpdateAsync(SavedDeliveryNote entity, int expectedRowVersion)
        {
            // A-1: service-layer authorization. The UI also checks,
            // but this is the boundary that cannot be bypassed.
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "update a delivery note");
            ReadOnlyGate.Demand("update a delivery note");
            if (entity == null) throw new ArgumentNullException(nameof(entity));

            using var ctx = new DeliveryNotesDbContext();
            var stored = await ctx.DeliveryNotes
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == entity.Id)
                .ConfigureAwait(false);
            if (stored == null)
                throw new KeyNotFoundException($"Delivery Note {entity.Id} was not found.");

            // ERP hardening (Phase 5): optimistic-concurrency check + counter.
            if (stored.RowVersion != expectedRowVersion)
                throw new Services.Database.ConcurrencyConflictException(
                    "Delivery Note", stored.DeliveryNoteId,
                    expectedRowVersion, stored.RowVersion);

            if (stored.IsVoid
                || DocumentLifecyclePolicy.IsVoidTerminal(
                    StatusTransitions.Kind.DeliveryNote, stored.Status))
                throw new InvalidOperationException(
                    $"Voided Delivery Note {stored.DeliveryNoteId} is retained and cannot be edited.");
            if (stored.IsMasterDn)
                throw new InvalidOperationException("Master Delivery Notes are system-managed and cannot be edited.");
            if (!DocumentLifecyclePolicy.CanEditBody(
                    StatusTransitions.Kind.DeliveryNote, stored.Status))
                throw new InvalidOperationException(
                    $"Delivery Note {stored.DeliveryNoteId} is body-locked in status '{stored.Status}'. Use a valid status-only transition or void it.");
            if (stored.HasTaxInvoice || !string.IsNullOrWhiteSpace(stored.TaxInvoiceId))
                throw new InvalidOperationException(
                    $"Delivery Note {stored.DeliveryNoteId} is linked to a Tax Invoice and its body is retained.");

            if (!string.Equals(stored.DeliveryNoteId, entity.DeliveryNoteId, StringComparison.Ordinal)
                || !string.Equals(stored.JobOrderId, entity.JobOrderId, StringComparison.Ordinal)
                || !string.Equals(stored.ProformaId, entity.ProformaId, StringComparison.Ordinal)
                || stored.SequenceNumber != entity.SequenceNumber
                || stored.IsMasterDn != entity.IsMasterDn
                || !string.Equals(stored.DeliveryType, entity.DeliveryType, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "Delivery Note identity, source links, sequence, and type are immutable after creation.");

            var transition = StatusTransitions.Validate(
                StatusTransitions.Kind.DeliveryNote, stored.Status, entity.Status);
            if (!transition.IsValid)
                throw new InvalidOperationException(
                    transition.Reason ?? "Invalid Delivery Note status transition.");

            // Copy only mutable business fields into the tracked row. Void,
            // invoice-link, attachment, identity, and creation metadata cannot
            // be overwritten by a detached UI object.
            stored.Status = entity.Status;
            stored.IsFoc = entity.IsFoc;
            stored.DriverName = entity.DriverName;
            stored.VehicleNumber = entity.VehicleNumber;
            stored.ReceiverName = entity.ReceiverName;
            stored.DeliveryDate = entity.DeliveryDate;
            stored.DeliveryTime = entity.DeliveryTime;
            stored.Notes = entity.Notes;
            stored.TotalOrderedQty = entity.TotalOrderedQty;
            stored.TotalDeliveredQty = entity.TotalDeliveredQty;
            stored.TotalBalanceQty = entity.TotalBalanceQty;
            stored.TotalPreviouslyDeliveredQty = entity.TotalPreviouslyDeliveredQty;

            ctx.DeliveryNoteItems.RemoveRange(stored.Items);
            stored.Items.Clear();
            foreach (var item in entity.Items ?? new List<DeliveryNoteItem>())
            {
                item.Id = 0;
                item.DeliveryNoteId = stored.DeliveryNoteId;
                item.JobOrderId = stored.JobOrderId;
                item.DeliveryNote = stored;
                ctx.Entry(item).State = EntityState.Added;
            }

            try
            {
                var snapshot = Mapper.DeliveryNoteMapper.DeserializeSnapshot(stored);
                Mapper.DeliveryNoteMapper.RefreshSnapshot(stored, snapshot?.JobOrderRef);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "The Delivery Note snapshot could not be safely refreshed; no update was saved.", ex);
            }

            stored.RowVersion++;
            stored.UpdatedAt = DateTime.Now;
            try
            {
                await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            }
            catch (DbUpdateConcurrencyException)
            {
                int actual = await GetCurrentRowVersionAsync(ctx, stored.Id).ConfigureAwait(false);
                throw new Services.Database.ConcurrencyConflictException(
                    "Delivery Note", stored.DeliveryNoteId, expectedRowVersion, actual);
            }
            entity.RowVersion = stored.RowVersion;
            entity.UpdatedAt = stored.UpdatedAt;
            DocumentLifecycle.Raise();
        }

        private static async Task<int> GetCurrentRowVersionAsync(
            DeliveryNotesDbContext context, int id)
        {
            return await context.DeliveryNotes.AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => (int?)x.RowVersion)
                .FirstOrDefaultAsync().ConfigureAwait(false) ?? -1;
        }

        public async Task<SavedDeliveryNote?> UpdateStatusAsync(
            int id, string newStatus, int expectedRowVersion)
        {
            OperationGuard.Demand(
                AppPermissions.DeliveryNoteEdit, "change a delivery note status");
            ReadOnlyGate.Demand("change a delivery note status");
            if (string.IsNullOrWhiteSpace(newStatus))
                throw new ArgumentException("A target status is required.", nameof(newStatus));

            using var ctx = new DeliveryNotesDbContext();
            var stored = await ctx.DeliveryNotes
                .Include(x => x.Items)
                .Include(x => x.Attachments)
                .FirstOrDefaultAsync(x => x.Id == id)
                .ConfigureAwait(false);
            if (stored == null) return null;
            if (stored.RowVersion != expectedRowVersion)
                throw new Services.Database.ConcurrencyConflictException(
                    "Delivery Note", stored.DeliveryNoteId, expectedRowVersion, stored.RowVersion);
            if (stored.IsVoid || DocumentLifecyclePolicy.IsVoidTerminal(
                    StatusTransitions.Kind.DeliveryNote, stored.Status))
                throw new InvalidOperationException(
                    $"Voided Delivery Note {stored.DeliveryNoteId} cannot change status.");
            if (stored.IsMasterDn)
                throw new InvalidOperationException("Master Delivery Notes are system-managed.");

            var transition = StatusTransitions.Validate(
                StatusTransitions.Kind.DeliveryNote, stored.Status, newStatus);
            if (!transition.IsValid)
                throw new InvalidOperationException(
                    transition.Reason ?? "Invalid Delivery Note status transition.");

            string oldStatus = stored.Status;
            stored.Status = newStatus.Trim();
            var snapshot = Mapper.DeliveryNoteMapper.DeserializeSnapshot(stored);
            Mapper.DeliveryNoteMapper.RefreshSnapshot(stored, snapshot?.JobOrderRef);
            stored.UpdatedAt = DateTime.Now;
            stored.RowVersion++;
            try
            {
                await SqliteResilience.SaveWithRetryAsync(_ => ctx.SaveChangesAsync());
            }
            catch (DbUpdateConcurrencyException)
            {
                int actual = await GetCurrentRowVersionAsync(ctx, stored.Id).ConfigureAwait(false);
                throw new Services.Database.ConcurrencyConflictException(
                    "Delivery Note", stored.DeliveryNoteId, expectedRowVersion, actual);
            }

            AuditService.Log(
                AuditSections.DeliveryNote, "Delivery Note status changed",
                $"{stored.DeliveryNoteId}: {oldStatus} → {stored.Status}",
                entityType: nameof(SavedDeliveryNote), entityId: stored.DeliveryNoteId);
            DocumentLifecycle.Raise();
            return stored;
        }
    }
}
