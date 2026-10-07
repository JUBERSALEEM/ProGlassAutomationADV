using System.Text.Json.Serialization;

namespace ProGlassAutomation.Services.RecordsSync
{
    /// <summary>
    /// Persisted configuration for the records → Supabase sync.
    ///
    /// Lives in %LOCALAPPDATA%\ProGlassAutomation\recordssync.json (the same
    /// AppData root every other per-install file uses). Deliberately NOT part
    /// of AppConfig: the sync is an optional integration and adding keys to
    /// the shared config class would touch every existing config consumer
    /// for no benefit.
    ///
    /// SECURITY (aligned with Windowfilehost's proven posture — its README:
    /// "publishable/anon key plus an authenticated user; no service-role
    /// credential is accepted"):
    ///   • This file holds NO secrets — URL, auth e-mail and switches only.
    ///   • The publishable/anon key and the dedicated auth user's password
    ///     live in Services.Security.SecretVault (DPAPI, CurrentUser scope)
    ///     under the RecordsSyncService.Secret* keys.
    ///   • Writes require the dedicated `authenticated` user (SELECT/INSERT/
    ///     UPDATE on records, no DELETE) — never service-role, no anon Bearer
    ///     fallback, and no RLS bypass.
    /// </summary>
    public sealed class RecordsSyncSettings
    {
        /// <summary>Master switch. Default false — the integration is inert
        /// until an operator explicitly enables and configures it.</summary>
        public bool Enabled { get; set; }

        /// <summary>Supabase project URL, e.g. https://xxxx.supabase.co</summary>
        public string SupabaseUrl { get; set; } = "";

        /// <summary>Dedicated Supabase Auth user (e-mail) for the sync — the
        /// Windowfilehost pattern. Password lives in SecretVault.</summary>
        public string AuthEmail { get; set; } = "";

        /// <summary>Non-secret identity metadata for the project/user that the
        /// saved SecretVault key and password were last stored against. Older
        /// settings have no binding and require explicit credential re-entry.</summary>
        public string CredentialBindingProjectUrl { get; set; } = "";

        /// <summary>Non-secret Auth email associated with the saved key/password.</summary>
        public string CredentialBindingAuthEmail { get; set; } = "";

        /// <summary>Source label written to records.source_file. The website
        /// source-scopes by this value; keeping it distinct from any Excel
        /// workbook name makes ownership visible and auditable.</summary>
        public string SourceFile { get; set; } = "ProGlassAutomation";

        /// <summary>Source label written to records.source_worksheet.</summary>
        public string SourceWorksheet { get; set; } = "PI";

        /// <summary>Minutes between background sync attempts (clamped 5..1440).</summary>
        public int IntervalMinutes { get; set; } = 15;
    }

    /// <summary>
    /// One row of public.records, exactly as the proven Windowfilehost
    /// payload (Models/SupabaseRecordPayload.cs) and the website's
    /// RecordRow define it. ALL business fields are strings — qty and sqm
    /// are text columns in the deployed schema, so sending JSON numbers
    /// would be a type error at PostgREST.
    /// </summary>
    public sealed class RecordPayload
    {
        /// <summary>Business date. String by contract; the website's
        /// parseExcelDate accepts DD-MM-YYYY / DD/MM/YYYY / DD.MM.YYYY.
        /// We always emit DD-MM-YYYY.</summary>
        [JsonPropertyName("date")] public string Date { get; set; } = "";

        [JsonPropertyName("company")] public string Company { get; set; } = "";
        [JsonPropertyName("salesman")] public string Salesman { get; set; } = "";
        [JsonPropertyName("pi_number")] public string PiNumber { get; set; } = "";
        [JsonPropertyName("customer_ref")] public string CustomerRef { get; set; } = "";
        [JsonPropertyName("prepared_by")] public string PreparedBy { get; set; } = "";
        [JsonPropertyName("type_of_work")] public string TypeOfWork { get; set; } = "";
        [JsonPropertyName("status")] public string Status { get; set; } = "";
        [JsonPropertyName("qty")] public string Qty { get; set; } = "";
        [JsonPropertyName("sqm")] public string Sqm { get; set; } = "";
        [JsonPropertyName("pi_status")] public string PiStatus { get; set; } = "";

        /// <summary>ProGlass Automation has no Zoho integration; the column
        /// is part of the contract so it is always sent empty rather than
        /// omitted (omission would leave stale values on updates).</summary>
        [JsonPropertyName("zoho_status")] public string ZohoStatus { get; set; } = "";

        /// <summary>Soft-active flag added by the website's dashboard
        /// migration; always true for rows we send (the reconcile RPC owns
        /// deactivation).</summary>
        [JsonPropertyName("is_active")] public bool IsActive { get; set; } = true;

        [JsonPropertyName("source_file")] public string SourceFile { get; set; } = "";
        [JsonPropertyName("source_worksheet")] public string SourceWorksheet { get; set; } = "";
    }

    /// <summary>
    /// Local sync state — the change-detection ledger. Rows whose content
    /// hash is unchanged since the last successful run are not re-sent,
    /// which is what makes "run sync twice" a no-op instead of a storm of
    /// identical PATCHes. Stored next to the settings file; losing it is
    /// safe (worst case: one full idempotent re-send).
    /// </summary>
    public sealed class RecordsSyncState
    {
        public DateTime LastSuccessUtc { get; set; }
        public int LastSentCount { get; set; }

        /// <summary>pi_number → SHA-256 hex of the serialized payload.</summary>
        public Dictionary<string, string> Hashes { get; set; } = new();
    }

    /// <summary>Read-only status shown on the Records Sync settings page.</summary>
    public sealed record RecordsSyncStatusSnapshot
    {
        public bool Enabled { get; init; }
        public bool AtomicBackendAvailable { get; init; }
        public int PendingCount { get; init; }
        public DateTimeOffset? LastSuccessfulSyncUtc { get; init; }
    }
}
