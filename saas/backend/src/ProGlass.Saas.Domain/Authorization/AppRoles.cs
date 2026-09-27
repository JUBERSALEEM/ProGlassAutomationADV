// ═══════════════════════════════════════════════════════════════════════
// PORTED from the WPF application: Services/Audit/Permissions.cs
// The WPF file is the source of truth for the role and permission
// vocabulary AND for which role grants which permission. This port changes
// no grant. The WPF PermissionService (dialog + audit on denial) is a UI
// concern and is replaced in Phase 4 by server-side authorization
// policies; the catalog itself must stay identical so WPF user roles keep
// working after migration.
// ═══════════════════════════════════════════════════════════════════════

namespace ProGlass.Saas.Domain.Authorization;

/// <summary>Canonical role names (WPF AppRoles, verbatim).</summary>
public static class AppRoles
{
    public const string Administrator = "Administrator";
    public const string Manager = "Manager";
    public const string Estimator = "Estimator";
    public const string Sales = "Sales";
    public const string Production = "Production";
    public const string Store = "Store";
    public const string Accountant = "Accountant";
    public const string Viewer = "Viewer";

    /// <summary>Legacy role kept for backward compatibility (= Store set).</summary>
    public const string Operator = "Operator";

    /// <summary>Options shown in the Users UI.</summary>
    public static readonly string[] All =
    {
        Administrator, Manager, Estimator, Sales,
        Production, Store, Accountant, Viewer
    };
}

/// <summary>Granular permission names ("Module.Capability") — WPF verbatim.</summary>
public static class AppPermissions
{
    public const string DashboardView = "Dashboard.View";

    public const string InquiryView = "Inquiry.View";
    public const string InquiryCreate = "Inquiry.Create";
    public const string InquiryEdit = "Inquiry.Edit";
    public const string InquiryDelete = "Inquiry.Delete";

    public const string ProformaView = "ProformaInvoice.View";
    public const string ProformaCreate = "ProformaInvoice.Create";
    public const string ProformaEdit = "ProformaInvoice.Edit";
    public const string ProformaDelete = "ProformaInvoice.Delete";
    public const string ProformaExport = "ProformaInvoice.Export";
    public const string ProformaPrint = "ProformaInvoice.Print";

    public const string JobOrderView = "JobOrder.View";
    public const string JobOrderEdit = "JobOrder.Edit";
    public const string JobOrderDelete = "JobOrder.Delete";

    public const string DeliveryNoteView = "DeliveryNote.View";
    public const string DeliveryNoteEdit = "DeliveryNote.Edit";

    public const string TaxInvoiceView = "TaxInvoice.View";
    public const string TaxInvoiceEdit = "TaxInvoice.Edit";
    public const string TaxInvoiceExport = "TaxInvoice.Export";
    // ERP lifecycle hardening (WPF Phase 3 G-6): deleting/voiding a fiscal
    // document is Manager/Admin-only. An Accountant may edit and issue
    // invoices but must NOT be able to erase a posted one.
    public const string TaxInvoiceDelete = "TaxInvoice.Delete";

    public const string CreditNoteView = "CreditNote.View";
    public const string CreditNoteEdit = "CreditNote.Edit";
    public const string CreditNoteExport = "CreditNote.Export";
    public const string CreditNoteDelete = "CreditNote.Delete";

    public const string SheetStoreView = "SheetStore.View";
    public const string SheetStoreCreate = "SheetStore.Create";
    public const string SheetStoreEdit = "SheetStore.Edit";
    public const string SheetStoreDelete = "SheetStore.Delete";

    public const string ReportsView = "Reports.View";
    public const string ReportsExport = "Reports.Export";
    public const string StatementGenerate = "Statement.Generate";

    public const string LogsView = "Logs.View";
    public const string AuditView = "Audit.View";
    public const string SettingsManage = "Settings.Manage";

    /// <summary>Wildcard used only for Administrators.</summary>
    public const string All = "*";
}

/// <summary>
/// Resolves which permissions a role carries — the WPF RolePermissions map,
/// grant for grant (verified by ProGlass.Saas.Domain.Tests parity tests
/// against the WPF source).
/// </summary>
public static class RolePermissions
{
    private static readonly Dictionary<string, string[]> Map = new()
    {
        [AppRoles.Administrator] = new[] { AppPermissions.All },

        [AppRoles.Manager] = ViewAll().Concat(new[]
        {
            AppPermissions.InquiryCreate, AppPermissions.InquiryEdit,
            AppPermissions.InquiryDelete,
            AppPermissions.ProformaCreate, AppPermissions.ProformaEdit,
            AppPermissions.ProformaDelete, AppPermissions.ProformaExport,
            AppPermissions.ProformaPrint,
            AppPermissions.JobOrderEdit, AppPermissions.JobOrderDelete,
            AppPermissions.DeliveryNoteEdit,
            AppPermissions.TaxInvoiceEdit, AppPermissions.TaxInvoiceExport,
            AppPermissions.TaxInvoiceDelete,
            AppPermissions.CreditNoteEdit, AppPermissions.CreditNoteExport,
            AppPermissions.CreditNoteDelete,
            AppPermissions.SheetStoreCreate, AppPermissions.SheetStoreEdit,
            AppPermissions.SheetStoreDelete,
            AppPermissions.ReportsExport, AppPermissions.StatementGenerate
        }).ToArray(),

        [AppRoles.Estimator] = new[]
        {
            AppPermissions.DashboardView,
            AppPermissions.InquiryView, AppPermissions.InquiryCreate,
            AppPermissions.InquiryEdit,
            AppPermissions.ProformaView, AppPermissions.ProformaCreate,
            AppPermissions.ProformaEdit, AppPermissions.ProformaExport,
            AppPermissions.ProformaPrint,
            AppPermissions.SheetStoreView,
            AppPermissions.JobOrderView, AppPermissions.ReportsView
        },

        [AppRoles.Sales] = new[]
        {
            AppPermissions.DashboardView,
            AppPermissions.InquiryView, AppPermissions.InquiryCreate,
            AppPermissions.InquiryEdit,
            AppPermissions.ProformaView, AppPermissions.ProformaCreate,
            AppPermissions.ProformaEdit, AppPermissions.ProformaExport,
            AppPermissions.ProformaPrint,
            AppPermissions.JobOrderView, AppPermissions.DeliveryNoteView,
            AppPermissions.TaxInvoiceView, AppPermissions.ReportsView
        },

        [AppRoles.Production] = new[]
        {
            AppPermissions.DashboardView,
            AppPermissions.InquiryView,
            AppPermissions.JobOrderView, AppPermissions.JobOrderEdit,
            AppPermissions.DeliveryNoteView, AppPermissions.DeliveryNoteEdit,
            AppPermissions.ProformaView, AppPermissions.SheetStoreView
        },

        // "Operator" (legacy) resolves to the same set as Store.
        [AppRoles.Store] = new[]
        {
            AppPermissions.DashboardView,
            AppPermissions.SheetStoreView, AppPermissions.SheetStoreCreate,
            AppPermissions.SheetStoreEdit, AppPermissions.SheetStoreDelete,
            AppPermissions.DeliveryNoteView
        },

        [AppRoles.Accountant] = new[]
        {
            AppPermissions.DashboardView,
            AppPermissions.TaxInvoiceView, AppPermissions.TaxInvoiceEdit,
            AppPermissions.TaxInvoiceExport,
            AppPermissions.CreditNoteView, AppPermissions.CreditNoteEdit,
            AppPermissions.CreditNoteExport,
            AppPermissions.ReportsView, AppPermissions.ReportsExport,
            AppPermissions.StatementGenerate,
            AppPermissions.ProformaView
        },

        [AppRoles.Viewer] = ViewAll()
    };

    static RolePermissions()
    {
        // Legacy role names from earlier builds keep working.
        Map[AppRoles.Operator] = Map[AppRoles.Store];
        Map["Admin"] = Map[AppRoles.Administrator];
    }

    private static string[] ViewAll() => new[]
    {
        AppPermissions.DashboardView, AppPermissions.ProformaView,
        AppPermissions.JobOrderView, AppPermissions.DeliveryNoteView,
        AppPermissions.TaxInvoiceView, AppPermissions.CreditNoteView,
        AppPermissions.SheetStoreView,
        AppPermissions.ReportsView, AppPermissions.LogsView,
        AppPermissions.AuditView
    };

    /// <summary>Permission set for a role (empty when unknown).</summary>
    public static IReadOnlyCollection<string> For(string? role) =>
        Map.TryGetValue(role ?? "", out var set) ? set : Array.Empty<string>();

    /// <summary>True when the role grants the permission.</summary>
    public static bool Has(string? role, string permission)
    {
        var set = For(role);
        return set.Contains(AppPermissions.All) || set.Contains(permission);
    }
}
