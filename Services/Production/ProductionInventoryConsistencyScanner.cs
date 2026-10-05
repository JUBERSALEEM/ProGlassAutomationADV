// Read-only Production ↔ Sheet Store consistency scanner.
// NEVER writes. NEVER calls UseStockAsync. NEVER releases holds.
// NEVER treats Consumed-without-Used as a defect — that is the
// current product contract (planning consume ≠ physical Used).

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Models.Production.SavedProduction;
using ProGlassAutomation.Services.Database;
using ProGlassAutomation.Services.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Production
{
    public enum ConsistencyKind
    {
        Fact = 0,
        Observation = 1,
        NotDetectable = 2
    }

    public sealed class ConsistencyFinding
    {
        public ConsistencyKind Kind { get; init; }
        public string Code { get; init; } = "";
        public string Message { get; init; } = "";
        public int? GlassSheetId { get; init; }
        public string? ReservationId { get; init; }
        public string? AllocationId { get; init; }
        public string? JobRef { get; init; }
        public int Quantity { get; init; }
    }

    public sealed class ProductionInventoryConsistencyReport
    {
        public DateTime ScannedAtUtc { get; init; } = DateTime.UtcNow;
        public int ReservationRows { get; init; }
        public int AllocationRows { get; init; }
        public int GlassSheetRows { get; init; }
        public int ActiveHoldQuantity { get; init; }
        public int ConsumedQuantity { get; init; }
        public List<ConsistencyFinding> Findings { get; } = new();
        public List<string> Limitations { get; } = new();
    }

    public static class ProductionInventoryConsistencyScanner
    {
        public static async Task<ProductionInventoryConsistencyReport> ScanAsync(
            CancellationToken ct = default)
        {
            ProductionRepository.EnsureInitialized();

            List<SavedMaterialReservation> reservations;
            List<SavedMaterialAllocation> allocations;
            using (var pdb = new ProductionDbContext())
            {
                reservations = await pdb.Reservations.AsNoTracking()
                    .ToListAsync(ct).ConfigureAwait(false);
                allocations = await pdb.Allocations.AsNoTracking()
                    .ToListAsync(ct).ConfigureAwait(false);
            }

            List<(int Id, int Stock, int Used)> sheets;
            List<(int SheetId, string Action, string? DocRef, int Qty)> history;
            using (var app = new AppDbContext())
            {
                var sheetRows = await app.GlassSheets.AsNoTracking()
                    .Select(g => new { g.Id, g.Stock, g.Used })
                    .ToListAsync(ct).ConfigureAwait(false);
                sheets = sheetRows.Select(g => (g.Id, g.Stock, g.Used)).ToList();

                var histRows = await app.SheetHistories.AsNoTracking()
                    .Select(h => new { h.SheetId, h.Action, h.DocumentReference, h.QuantityChanged })
                    .ToListAsync(ct).ConfigureAwait(false);
                history = histRows
                    .Select(h => (h.SheetId, h.Action, h.DocumentReference, h.QuantityChanged))
                    .ToList();
            }

            HashSet<string> releaseRefs;
            using (var pdb = new ProductionDbContext())
            {
                releaseRefs = (await pdb.Releases.AsNoTracking()
                    .Select(r => r.JobRef)
                    .ToListAsync(ct).ConfigureAwait(false))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
            }

            var sheetById = sheets.ToDictionary(s => s.Id);
            var report = new ProductionInventoryConsistencyReport
            {
                ReservationRows = reservations.Count,
                AllocationRows = allocations.Count,
                GlassSheetRows = sheets.Count,
                ActiveHoldQuantity = reservations
                    .Where(r => r.Status == (int)ReservationStatus.Reserved
                             || r.Status == (int)ReservationStatus.Allocated)
                    .Sum(r => r.Quantity),
                ConsumedQuantity = reservations
                    .Where(r => r.Status == (int)ReservationStatus.Consumed)
                    .Sum(r => r.Quantity)
            };

            report.Limitations.AddRange(new[]
            {
                "Cannot distinguish a retried UseStock from a new legitimate use — no physical EventId.",
                "Cannot prove Consumed-without-Used is an error — Consumed is planning-only.",
                "Cannot join a SheetHistory Used row to a ReservationId — history has no ReservationId.",
                "Cannot auto-repair; this scanner never writes.",
                "Stale Reserved cannot be proven without TTL (none exists). Visibility only.",
                "Two SQLite files are not one transaction — scan is a point-in-time read of each."
            });

            foreach (var s in sheets)
            {
                if (s.Used > s.Stock)
                {
                    report.Findings.Add(new ConsistencyFinding
                    {
                        Kind = ConsistencyKind.Fact,
                        Code = "UsedExceedsStock",
                        Message = $"Sheet {s.Id}: Used {s.Used} > Stock {s.Stock}.",
                        GlassSheetId = s.Id,
                        Quantity = s.Used - s.Stock
                    });
                }
            }

            var holdBySheet = reservations
                .Where(r => r.Status == (int)ReservationStatus.Reserved
                         || r.Status == (int)ReservationStatus.Allocated)
                .GroupBy(r => r.GlassSheetId)
                .ToDictionary(g => g.Key, g => g.Sum(r => r.Quantity));

            foreach (var kv in holdBySheet)
            {
                if (kv.Key <= 0) continue;
                if (!sheetById.TryGetValue(kv.Key, out var sheet))
                {
                    report.Findings.Add(new ConsistencyFinding
                    {
                        Kind = ConsistencyKind.Fact,
                        Code = "MissingGlassSheet",
                        Message = $"Active hold {kv.Value} on missing GlassSheetId {kv.Key}.",
                        GlassSheetId = kv.Key,
                        Quantity = kv.Value
                    });
                    continue;
                }

                int physical = sheet.Stock - sheet.Used;
                if (kv.Value > physical)
                {
                    report.Findings.Add(new ConsistencyFinding
                    {
                        Kind = ConsistencyKind.Fact,
                        Code = "ActiveHoldExceedsPhysical",
                        Message =
                            $"Sheet {kv.Key}: planning hold {kv.Value} > PhysicalAvailable {physical} (Stock {sheet.Stock} Used {sheet.Used}).",
                        GlassSheetId = kv.Key,
                        Quantity = kv.Value
                    });
                }
            }

            var reservationIds = reservations
                .Select(r => r.ReservationId)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var a in allocations)
            {
                if (!reservationIds.Contains(a.ReservationId))
                {
                    report.Findings.Add(new ConsistencyFinding
                    {
                        Kind = ConsistencyKind.Fact,
                        Code = "AllocationMissingReservation",
                        Message = $"Allocation {a.AllocationId} references missing reservation {a.ReservationId}.",
                        AllocationId = a.AllocationId,
                        ReservationId = a.ReservationId,
                        JobRef = a.JobRef
                    });
                }
            }

            var resById = reservations.ToDictionary(r => r.ReservationId, StringComparer.Ordinal);
            foreach (var a in allocations.Where(x => x.Status == (int)AllocationStatus.Consumed))
            {
                if (!resById.TryGetValue(a.ReservationId, out var res)) continue;
                if (res.Status == (int)ReservationStatus.Released)
                {
                    report.Findings.Add(new ConsistencyFinding
                    {
                        Kind = ConsistencyKind.Fact,
                        Code = "AllocConsumedReservationReleased",
                        Message =
                            $"Allocation {a.AllocationId} is Consumed while reservation {res.ReservationId} is Released.",
                        AllocationId = a.AllocationId,
                        ReservationId = res.ReservationId,
                        JobRef = res.JobRef,
                        GlassSheetId = res.GlassSheetId
                    });
                }
            }

            foreach (var r in reservations)
            {
                if (string.IsNullOrWhiteSpace(r.JobRef)) continue;
                if (!releaseRefs.Contains(r.JobRef))
                {
                    report.Findings.Add(new ConsistencyFinding
                    {
                        Kind = ConsistencyKind.Fact,
                        Code = "ReservationWithoutRelease",
                        Message = $"Reservation {r.ReservationId} JobRef {r.JobRef} has no production release row.",
                        ReservationId = r.ReservationId,
                        JobRef = r.JobRef,
                        GlassSheetId = r.GlassSheetId
                    });
                }
            }

            // Observation only: Consumed rows exist. Used may be 0. Current contract.
            int consumedNoHistory = 0;
            foreach (var r in reservations.Where(x => x.Status == (int)ReservationStatus.Consumed))
            {
                bool tagged = history.Any(h =>
                    string.Equals(h.Action, "Used", StringComparison.OrdinalIgnoreCase)
                    && !string.IsNullOrWhiteSpace(h.DocRef)
                    && h.DocRef.IndexOf(r.JobRef, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!tagged) consumedNoHistory++;
            }

            if (consumedNoHistory > 0)
            {
                report.Findings.Add(new ConsistencyFinding
                {
                    Kind = ConsistencyKind.Observation,
                    Code = "PlanningConsumedNoMatchingHistory",
                    Message =
                        $"{consumedNoHistory} Consumed reservation(s) have no SheetHistory Used row tagged with that JobRef. " +
                        "Not proof of missing physical use — Consumed does not write Used, and Used often has empty DocumentReference.",
                    Quantity = consumedNoHistory
                });
            }

            report.Findings.Add(new ConsistencyFinding
            {
                Kind = ConsistencyKind.NotDetectable,
                Code = "RetryVsNewConsumption",
                Message = PhysicalInventoryConsumptionIdentity.MissingIdempotencyKey
            });

            AppLogService.Debug(
                $"[InventoryConsistency] findings={report.Findings.Count} holds={report.ActiveHoldQuantity} consumed={report.ConsumedQuantity}");

            return report;
        }
    }
}
