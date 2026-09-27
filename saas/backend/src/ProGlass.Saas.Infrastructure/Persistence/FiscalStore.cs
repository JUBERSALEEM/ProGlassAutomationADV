using Microsoft.EntityFrameworkCore;
using ProGlass.Saas.Application.Fiscal;
using ProGlass.Saas.Domain.Entities;

namespace ProGlass.Saas.Infrastructure.Persistence;

// ═══════════════════════════════════════════════════════════════════════
//  Phase 12 storage — the WPF TaxInvoiceRepository / CreditNoteRepository
//  / StatementRepository port: RLS-scoped contexts per tenant, unique-
//  violation-aware inserts for the fiscal document numbers (INV-/CN-),
//  one-transaction statement persistence.
// ═══════════════════════════════════════════════════════════════════════

public sealed class FiscalStore(SaasDbContextFactory factory) : IFiscalStore
{
    // ── lookups ─────────────────────────────────────────────────────

    public async Task<ProformaInvoice?> FindProformaAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.ProformaInvoices.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<JobOrder?> FindJobAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<TaxInvoice?> FindInvoiceAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.TaxInvoices.AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaxInvoice>> ListInvoicesForProformaAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.TaxInvoices.AsNoTracking()
            .Where(i => i.ProformaId == proformaId)
            .OrderBy(i => i.InvoiceNo)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<TaxInvoice>> SearchInvoicesAsync(
        Guid tenantId, string? search, string? status, bool includeVoid,
        int page, int pageSize, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var q = db.TaxInvoices.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            q = q.Where(i =>
                i.InvoiceNo.Contains(needle) ||
                i.CustomerName.Contains(needle) ||
                i.ProjectName.Contains(needle));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            q = q.Where(i => i.Status == status);
        }

        if (!includeVoid)
        {
            q = q.Where(i => !i.IsVoid);
        }

        return await q
            .OrderByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<(int MaxSeq, HashSet<string> UsedNumbers)>
        GetInvoiceNumbersAsync(Guid tenantId, int year, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var prefix = $"INV-{year}-";
        var numbers = await db.TaxInvoices.AsNoTracking()
            .Where(i => i.InvoiceNo.StartsWith(prefix))
            .Select(i => i.InvoiceNo)
            .ToListAsync(ct).ConfigureAwait(false);
        return (MaxSequence(numbers), [.. numbers]);
    }

    public async Task<(int MaxSeq, HashSet<string> UsedNumbers)>
        GetCreditNoteNumbersAsync(Guid tenantId, int year, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var prefix = $"CN-{year}-";
        var numbers = await db.CreditNotes.AsNoTracking()
            .Where(n => n.CreditNoteNo.StartsWith(prefix))
            .Select(n => n.CreditNoteNo)
            .ToListAsync(ct).ConfigureAwait(false);
        return (MaxSequence(numbers), [.. numbers]);
    }

    private static int MaxSequence(IReadOnlyList<string> numbers)
    {
        var max = 0;
        foreach (var n in numbers)
        {
            var parts = n.Split('-');
            if (parts.Length >= 3
                && int.TryParse(parts[2], out var seq)
                && seq > max)
            {
                max = seq;
            }
        }

        return max;
    }

    // ── writes ──────────────────────────────────────────────────────

    public async Task<bool> TryInsertInvoiceAsync(
        TaxInvoice invoice, IReadOnlyList<TaxInvoiceItem>? items,
        CancellationToken ct)
    {
        using var db = factory.CreateForTenant(invoice.TenantId);
        db.TaxInvoices.Add(invoice);
        if (items is { Count: > 0 })
        {
            db.TaxInvoiceItems.AddRange(items);
        }

        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateInvoiceAsync(
        TaxInvoice invoice, int? expectedRowVersion, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(invoice.TenantId);
        var stored = await db.TaxInvoices
            .FirstOrDefaultAsync(i => i.Id == invoice.Id, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        if (expectedRowVersion.HasValue
            && stored.RowVersion != expectedRowVersion.Value)
        {
            return false;
        }

        db.Entry(stored).CurrentValues.SetValues(invoice);
        stored.RowVersion = invoice.RowVersion + 1;
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    // ── credit notes ────────────────────────────────────────────────

    public async Task<CreditNote?> FindCreditNoteAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.CreditNotes.AsNoTracking()
            .Include(n => n.TaxInvoice)
            .FirstOrDefaultAsync(n => n.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CreditNote>> ListCreditNotesAsync(
        Guid tenantId, Guid? taxInvoiceId, int page, int pageSize,
        CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var q = db.CreditNotes.AsNoTracking().AsQueryable();
        if (taxInvoiceId.HasValue)
        {
            q = q.Where(n => n.TaxInvoiceId == taxInvoiceId.Value);
        }

        return await q
            .OrderByDescending(n => n.CreatedAt)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> TryInsertCreditNoteAsync(
        CreditNote note, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(note.TenantId);
        db.CreditNotes.Add(note);
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateCreditNoteAsync(
        CreditNote note, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(note.TenantId);
        var stored = await db.CreditNotes
            .FirstOrDefaultAsync(n => n.Id == note.Id, ct).ConfigureAwait(false);
        if (stored is null)
        {
            return false;
        }

        db.Entry(stored).CurrentValues.SetValues(note);
        stored.RowVersion = note.RowVersion + 1;
        try
        {
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return true;
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return false;
        }
    }

    // ── VAT + statements ────────────────────────────────────────────

    public async Task<IReadOnlyList<TaxInvoice>> ListVatableInvoicesAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var f = from.ToDateTime(TimeOnly.MinValue);
        var t = to.ToDateTime(TimeOnly.MaxValue);
        return await db.TaxInvoices.AsNoTracking()
            .Where(i => i.IssueDate != null
                && i.CreatedAt >= f && i.CreatedAt <= t
                && !i.IsVoid
                && (i.Status == "Issued" || i.Status == "PartiallyPaid"
                    || i.Status == "Paid" || i.Status == "Overdue"))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<CreditNote>> ListVatableCreditNotesAsync(
        Guid tenantId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var f = from.ToDateTime(TimeOnly.MinValue);
        var t = to.ToDateTime(TimeOnly.MaxValue);
        return await db.CreditNotes.AsNoTracking()
            .Where(n => n.CreatedAt >= f && n.CreatedAt <= t
                && (n.Status == "Issued" || n.Status == "Applied"))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ProformaLine>> ListProformaLinesAsync(
        Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        // SpecName is the SaaS-normalized display name (the WPF snapshot's
        // GroupName has no dedicated column — documented); GlassRef is
        // snapshot-only (0009 parity uses position), so the row carries ""
        // and the composite key is (group number, row serial).
        return await (from g in db.SpecGroups.AsNoTracking()
                      join i in db.SpecGroupItems.AsNoTracking()
                          on g.Id equals i.SpecGroupId
                      where g.ProformaId == proformaId
                      orderby g.GroupNumber, i.RowSerial
                      select new ProformaLine(
                          g.GroupNumber, g.SpecName, g.GlassType,
                          i.RowSerial, "",
                          i.W1, i.H1, i.W2, i.H2, i.Qty))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<(Guid JobOrderId, string JobNo)>>
        ListJobOrderNosForProformaAsync(
            Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.JobOrders.AsNoTracking()
            .Where(j => j.ProformaId == proformaId)
            .OrderBy(j => j.JobNo)
            .Select(j => new ValueTuple<Guid, string>(j.Id, j.JobNo))
            .ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<Dictionary<(int Group, int Serial), int>>
        SumDeliveredByLineAsync(
            Guid tenantId, IReadOnlyList<Guid> jobOrderIds,
            CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        // The WPF "confirmed DN" filter: excludes Draft, Dummy and master
        // DNs; sums DeliveredQty by the composite key.
        var noteIds = await db.DeliveryNotes.AsNoTracking()
            .Where(n => jobOrderIds.Contains(n.JobOrderId)
                && !n.IsMasterDn
                && n.Status != "Draft"
                && n.DeliveryType != "Dummy")
            .Select(n => n.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        return await db.DeliveryNoteItems.AsNoTracking()
            .Where(i => noteIds.Contains(i.DeliveryNoteId))
            .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
            .Select(g => new
            {
                g.Key.SpecGroupNumber,
                g.Key.SerialNumber,
                Qty = g.Sum(i => i.DeliveredQty)
            })
            .ToDictionaryAsync(
                x => (x.SpecGroupNumber, x.SerialNumber),
                x => x.Qty, ct).ConfigureAwait(false);
    }

    public async Task<Dictionary<(int Group, int Serial), int>>
        SumInvoicedByLineAsync(
            Guid tenantId, Guid proformaId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        // The WPF filter: excludes Cancelled, Void and Draft invoices.
        var invoiceIds = await db.TaxInvoices.AsNoTracking()
            .Where(t => t.ProformaId == proformaId
                && t.Status != "Draft"
                && t.Status != "Cancelled"
                && t.Status != "Void"
                && !t.IsVoid)
            .Select(t => t.Id)
            .ToListAsync(ct).ConfigureAwait(false);
        return await db.TaxInvoiceItems.AsNoTracking()
            .Where(i => invoiceIds.Contains(i.TaxInvoiceId))
            .GroupBy(i => new { i.SpecGroupNumber, i.SerialNumber })
            .Select(g => new
            {
                g.Key.SpecGroupNumber,
                g.Key.SerialNumber,
                Qty = g.Sum(i => i.Qty)
            })
            .ToDictionaryAsync(
                x => (x.SpecGroupNumber, x.SerialNumber),
                x => x.Qty, ct).ConfigureAwait(false);
    }

    public async Task<int> CountInvoicesSinceAsync(
        Guid tenantId, DateOnly since, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var from = since.ToDateTime(TimeOnly.MinValue);
        return await db.TaxInvoices.AsNoTracking()
            .Where(i => i.CreatedAt >= from)
            .CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<int> CountStatementsSinceAsync(
        Guid tenantId, DateOnly since, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        var from = since.ToDateTime(TimeOnly.MinValue);
        return await db.Statements.AsNoTracking()
            .Where(s => s.CreatedAt >= from)
            .CountAsync(ct).ConfigureAwait(false);
    }

    public async Task<Statement> PersistStatementAsync(
        Guid tenantId, Guid actorId, Statement statement,
        IReadOnlyList<StatementLine> lines, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        await using var tx = await db.Database
            .BeginTransactionAsync(ct).ConfigureAwait(false);
        db.Statements.Add(statement);
        foreach (var line in lines)
        {
            db.StatementLines.Add(line);
        }

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return statement;
    }

    public async Task<Statement?> FindStatementAsync(
        Guid tenantId, Guid id, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.Statements.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == id, ct).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<StatementLine>> ListStatementLinesAsync(
        Guid tenantId, Guid statementId, CancellationToken ct)
    {
        using var db = factory.CreateForTenant(tenantId);
        return await db.StatementLines.AsNoTracking()
            .Where(l => l.StatementId == statementId)
            .OrderBy(l => l.LineNo)
            .ToListAsync(ct).ConfigureAwait(false);
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException?.Message.Contains("23505", StringComparison.Ordinal) == true
        || ex.InnerException?.Message.Contains(
            "duplicate key value violates unique constraint",
            StringComparison.OrdinalIgnoreCase) == true;
}
