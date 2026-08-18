using ProGlassAutomation.Models.DeliveryNote.SavedDeliveryNote;
using ProGlassAutomation.Models.JobOrder.SavedJobOrder;
using ProGlassAutomation.Utilities;
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
    ///
    /// Path B — FromBalance(SavedDeliveryNote, dnId, seq):
    ///   Creates a Dummy DN from the balance of a prior DN.
    ///
    /// Path C — Serialize/Deserialize snapshot (JSON round-trip).
    ///
    /// BUG-22: TotalArea now uses SqmCalculator.CalculatePanel()
    ///         instead of inline formula — single source of truth.
    /// BUG-23: FromBalance TotalOrderedQty uses MAX per unique item
    ///         not SUM — fixes Gotcha #40 double-count on splits.
    /// BUG-24: Empty ItemRows list guard added in FromJobOrder.
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
                Status = "Dispatching",
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
                TotalBalanceQty = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            // ── Build items from JO spec groups ──────────────────────────
            var items = new List<DeliveryNoteItem>();

            if (pi?.SpecGroups != null)
            {
                foreach (var group in pi.SpecGroups)
                {
                    // BUG-24: guard against null AND empty ItemRows
                    if (group.ItemRows == null || group.ItemRows.Count == 0)
                        continue;

                    foreach (var row in group.ItemRows)
                    {
                        var item = new DeliveryNoteItem
                        {
                            DeliveryNoteId = deliveryNoteId,
                            JobOrderId = jobOrder.JobOrderId,
                            SpecGroupNumber = group.GroupNumber,
                            SpecGroupName = !string.IsNullOrWhiteSpace(
                                                         group.SelectedSpecName)
                                                         ? group.SelectedSpecName
                                                         : (group.GroupName
                                                            ?? $"Spec {group.GroupNumber}"),
                            SerialNumber = row.SerialNumber,
                            GlassRef = row.GlassRef ?? "",
                            W1 = row.W1,
                            H1 = row.H1,
                            W2 = row.W2,
                            H2 = row.H2,
                            OrderedQty = row.Qty,
                            PreviouslyDeliveredQty = 0,
                            DeliveredQty = 0,
                            BalanceQty = row.Qty,
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
            entity.FullSnapshotJson = JsonSerializer.Serialize(
                snapshot, _jsonOptions);

            return entity;
        }

        // ════════════════════════════════════════════════════════════════
        //  PATH B — Prior DN Balance → New Dummy Delivery Note
        // ════════════════════════════════════════════════════════════════

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
                Status = "Dispatching",
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
                TotalBalanceQty = 0,
                CreatedAt = now,
                UpdatedAt = now
            };

            // ── Carry forward only items with balance > 0 ─────────────────
            var items = new List<DeliveryNoteItem>();

            // BUG-23: Group by unique item key to get MAX OrderedQty
            // OrderedQty is REPLICATED across splits — use MAX not SUM
            var uniqueItems = priorDn.Items
                .Where(i => i.BalanceQty > 0)
                .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
                .ToList();

            foreach (var itemGroup in uniqueItems)
            {
                // MAX OrderedQty across splits (Gotcha #40 fix — BUG-23)
                var representative = itemGroup.First();
                int maxOrderedQty = itemGroup.Max(i => i.OrderedQty);
                int totalPreviouslyDeliv =
                    representative.PreviouslyDeliveredQty +
                    representative.DeliveredQty;

                var item = new DeliveryNoteItem
                {
                    DeliveryNoteId = newDeliveryNoteId,
                    JobOrderId = priorDn.JobOrderId,
                    SpecGroupNumber = representative.SpecGroupNumber,
                    SpecGroupName = representative.SpecGroupName,
                    SerialNumber = representative.SerialNumber,
                    GlassRef = representative.GlassRef,
                    W1 = representative.W1,
                    H1 = representative.H1,
                    W2 = representative.W2,
                    H2 = representative.H2,
                    OrderedQty = maxOrderedQty,
                    PreviouslyDeliveredQty = totalPreviouslyDeliv,
                    DeliveredQty = 0,
                    BalanceQty = representative.BalanceQty,
                    ReworkQty = 0,
                    ReturnedQty = 0,
                    Notes = ""
                };

                items.Add(item);
                entity.TotalOrderedQty += maxOrderedQty;
                entity.TotalBalanceQty += representative.BalanceQty;
            }

            entity.Items = items;

            // ── Deserialize prior snapshot to get JO ref context ──────────
            var priorSnapshot = DeserializeSnapshot(priorDn);
            var snapshot = BuildSnapshot(
                entity, null, priorSnapshot?.JobOrderRef);
            entity.FullSnapshotJson = JsonSerializer.Serialize(
                snapshot, _jsonOptions);

            return entity;
        }

        // ════════════════════════════════════════════════════════════════
        //  PATH C — Serialize / Deserialize
        // ════════════════════════════════════════════════════════════════

        /// <summary>
        /// Deserializes the JSON snapshot from a saved entity.
        /// Returns null if JSON is empty or malformed.
        /// BUG-25: Catch block preserved — malformed JSON returns null
        /// gracefully. Callers must handle null snapshot defensively.
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
            catch (JsonException ex)
            {
                // Log for diagnostics — malformed snapshot causes empty detail view
                System.Diagnostics.Debug.WriteLine(
                    $"[DeliveryNoteMapper] Snapshot deserialize failed " +
                    $"for DN '{entity.DeliveryNoteId}': {ex.Message}");
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

                    // BUG-22: use SqmCalculator instead of inline formula
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
                            SqmCalculator.CalculatePanel(r.W1, r.H1) * r.Qty)
                            ?? 0m
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