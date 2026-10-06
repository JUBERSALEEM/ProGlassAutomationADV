using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ProGlassAutomation.Services.RecordsSync
{
    /// <summary>
    /// Thin PostgREST transport for public.records — the ONLY network code in
    /// the sync. No Supabase client library is introduced: the REST surface
    /// the website itself uses is plain HTTPS + JSON, and taking a dependency
    /// for it would violate "do not introduce unnecessary dependencies".
    ///
    /// Write strategy — select-then-upsert, because the table has no unique
    /// constraint on (source_file, source_worksheet, pi_number) that a
    /// PostgREST on_conflict merge could target:
    ///   1. GET the ids of the batch's pi_numbers within our source scope.
    ///   2. PATCH changed rows by id; POST the ones that do not exist yet.
    ///   3. RPC reconcile_records_active_state soft-deactivates remote rows
    ///      whose PI no longer exists locally — the exact proven
    ///      Windowfilehost reconciliation, server-side guarded against
    ///      empty-array mass deactivation.
    ///
    /// Credentials: caller passes the key (from SecretVault). It is used as
    /// both apikey and Bearer token, matching how every Supabase tool
    /// authenticates. Nothing is logged; nothing is persisted here.
    ///
    /// All methods throw on failure — the CALLER owns retry/backoff policy,
    /// so transport faults have exactly one handling path.
    /// </summary>
    public sealed class SupabaseRecordsClient : IDisposable
    {
        private const string RestBase = "/rest/v1/records";
        private const string ReconcileRpc = "/rest/v1/rpc/reconcile_records_active_state";
        private const int BatchChunk = 100; // pi_numbers per GET/POST/PATCH round

        private readonly HttpClient _http;
        private readonly string _baseUrl;
        private readonly string _apiKey;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.Never,
        };

        public SupabaseRecordsClient(string supabaseUrl, string apiKey)
        {
            _baseUrl = (supabaseUrl ?? "").TrimEnd('/');
            _apiKey = apiKey ?? "";
            if (_baseUrl.Length == 0 || _apiKey.Length == 0)
                throw new ArgumentException("Supabase URL and key are both required.");

            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
            _http.DefaultRequestHeaders.Add("apikey", _apiKey);
            _http.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", _apiKey);
        }

        /// <summary>
        /// Sends one synchronization pass. Returns the number of rows
        /// actually written (inserted + updated). Idempotent: re-sending the
        /// same rows PATCHes them to identical values.
        /// </summary>
        public async Task<int> SyncAsync(
            IReadOnlyList<RecordPayload> rows,
            string sourceFile,
            string sourceWorksheet,
            CancellationToken cancellationToken = default)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var written = 0;
            foreach (var chunk in Chunk(rows, BatchChunk))
            {
                var existing = await FetchExistingIdsAsync(
                    chunk, sourceFile, sourceWorksheet, cancellationToken)
                    .ConfigureAwait(false);

                var toInsert = new List<RecordPayload>();
                foreach (var row in chunk)
                {
                    if (existing.TryGetValue(row.PiNumber, out var id))
                    {
                        await PatchAsync(id, row, cancellationToken).ConfigureAwait(false);
                        written++;
                    }
                    else
                    {
                        toInsert.Add(row);
                    }
                }

                if (toInsert.Count > 0)
                {
                    await PostAsync(toInsert, cancellationToken).ConfigureAwait(false);
                    written += toInsert.Count;
                }
            }

            await ReconcileAsync(rows, sourceFile, sourceWorksheet, cancellationToken)
                .ConfigureAwait(false);

            return written;
        }

        /// <summary>pi_number → record id for the pi_numbers that already
        /// exist inside our own source scope. Scoped strictly: rows from
        /// Excel workbooks are never seen, never touched.</summary>
        private async Task<Dictionary<string, long>> FetchExistingIdsAsync(
            IReadOnlyList<RecordPayload> chunk,
            string sourceFile,
            string sourceWorksheet,
            CancellationToken cancellationToken)
        {
            var inList = string.Join(",",
                chunk.Select(r => "\"" + r.PiNumber.Replace("\"", "") + "\""));

            var url = _baseUrl + RestBase
                + "?select=id,pi_number"
                + "&source_file=eq." + Uri.EscapeDataString(sourceFile)
                + "&source_worksheet=eq." + Uri.EscapeDataString(sourceWorksheet)
                + "&pi_number=in.(" + Uri.EscapeDataString(inList) + ")";

            using var response = await _http
                .GetAsync(url, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken)
                .ConfigureAwait(false);

            var result = new Dictionary<string, long>(StringComparer.Ordinal);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return result;

            foreach (var el in doc.RootElement.EnumerateArray())
            {
                if (!el.TryGetProperty("pi_number", out var piEl)) continue;
                var pi = piEl.GetString();
                if (string.IsNullOrEmpty(pi)) continue;
                if (el.TryGetProperty("id", out var idEl) &&
                    idEl.ValueKind == JsonValueKind.Number &&
                    idEl.TryGetInt64(out var id))
                {
                    result[pi] = id; // last wins — duplicate rows (if any
                                     // pre-existed) converge on one target
                }
            }
            return result;
        }

        private async Task PatchAsync(
            long id, RecordPayload row, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Patch, _baseUrl + RestBase + "?id=eq." + id);
            AddWriteHeaders(request);
            request.Content = JsonContent(row);

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private async Task PostAsync(
            IReadOnlyList<RecordPayload> rows, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post, _baseUrl + RestBase);
            AddWriteHeaders(request);
            request.Content = JsonContent(rows);

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        /// <summary>Soft-deactivate remote rows whose PI vanished locally.
        /// Mirrors Windowfilehost's proven end-of-sync reconciliation and
        /// relies on the RPC's own empty-array safety guard.</summary>
        private async Task ReconcileAsync(
            IReadOnlyList<RecordPayload> rows,
            string sourceFile,
            string sourceWorksheet,
            CancellationToken cancellationToken)
        {
            if (rows.Count == 0)
                return; // never call with an empty set — belt and braces on
                        // top of the server-side guard

            var body = JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["p_source_file"] = sourceFile,
                ["p_source_worksheet"] = sourceWorksheet,
                ["p_active_pi_numbers"] = rows.Select(r => r.PiNumber).ToArray(),
            });

            using var request = new HttpRequestMessage(
                HttpMethod.Post, _baseUrl + ReconcileRpc);
            request.Headers.Add("Prefer", "return=minimal");
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await _http
                .SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();
        }

        private static void AddWriteHeaders(HttpRequestMessage request) =>
            request.Headers.Add("Prefer", "return=minimal");

        private static StringContent JsonContent(object value) =>
            new(JsonSerializer.Serialize(value, JsonOptions), Encoding.UTF8, "application/json");

        private static IEnumerable<IReadOnlyList<T>> Chunk<T>(IReadOnlyList<T> source, int size)
        {
            for (var i = 0; i < source.Count; i += size)
                yield return source.Skip(i).Take(size).ToList();
        }

        public void Dispose() => _http.Dispose();
    }
}
