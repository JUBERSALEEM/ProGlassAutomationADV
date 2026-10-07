using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ProGlassAutomation.Services.RecordsSync
{
    /// <summary>
    /// PostgREST transport for public.records — the ONLY network code in the
    /// sync. No Supabase client library is introduced: the REST surface the
    /// website and Windowfilehost both use is plain HTTPS + JSON.
    ///
    /// Contract (verified against the Windowfilehost reference source —
    /// Services/SupabaseService.cs, supabase/schema.sql, docs/SUPABASE_SETUP.md):
    ///   • pi_number is the table's PRIMARY KEY — record identity is GLOBAL,
    ///     not per source scope.
    ///   • qty/sqm are TEXT columns; every business field ships as a string.
    ///   • Auth = publishable/anon key + a DEDICATED authenticated user
    ///     (POST /auth/v1/token?grant_type=password). Writes run as the
    ///     `authenticated` role with SELECT/INSERT/UPDATE only — no
    ///     service-role, no RLS bypass, no DELETE.
    ///   • Writes are fenced: inserts are POST-only for rows the remote
    ///     snapshot says are missing; updates are per-row PATCHes filtered
    ///     on pi_number + OBSERVED source_file/source_worksheet + OBSERVED
    ///     updated_at. A row that changed under us (or is owned by another
    ///     producer and moved) simply matches zero rows and is retried next
    ///     pass — accidental overwrites are impossible by construction.
    ///   • A PI that exists remotely under the Excel workbook's ownership
    ///     AND exists in the ProGlass database is reassigned to this app's
    ///     source scope via the same fenced PATCH — the exact "conditional
    ///     reassignment when present in the local source" semantics the
    ///     reference implements. Absent locally, remote rows are untouched.
    ///   • End-of-pass reconciliation uses the existing
    ///     reconcile_records_active_state RPC (server-side empty-array
    ///     guard), same as Windowfilehost.
    ///
    /// All methods throw on hard failure — the CALLER owns retry/backoff.
    /// </summary>
    public sealed class SupabaseRecordsClient : IDisposable
    {
        private const string RecordsEndpoint = "/rest/v1/records";
        private const string ReconcileRpc = "/rest/v1/rpc/reconcile_records_active_state";
        private const int SelectChunk = 100;   // pi_numbers per snapshot GET
        private const int InsertChunk = 50;    // rows per POST

        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string? _authEmail;
        private readonly string? _authPassword;

        public SupabaseRecordsClient(
            string supabaseUrl, string apiKey,
            string? authEmail = null, string? authPassword = null)
        {
            _baseUrl = (supabaseUrl ?? "").TrimEnd('/');
            _apiKey = apiKey ?? "";
            _authEmail = string.IsNullOrWhiteSpace(authEmail) ? null : authEmail.Trim();
            _authPassword = string.IsNullOrWhiteSpace(authPassword) ? null : authPassword;

            if (_baseUrl.Length == 0 || _apiKey.Length == 0)
                throw new ArgumentException("Supabase URL and key are both required.");

            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.Add("apikey", _apiKey);
        }

        /// <summary>Explicit read-only Settings check. It authenticates the
        /// dedicated account and requests zero PI rows; no business record is
        /// read and no write endpoint is called.</summary>
        public async Task TestReadConnectionAsync(CancellationToken cancellationToken = default)
        {
            if (_authEmail == null || _authPassword == null)
                throw new InvalidOperationException(
                    "A dedicated Records Sync account email and password are required for this test.");

            var bearer = await ResolveBearerTokenAsync(cancellationToken)
                .ConfigureAwait(false);
            using var request = new HttpRequestMessage(
                HttpMethod.Get, _baseUrl + RecordsEndpoint + "?select=pi_number&limit=0");
            AddAuth(request, bearer);

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new HttpRequestException(
                    "Supabase read-only check failed with HTTP " +
                    (int)response.StatusCode + " " + response.ReasonPhrase + ".",
                    null,
                    response.StatusCode);
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            using var document = JsonDocument.Parse(body);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException(
                    "Supabase returned an unexpected response for the read-only PI check.");
        }

        private sealed record RemoteRow(
            string PiNumber, string? SourceFile, string? SourceWorksheet, string? UpdatedAtRaw);

        /// <summary>
        /// One synchronization pass. Returns (written, skipped). Idempotent
        /// and version-fenced: re-running sends only what the local ledger
        /// flags as changed, and every remote mutation is preconditioned on
        /// the row exactly as observed moments earlier.
        /// </summary>
        public async Task<(int Written, int Skipped)> SyncAsync(
            IReadOnlyList<RecordPayload> rows,
            IReadOnlyList<string> activePiNumbers,
            string sourceFile,
            string sourceWorksheet,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(rows);
            ArgumentNullException.ThrowIfNull(activePiNumbers);

            var bearer = await ResolveBearerTokenAsync(cancellationToken)
                .ConfigureAwait(false);

            var written = 0;
            var skipped = 0;

            foreach (var chunk in Chunk(rows, SelectChunk))
            {
                var remote = await FetchRemoteRowsAsync(chunk, bearer, cancellationToken)
                    .ConfigureAwait(false);

                var toInsert = new List<RecordPayload>();
                foreach (var row in chunk)
                {
                    if (!remote.TryGetValue(row.PiNumber, out var existing))
                    {
                        toInsert.Add(row);
                        continue;
                    }

                    // Fenced update — filter pins identity + observed owner +
                    // observed version. Zero rows back means the row moved
                    // under us: skip this pass, never blind-write.
                    if (await FencedPatchAsync(row, existing, bearer, cancellationToken)
                            .ConfigureAwait(false))
                        written++;
                    else
                        skipped++;
                }

                if (toInsert.Count > 0)
                {
                    var (inserted, insertSkipped) = await InsertAsync(
                        toInsert, bearer, cancellationToken).ConfigureAwait(false);
                    written += inserted;
                    skipped += insertSkipped;
                }
            }

            await ReconcileAsync(activePiNumbers, sourceFile, sourceWorksheet, bearer, cancellationToken)
                .ConfigureAwait(false);

            return (written, skipped);
        }

        /// <summary>Windowfilehost auth pattern: anon key + dedicated user →
        /// short-lived access token used as Bearer. Without configured
        /// credentials the stored key itself is the Bearer (compatibility
        /// path; the caller logs which mode is active).</summary>
        private async Task<string> ResolveBearerTokenAsync(CancellationToken cancellationToken)
        {
            if (_authEmail == null || _authPassword == null)
                return _apiKey;

            var body = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["email"] = _authEmail,
                ["password"] = _authPassword,
            });

            using var request = new HttpRequestMessage(
                HttpMethod.Post, _baseUrl + "/auth/v1/token?grant_type=password");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("access_token", out var tokenEl) &&
                tokenEl.ValueKind == JsonValueKind.String &&
                !string.IsNullOrEmpty(tokenEl.GetString()))
            {
                return tokenEl.GetString()!;
            }

            throw new InvalidOperationException(
                "Supabase auth response contained no access_token.");
        }

        /// <summary>Remote snapshot for the chunk's PI numbers — GLOBAL by
        /// pi_number (the primary key), deliberately NOT scoped to our
        /// source_file: ownership of an existing PI must be observed, not
        /// assumed away.</summary>
        private async Task<Dictionary<string, RemoteRow>> FetchRemoteRowsAsync(
            IReadOnlyList<RecordPayload> chunk, string bearer,
            CancellationToken cancellationToken)
        {
            var inList = string.Join(",",
                chunk.Select(r => "\"" + r.PiNumber.Replace("\"", "") + "\""));

            var url = _baseUrl + RecordsEndpoint
                + "?select=pi_number,source_file,source_worksheet,updated_at"
                + "&pi_number=in.(" + Uri.EscapeDataString(inList) + ")";

            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            AddAuth(request, bearer);

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            var result = new Dictionary<string, RemoteRow>(StringComparer.Ordinal);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var pi = el.TryGetProperty("pi_number", out var piEl)
                    ? piEl.GetString() : null;
                if (string.IsNullOrEmpty(pi)) continue;

                result[pi] = new RemoteRow(
                    pi,
                    GetNullableString(el, "source_file"),
                    GetNullableString(el, "source_worksheet"),
                    el.TryGetProperty("updated_at", out var upd) &&
                        upd.ValueKind == JsonValueKind.String
                        ? upd.GetString()
                        : null);
            }
            return result;
        }

        /// <summary>One fenced PATCH. The body carries our full row (source
        /// labels included — that is how a locally-present PI is
        /// conditionally reassigned from another producer's scope to ours).
        /// Returns false when the preconditions matched zero rows.</summary>
        private async Task<bool> FencedPatchAsync(
            RecordPayload row, RemoteRow observed, string bearer,
            CancellationToken cancellationToken)
        {
            var filters = new List<string>
            {
                "pi_number=eq." + Uri.EscapeDataString(row.PiNumber),
            };

            // Pin the observed ownership; when the remote row predates
            // source labelling (website migration added the columns
            // nullable) treat unlabelled as its own observed state.
            filters.Add(observed.SourceFile == null
                ? "source_file=is.null"
                : "source_file=eq." + Uri.EscapeDataString(observed.SourceFile));
            filters.Add(observed.SourceWorksheet == null
                ? "source_worksheet=is.null"
                : "source_worksheet=eq." + Uri.EscapeDataString(observed.SourceWorksheet));

            // Version fence on the raw observed timestamp — reused verbatim
            // so no reformatting can drift from what PostgREST stored.
            filters.Add(observed.UpdatedAtRaw != null
                ? "updated_at=eq." + Uri.EscapeDataString(observed.UpdatedAtRaw)
                : "updated_at=is.null");

            using var request = new HttpRequestMessage(
                HttpMethod.Patch,
                _baseUrl + RecordsEndpoint + "?" + string.Join("&", filters) +
                "&select=pi_number");
            AddAuth(request, bearer);
            request.Headers.Add("Prefer", "return=representation");
            request.Content = JsonContent(row);

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);
            return ParseReturnedPiNumbers(body).Contains(row.PiNumber);
        }

        /// <summary>Batch insert of rows the snapshot said are missing.
        /// A 409 means a concurrent producer inserted the same PI between
        /// snapshot and write — those rows are deferred to the next pass
        /// (where the fenced PATCH path handles them).</summary>
        private async Task<(int Inserted, int Skipped)> InsertAsync(
            IReadOnlyList<RecordPayload> rows, string bearer,
            CancellationToken cancellationToken)
        {
            var inserted = 0;
            var skipped = 0;

            foreach (var batch in Chunk(rows, InsertChunk))
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post, _baseUrl + RecordsEndpoint + "?select=pi_number");
                AddAuth(request, bearer);
                request.Headers.Add("Prefer", "return=representation");
                request.Content = JsonContent(batch);

                using var response = await _http
                    .SendAsync(request, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.Conflict)
                {
                    // Concurrent insert race — fall back to per-row so one
                    // colliding PI cannot sink the whole batch.
                    foreach (var row in batch)
                    {
                        using var single = new HttpRequestMessage(
                            HttpMethod.Post, _baseUrl + RecordsEndpoint + "?select=pi_number");
                        AddAuth(single, bearer);
                        single.Headers.Add("Prefer", "return=representation");
                        single.Content = JsonContent(new[] { row });

                        using var singleResponse = await _http
                            .SendAsync(single, cancellationToken).ConfigureAwait(false);

                        if (singleResponse.StatusCode == HttpStatusCode.Conflict)
                        {
                            skipped++; // raced — next pass observes and fences it
                            continue;
                        }

                        singleResponse.EnsureSuccessStatusCode();
                        var singleJson = await singleResponse.Content
                            .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                        if (ParseReturnedPiNumbers(singleJson).Contains(row.PiNumber))
                            inserted++;
                        else
                            skipped++; // no returned key → don't mark locally as synced
                    }
                    continue;
                }

                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content
                    .ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var returned = ParseReturnedPiNumbers(responseJson);
                foreach (var row in batch)
                {
                    if (returned.Contains(row.PiNumber)) inserted++;
                    else skipped++; // validate the representation before advancing local state
                }
            }

            return (inserted, skipped);
        }

        /// <summary>Soft-deactivate remote rows whose PI vanished locally —
        /// the proven end-of-sync RPC with its server-side empty-array
        /// guard; never physically deletes, exactly like the reference.</summary>
        private async Task ReconcileAsync(
            IReadOnlyList<string> activePiNumbers,
            string sourceFile, string sourceWorksheet,
            string bearer, CancellationToken cancellationToken)
        {
            if (activePiNumbers.Count == 0)
                return; // belt and braces on top of the server-side guard

            var body = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["p_source_file"] = sourceFile,
                ["p_source_worksheet"] = sourceWorksheet,
                ["p_active_pi_numbers"] = activePiNumbers.ToArray(),
            });

            using var request = new HttpRequestMessage(
                HttpMethod.Post, _baseUrl + ReconcileRpc);
            AddAuth(request, bearer);
            request.Headers.Add("Prefer", "return=minimal");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private void AddAuth(HttpRequestMessage request, string bearer) =>
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearer);

        private static string? GetNullableString(JsonElement el, string name)
        {
            if (!el.TryGetProperty(name, out var value) ||
                value.ValueKind == JsonValueKind.Null ||
                value.ValueKind == JsonValueKind.Undefined)
                return null;

            return value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : value.ToString();
        }

        private static HashSet<string> ParseReturnedPiNumbers(string json)
        {
            var result = new HashSet<string>(StringComparer.Ordinal);
            if (string.IsNullOrWhiteSpace(json))
                return result;

            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                throw new InvalidOperationException(
                    "Supabase returned a non-array representation for records.");

            foreach (var row in doc.RootElement.EnumerateArray())
            {
                if (row.TryGetProperty("pi_number", out var pi) &&
                    pi.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(pi.GetString()))
                    result.Add(pi.GetString()!);
            }
            return result;
        }

        private static StringContent JsonContent(object value) =>
            new(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json");

        private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> source, int size)
        {
            for (var i = 0; i < source.Count; i += size)
                yield return source.Skip(i).Take(size).ToList();
        }

        public void Dispose() => _http.Dispose();
    }
}
