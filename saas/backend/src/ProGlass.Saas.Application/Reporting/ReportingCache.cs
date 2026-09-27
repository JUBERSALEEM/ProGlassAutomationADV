// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application (read 2026-09-27), Phase 23:
//   Services/Reporting/ReportingCacheService.cs (Phase 9: Performance)
// Memoizes the reporting read models with a 30s sliding expiry so a
// filter change repeated within the window hits the cache instead of
// re-querying (the WPF's exact rationale: 5 keystrokes = 5 summary
// queries).
//
// SaaS adaptations (documented in mapping §1.9):
//  • KEYS ARE TENANT-PREFIXED — the WPF is single-user; a shared cache
//    without the tenant prefix would leak one tenant's figures to
//    another. This is the one place the port ADDS a rule, and it is a
//    security boundary, not a business rule.
//  • Invalidation: the WPF clears on every DocumentLifecycle change
//    (an in-process event its repositories raise). The SaaS services do
//    not broadcast writes; the 30-second TTL bounds staleness the same
//    way, and Invalidate() is exposed (POST /reporting/cache/invalidate)
//    for an operator who needs a provably fresh read.
// ═══════════════════════════════════════════════════════════════════════

using System.Collections.Concurrent;

namespace ProGlass.Saas.Application.Reporting;

public sealed class ReportingCacheService : IDisposable
{
    private sealed class CacheEntry
    {
        public object Value { get; init; } = null!;
        public DateTime ExpiryUtc { get; init; }
        public bool IsExpired => DateTime.UtcNow > ExpiryUtc;
    }

    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();
    private readonly TimeSpan _defaultExpiry = TimeSpan.FromSeconds(30);
    private readonly Timer _pruneTimer;
    private volatile bool _disposed;

    public ReportingCacheService()
    {
        // Prune expired entries every 60s (the WPF cadence).
        _pruneTimer = new Timer(_ => PruneExpired(), null,
            TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(60));
    }

    /// <summary>
    /// Tenant-scoped cache key from the method + every filter axis (the
    /// WPF BuildKey, plus the tenant prefix — see the file header).
    /// </summary>
    public string BuildKey(Guid tenantId, string method, ReportingFilter? filter)
    {
        if (filter == null) return $"{tenantId:N}|{method}|null";

        return $"{tenantId:N}|{method}|" +
               $"C={filter.Company}|" +
               $"Cust={filter.Customer}|" +
               $"Proj={filter.Project}|" +
               $"Sales={filter.Salesman}|" +
               $"PI={filter.ProformaNo}|" +
               $"JO={filter.JobOrderNo}|" +
               $"DN={filter.DeliveryNoteNo}|" +
               $"TI={filter.TaxInvoiceNo}|" +
               $"Status={filter.Status}|" +
               $"Search={filter.SearchText}|" +
               $"From={filter.DateFrom:yyyy-MM-dd}|" +
               $"To={filter.DateTo:yyyy-MM-dd}|" +
               $"Page={filter.Page}|" +
               $"PageSize={filter.PageSize}|" +
               $"GlassType={filter.GlassType}|" +
               $"GlassRef={filter.GlassRef}|" +
               $"Spec={filter.Specification}|" +
               $"Sort={filter.SortField}:{filter.SortDescending}";
    }

    public bool TryGet<T>(string key, out T value)
    {
        value = default!;
        if (_cache.TryGetValue(key, out var entry))
        {
            if (!entry.IsExpired)
            {
                if (entry.Value is T typed)
                {
                    value = typed;
                    return true;
                }
            }
            else
            {
                _cache.TryRemove(key, out _);
            }
        }
        return false;
    }

    public void Set<T>(string key, T value, TimeSpan? expiry = null)
    {
        _cache[key] = new CacheEntry
        {
            Value = value!,
            ExpiryUtc = DateTime.UtcNow + (expiry ?? _defaultExpiry)
        };
    }

    /// <summary>Never caches failures — the factory's exception
    /// propagates (the WPF behaviour).</summary>
    public async Task<T> GetOrAddAsync<T>(
        string key, Func<Task<T>> factory, TimeSpan? expiry = null)
    {
        if (TryGet<T>(key, out var cached))
            return cached;

        var value = await factory().ConfigureAwait(false);
        Set(key, value, expiry);
        return value;
    }

    /// <summary>Operator-triggered invalidation (the WPF clears on
    /// DocumentLifecycle; see the file header).</summary>
    public void Invalidate() => Clear();

    public void Clear()
    {
        _cache.Clear();
    }

    public void Remove(string key)
    {
        _cache.TryRemove(key, out _);
    }

    private void PruneExpired()
    {
        try
        {
            foreach (var kvp in _cache)
            {
                if (kvp.Value.IsExpired)
                    _cache.TryRemove(kvp.Key, out _);
            }
        }
        catch
        {
            // The WPF logs-and-continues; a prune failure is not fatal.
        }
    }

    public (int Count, int Expired) GetStats()
    {
        int expired = 0;
        foreach (var kvp in _cache)
            if (kvp.Value.IsExpired) expired++;

        return (_cache.Count, expired);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        try { _pruneTimer?.Dispose(); } catch { /* logged-and-continued in the WPF */ }
        _cache.Clear();
    }
}
