// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Database/GlobalQueryEngine.cs
// PROJECT: ProGlassAutomation
// MODULE: Database Core — Global High-Scale Query & Paging Engine
//
// PURPOSE:
//   ONE centralized, high-scale database query infrastructure for all ERP
//   modules (Proforma Invoice, Job Order, Delivery Note, Tax Invoice,
//   Statements, Inventory, Master Data, Reports, AI tools, and future modules).
//
// DESIGN PRINCIPLES (MASTER PROMPT RULES):
//   • Zero full-table memory materialization — all filtering, paging,
//     and aggregations happen in SQLite / SQL engine.
//   • Standardized 100-row controlled data windows.
//   • Automatic AsNoTracking() enforcement for read queries.
//   • Direct CancellationToken propagation across every async step.
//   • Projection-first design (.Select() returns only needed columns).
// ═══════════════════════════════════════════════════════════════════════

using Microsoft.EntityFrameworkCore;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.Reporting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.Database
{
    /// <summary>
    /// Contract for the global high-scale database query and pagination engine.
    /// </summary>
    public interface IGlobalQueryEngine
    {
        Task<PagedResult<TResult>> ExecutePagedQueryAsync<TSource, TResult>(
            IQueryable<TSource> query,
            Expression<Func<TSource, TResult>> selector,
            int page = 1,
            int pageSize = 100,
            CancellationToken ct = default)
            where TSource : class;

        Task<int> ExecuteCountAsync<TSource>(
            IQueryable<TSource> query,
            CancellationToken ct = default)
            where TSource : class;

        Task<decimal> ExecuteSumAsync<TSource>(
            IQueryable<TSource> query,
            Expression<Func<TSource, decimal?>> selector,
            CancellationToken ct = default)
            where TSource : class;
    }

    /// <summary>
    /// Thread-safe, high-performance database query engine.
    /// </summary>
    public sealed class GlobalQueryEngine : IGlobalQueryEngine
    {
        public static readonly GlobalQueryEngine Instance = new();

        public const int DefaultPageSize = 100;

        /// <summary>
        /// Executes a database-side paginated query with projection.
        /// Guaranteed: Only the requested page window (default 100 rows) is retrieved from the database.
        /// </summary>
        public async Task<PagedResult<TResult>> ExecutePagedQueryAsync<TSource, TResult>(
            IQueryable<TSource> query,
            Expression<Func<TSource, TResult>> selector,
            int page = 1,
            int pageSize = DefaultPageSize,
            CancellationToken ct = default)
            where TSource : class
        {
            if (query == null) throw new ArgumentNullException(nameof(query));
            if (selector == null) throw new ArgumentNullException(nameof(selector));

            var safePage = Math.Max(1, page);
            var safePageSize = Math.Max(1, pageSize);
            var skip = (safePage - 1) * safePageSize;

            try
            {
                // 1. Database-side total count query
                var totalCount = await query.AsNoTracking().CountAsync(ct).ConfigureAwait(false);
                if (totalCount == 0 || ct.IsCancellationRequested)
                {
                    return new PagedResult<TResult>
                    {
                        Items = new List<TResult>(),
                        TotalCount = totalCount,
                        Page = safePage,
                        PageSize = safePageSize
                    };
                }

                // 2. Database-side Skip + Take + Projection query
                var items = await query.AsNoTracking()
                    .Skip(skip)
                    .Take(safePageSize)
                    .Select(selector)
                    .ToListAsync(ct)
                    .ConfigureAwait(false);

                return new PagedResult<TResult>
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = safePage,
                    PageSize = safePageSize
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"GlobalQueryEngine.ExecutePagedQueryAsync error: {ex.Message}");
                return new PagedResult<TResult>
                {
                    Items = new List<TResult>(),
                    TotalCount = 0,
                    Page = safePage,
                    PageSize = safePageSize
                };
            }
        }

        /// <summary>
        /// Executes a fast database-side COUNT query.
        /// </summary>
        public async Task<int> ExecuteCountAsync<TSource>(
            IQueryable<TSource> query,
            CancellationToken ct = default)
            where TSource : class
        {
            if (query == null) return 0;
            try
            {
                return await query.AsNoTracking().CountAsync(ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"GlobalQueryEngine.ExecuteCountAsync error: {ex.Message}");
                return 0;
            }
        }

        /// <summary>
        /// Executes a fast database-side SUM query.
        /// </summary>
        public async Task<decimal> ExecuteSumAsync<TSource>(
            IQueryable<TSource> query,
            Expression<Func<TSource, decimal?>> selector,
            CancellationToken ct = default)
            where TSource : class
        {
            if (query == null || selector == null) return 0m;
            try
            {
                return await query.AsNoTracking().SumAsync(selector, ct).ConfigureAwait(false) ?? 0m;
            }
            catch (Exception ex)
            {
                AppLogService.Warning($"GlobalQueryEngine.ExecuteSumAsync error: {ex.Message}");
                return 0m;
            }
        }
    }
}
