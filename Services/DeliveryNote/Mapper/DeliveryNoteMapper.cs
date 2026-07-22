using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace ProGlassAutomation.Services.DeliveryNote.Mapper
{
    /// <summary>
    /// Converts Job Order entities into Delivery Note entities.
    ///
    /// Three conversion paths:
    ///
    /// Path A — FromJobOrder(SavedJobOrder, snapshot, dnId, seq):
    ///   Creates a new Draft DN from a Job Order.
    ///   All items populated with OrderedQty from JO spec rows.
    ///   DeliveredQty = 0, PreviouslyDeliveredQty = 0, BalanceQty = OrderedQty.
    ///   User edits DeliveredQty in the UI before confirming.
    ///
    /// Path B — FromBalance(SavedDeliveryNote, dnId, seq):
    ///   Creates a Dummy DN from the balance of a prior DN.
    ///   Items where BalanceQty > 0 are carried forward.
    ///   DeliveredQty = 0, PreviouslyDeliveredQty updated, BalanceQty = balance.
    ///   Items fully delivered are excluded.
    ///
    /// Path C — Serialize/Deserialize snapshot (JSON round-trip).
    /// </summary>
    public static class DeliveryNoteMapper
    {
        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false,
            PropertyNameCaseInsensitive = true
        };

        // ════════════════════════════════════════════════════════════════
        //  PATH A — Job Order → New Draft Delivery Note
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a new Draft DN entity from a Job Order.
        /// All quantities start at 0 delivered — user sets DeliveredQty
        /// in the Create Delivery UI before saving.
        /// </summary>
        public static SavedDeliveryNote FromJobOrder(
            SavedJobOrder jobOrder,
            JobOrderSnapshot joSnapshot,
            string deliveryNoteId,
            int sequenceNumber)
        {
            if (jobOrder == null) throw new ArgumentNullException(nameof(jobOrder));
            if (joSnapshot == null) throw new ArgumentNullException(nameof(joSnapshot));

            var pi = joSnapshot.EmbeddedInvoice;

            var now = DateTime.Now;

            var entity = new SavedDeliveryNote
            {
                DeliveryNoteId = deliveryNoteId,
                JobOrderId = jobOrder.JobOrderId,
                ProformaId = jobOrder.SourceProformaId,
                SequenceNumber = sequenceNumber,
                CustomerName = jobOrder.CustomerName,
                ProjectName = jobOrder.ProjectName,
                Status = "Draft",
                DeliveryType = "Original",
                IsFoc = false,
                DriverName = "",
                VehicleNumber = "",
                ReceiverName = "",
                DeliveryDate = now,
                DeliveryTime = now.ToString("HH:mm"),
                Notes = "",
                TotalOrderedQty = 0,
                TotalDeliveredQty = 0,
                TotalBalanceQty = 0
            };

            // ── Build items from JO spec groups ──────────────────────────
            var items = new List<DeliveryNoteItem>();

            if (pi?.SpecGroups != null)
            {
                foreach (var group in pi.SpecGroups)
                {
                    if (group.ItemRows == null) continue;

                    foreach (var row in group.ItemRows)
                    {
                        var item = new DeliveryNoteItem
                        {
                            DeliveryNoteId = deliveryNoteId,
                            JobOrderId = jobOrder.JobOrderId,
                            SpecGroupNumber = group.GroupNumber,
                            SpecGroupName = !string.IsNullOrWhiteSpace(group.SelectedSpecName)
                                                         ? group.SelectedSpecName
                                                         : (group.GroupName ?? $"Spec {group.GroupNumber}"),
                            SerialNumber = row.SerialNumber,
                            GlassRef = row.GlassRef ?? "",
                            W1 = row.W1,
                            H1 = row.H1,
                            W2 = row.W2,
                            H2 = row.H2,
                            OrderedQty = row.Qty,
                            PreviouslyDeliveredQty = 0,
                            DeliveredQty = 0,
                            BalanceQty = row.Qty,  // all balance at start
                            ReworkQty = 0,
                            ReturnedQty = 0,
                            Notes = ""
                        };

                        items.Add(item);
                        entity.TotalOrderedQty += row.Qty;
                        entity.TotalBalanceQty += row.Qty;
                    }
                }
            }

            entity.Items = items;

            // ── Build snapshot ────────────────────────────────────────────
            var snapshot = BuildSnapshot(entity, joSnapshot);
            entity.FullSnapshotJson = JsonSerializer.Serialize(snapshot, _jsonOptions);

            return entity;
        }

        // ════════════════════════════════════════════════════════════════
        //  PATH B — Prior DN Balance → New Dummy Delivery Note
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Creates a Dummy DN from the balance quantities of a prior DN.
        /// Only items with BalanceQty > 0 are carried forward.
        /// PreviouslyDeliveredQty updated to reflect all prior deliveries.
        /// DeliveredQty starts at 0 — user edits before confirming.
        /// </summary>
        public static SavedDeliveryNote FromBalance(
            SavedDeliveryNote priorDn,
            string newDeliveryNoteId,
            int newSequenceNumber)
        {
            if (priorDn == null)
                throw new ArgumentNullException(nameof(priorDn));

            var now = DateTime.Now;

            var entity = new SavedDeliveryNote
            {
                DeliveryNoteId = newDeliveryNoteId,
                JobOrderId = priorDn.JobOrderId,
                ProformaId = priorDn.ProformaId,
                SequenceNumber = newSequenceNumber,
                CustomerName = priorDn.CustomerName,
                ProjectName = priorDn.ProjectName,
                Status = "Draft",
                DeliveryType = "Dummy",
                IsFoc = false,
                DriverName = "",
                VehicleNumber = "",
                ReceiverName = "",
                DeliveryDate = now,
                DeliveryTime = now.ToString("HH:mm"),
                Notes = "",
                TotalOrderedQty = 0,
                TotalDeliveredQty = 0,
                TotalBalanceQty = 0
            };

            // ── Carry forward only items with balance > 0 ─────────────────
            var items = new List<DeliveryNoteItem>();

            foreach (var priorItem in priorDn.Items
                .Where(i => i.BalanceQty > 0))
            {
                // PreviouslyDelivered = all prior delivered (including this DN)
                int totalPreviouslyDelivered =
                    priorItem.PreviouslyDeliveredQty + priorItem.DeliveredQty;

                var item = new DeliveryNoteItem
                {
                    DeliveryNoteId = newDeliveryNoteId,
                    JobOrderId = priorDn.JobOrderId,
                    SpecGroupNumber = priorItem.SpecGroupNumber,
                    SpecGroupName = priorItem.SpecGroupName,
                    SerialNumber = priorItem.SerialNumber,
                    GlassRef = priorItem.GlassRef,
                    W1 = priorItem.W1,
                    H1 = priorItem.H1,
                    W2 = priorItem.W2,
                    H2 = priorItem.H2,
                    OrderedQty = priorItem.OrderedQty,
                    PreviouslyDeliveredQty = totalPreviouslyDelivered,
                    DeliveredQty = 0,
                    BalanceQty = priorItem.BalanceQty,
                    ReworkQty = 0,
                    ReturnedQty = 0,
                    Notes = ""
                };

                items.Add(item);
                entity.TotalOrderedQty += priorItem.OrderedQty;
                entity.TotalBalanceQty += priorItem.BalanceQty;
            }

            entity.Items = items;

            // ── Deserialize prior snapshot to get JO ref context ──────────
            var priorSnapshot = DeserializeSnapshot(priorDn);
            var snapshot = BuildSnapshot(entity, null, priorSnapshot?.JobOrderRef);
            entity.FullSnapshotJson = JsonSerializer.Serialize(snapshot, _jsonOptions);

            return entity;
        }

        // ════════════════════════════════════════════════════════════════
        //  PATH C — Serialize / Deserialize
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Deserializes the JSON snapshot from a saved entity.
        /// Returns null if JSON is empty or malformed.
        /// </summary>
        public static DeliveryNoteSnapshot? DeserializeSnapshot(
            SavedDeliveryNote entity)
        {
            if (entity == null
                || string.IsNullOrWhiteSpace(entity.FullSnapshotJson)
                || entity.FullSnapshotJson == "{}")
                return null;

            try
            {
                return JsonSerializer.Deserialize<DeliveryNoteSnapshot>(
                    entity.FullSnapshotJson, _jsonOptions);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Serializes the current entity state into FullSnapshotJson.
        /// Call before InsertAsync / UpdateAsync.
        /// </summary>
        public static void RefreshSnapshot(
            SavedDeliveryNote entity,
            DnJobOrderRefSnapshot? joRef = null)
        {
            var snapshot = BuildSnapshot(entity, null, joRef);
            entity.FullSnapshotJson = JsonSerializer.Serialize(
                snapshot, _jsonOptions);
        }

        // ════════════════════════════════════════════════════════════════
        //  PRIVATE — snapshot builder
        // ════════════════════════════════════════════════════════════════

        private static DeliveryNoteSnapshot BuildSnapshot(
            SavedDeliveryNote entity,
            JobOrderSnapshot? joSnapshot = null,
            DnJobOrderRefSnapshot? existingRef = null)
        {
            // Build JO reference block from JO snapshot if available,
            // otherwise reuse existing ref from prior snapshot
            var joRef = existingRef ?? new DnJobOrderRefSnapshot();

            if (joSnapshot != null)
            {
                var pi = joSnapshot.EmbeddedInvoice;

                joRef = new DnJobOrderRefSnapshot
                {
                    JobOrderId = joSnapshot.JobOrderId,
                    ProformaId = joSnapshot.SourceProformaId,
                    CustomerName = pi?.Client?.CustomerName ?? "",
                    ProjectName = pi?.Project?.ProjectIdentifierName ?? "",
                    CompanyName = pi?.Company?.CompanyName ?? "",
                    CompanyTrn = pi?.Company?.CompanyTrn ?? "",
                    CompanyLocation = pi?.Company?.CompanyLocation ?? "",
                    SpecGroups = pi?.SpecGroups?.Select(g => new DnJoSpecGroupSnapshot
                    {
                        GroupNumber = g.GroupNumber,
                        GroupName = !string.IsNullOrWhiteSpace(g.SelectedSpecName)
                                          ? g.SelectedSpecName
                                          : (g.GroupName ?? $"Spec {g.GroupNumber}"),
                        GlassType = g.SelectedGlassType ?? "",
                        SpecName = g.SelectedSpecName ?? "",
                        TotalQty = g.ItemRows?.Sum(r => r.Qty) ?? 0,
                        TotalArea = g.ItemRows?.Sum(r =>
                            (decimal)Math.Max(
                                Math.Round((double)r.W1 * r.H1 / 1_000_000d, 3),
                                0.5d) * r.Qty) ?? 0m
                    }).ToList() ?? new()
                };
            }

            return new DeliveryNoteSnapshot
            {
                Header = new DnHeaderSnapshot
                {
                    DeliveryNoteId = entity.DeliveryNoteId,
                    JobOrderId = entity.JobOrderId,
                    ProformaId = entity.ProformaId,
                    SequenceNumber = entity.SequenceNumber,
                    Status = entity.Status,
                    DeliveryType = entity.DeliveryType,
                    IsFoc = entity.IsFoc,
                    DriverName = entity.DriverName,
                    VehicleNumber = entity.VehicleNumber,
                    ReceiverName = entity.ReceiverName,
                    DeliveryDate = entity.DeliveryDate,
                    DeliveryTime = entity.DeliveryTime,
                    Notes = entity.Notes
                },
                JobOrderRef = joRef,
                Items = entity.Items?.Select(i => new DnItemSnapshot
                {
                    SpecGroupNumber = i.SpecGroupNumber,
                    SpecGroupName = i.SpecGroupName,
                    SerialNumber = i.SerialNumber,
                    GlassRef = i.GlassRef,
                    W1 = i.W1,
                    H1 = i.H1,
                    W2 = i.W2,
                    H2 = i.H2,
                    OrderedQty = i.OrderedQty,
                    PreviouslyDeliveredQty = i.PreviouslyDeliveredQty,
                    DeliveredQty = i.DeliveredQty,
                    BalanceQty = i.BalanceQty,
                    ReworkQty = i.ReworkQty,
                    ReturnedQty = i.ReturnedQty,
                    Notes = i.Notes
                }).ToList() ?? new(),
                Attachments = entity.Attachments?.Select(a => new DnAttachmentSnapshot
                {
                    AttachmentType = a.AttachmentType,
                    FileName = a.FileName,
                    FilePath = a.FilePath,
                    FileSize = a.FileSize,
                    Description = a.Description,
                    UploadedAt = a.UploadedAt
                }).ToList() ?? new()
            };
        }
    }
}