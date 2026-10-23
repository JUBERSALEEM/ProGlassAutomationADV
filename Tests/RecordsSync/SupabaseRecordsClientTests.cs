using System.Net;
using System.Text;
using System.Text.Json;
using ProGlassAutomation.Services.RecordsSync;
using Xunit;

namespace ProGlassAutomation.Tests.RecordsSync;

public sealed class SupabaseRecordsClientTests
{
    private const string ProjectUrl = "https://example-project.supabase.co";
    private const string ApiKey = "sb_publishable_unit_test_key";
    private const string AuthEmail = "records-sync@example.test";
    private const string AuthPassword = "unit-test-password-do-not-display";

    [Fact]
    public async Task TestReadConnection_AuthenticatesThenRequestsZeroRows()
    {
        var handler = new ScriptedHandler(
            JsonResponse(HttpStatusCode.OK, "{\"access_token\":\"test-user-token\"}"),
            JsonResponse(HttpStatusCode.OK, "[]"));
        using var client = new SupabaseRecordsClient(
            ProjectUrl, ApiKey, AuthEmail, AuthPassword, handler);

        await client.TestReadConnectionAsync();

        Assert.Equal(2, handler.Requests.Count);
        var auth = handler.Requests[0];
        Assert.Equal(HttpMethod.Post, auth.Method);
        Assert.Equal("/auth/v1/token?grant_type=password", auth.Uri.PathAndQuery);
        Assert.Equal(ApiKey, auth.ApiKey);
        Assert.Equal("application/json", auth.ContentType);
        Assert.Null(auth.Authorization);
        using (var payload = JsonDocument.Parse(auth.Body))
        {
            Assert.Equal(AuthEmail, payload.RootElement.GetProperty("email").GetString());
            Assert.Equal(AuthPassword, payload.RootElement.GetProperty("password").GetString());
        }

        var read = handler.Requests[1];
        Assert.Equal(HttpMethod.Get, read.Method);
        Assert.Equal("/rest/v1/records?select=pi_number&limit=0", read.Uri.PathAndQuery);
        Assert.Equal(ApiKey, read.ApiKey);
        Assert.Equal("Bearer test-user-token", read.Authorization);
    }

    [Fact]
    public void ClientRequiresDedicatedAuthCredentialsAndDoesNotAllowAnonBearerFallback()
    {
        Assert.Throws<ArgumentException>(() => new SupabaseRecordsClient(ProjectUrl, ApiKey));
        Assert.Throws<ArgumentException>(() => new SupabaseRecordsClient(
            ProjectUrl, ApiKey, AuthEmail, string.Empty));
    }

    [Fact]
    public void ApiKeyValidation_allowsPublishableAndLegacyAnon_butRejectsPrivilegedKeys()
    {
        Assert.True(SupabaseRecordsClient.IsPublishableOrAnonApiKey(ApiKey));
        Assert.True(SupabaseRecordsClient.IsPublishableOrAnonApiKey(
            LegacyAnonKey("example-project")));
        Assert.False(SupabaseRecordsClient.IsPublishableOrAnonApiKey("sb_secret_unit_test_key"));
        Assert.False(SupabaseRecordsClient.IsPublishableOrAnonApiKey(
            LegacyKey("service_role", "example-project")));
    }

    [Fact]
    public void LegacyAnonKey_validationDetectsProjectReferenceMismatch()
    {
        Assert.True(SupabaseRecordsClient.KeyHasNoProjectMismatch(
            LegacyAnonKey("example-project"), ProjectUrl));
        Assert.False(SupabaseRecordsClient.KeyHasNoProjectMismatch(
            LegacyAnonKey("another-project"), ProjectUrl));
        Assert.True(SupabaseRecordsClient.KeyHasNoProjectMismatch(ApiKey, ProjectUrl));
        Assert.True(SupabaseRecordsClient.KeyHasNoProjectMismatch(
            LegacyAnonKey("afpbdyamedpanwijxghk"), RecordsSyncService.ExpectedProjectUrl));
    }

    [Fact]
    public void ClientRejectsSecretServiceRoleAndMismatchedLegacyKeysBeforeSending()
    {
        Assert.Throws<ArgumentException>(() => new SupabaseRecordsClient(
            ProjectUrl, "sb_secret_unit_test_key", AuthEmail, AuthPassword, new ScriptedHandler()));
        Assert.Throws<ArgumentException>(() => new SupabaseRecordsClient(
            ProjectUrl, LegacyKey("service_role", "example-project"), AuthEmail, AuthPassword,
            new ScriptedHandler()));
        Assert.Throws<ArgumentException>(() => new SupabaseRecordsClient(
            ProjectUrl, LegacyAnonKey("another-project"), AuthEmail, AuthPassword,
            new ScriptedHandler()));
    }

    [Fact]
    public async Task TestReadConnection_ReportsSafeAuthErrorAndStopsBeforeRestRead()
    {
        var handler = new ScriptedHandler(JsonResponse(
            HttpStatusCode.BadRequest,
            JsonSerializer.Serialize(new
            {
                code = 400,
                error_code = "invalid_credentials",
                msg = "Invalid login credentials",
                details = $"Reflected input: {ApiKey} {AuthPassword}",
            })));
        using var client = new SupabaseRecordsClient(
            ProjectUrl, ApiKey, AuthEmail, AuthPassword, handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.TestReadConnectionAsync());

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode.GetValueOrDefault());
        Assert.Contains("Supabase Auth password sign-in failed", exception.Message);
        Assert.Contains("Invalid login credentials", exception.Message);
        Assert.Contains("invalid_credentials", exception.Message);
        Assert.Contains("Email provider", exception.Message);
        Assert.Contains("Host: example-project.supabase.co", exception.Message);
        Assert.Contains("endpoint: /auth/v1/token?grant_type=password", exception.Message);
        Assert.Contains("HTTP method: POST", exception.Message);
        Assert.Contains("apikey header added: true", exception.Message);
        Assert.Contains("Authorization header present: false", exception.Message);
        Assert.Contains("API key type: publishable", exception.Message);
        Assert.Contains("API key project reference: opaque publishable key", exception.Message);
        Assert.Contains("Content-Type: application/json", exception.Message);
        Assert.Contains("[redacted]", exception.Message);
        Assert.DoesNotContain(ApiKey, exception.Message);
        Assert.DoesNotContain(AuthPassword, exception.Message);
        Assert.Equal(1, handler.Requests.Count);
        Assert.Equal("/auth/v1/token?grant_type=password", handler.Requests[0].Uri.PathAndQuery);
    }

    [Fact]
    public async Task TestReadConnection_DistinguishesPostgrestFailureFromAuthFailure()
    {
        var handler = new ScriptedHandler(
            JsonResponse(HttpStatusCode.OK, "{\"access_token\":\"test-user-token\"}"),
            JsonResponse(HttpStatusCode.Forbidden,
                "{\"code\":\"42501\",\"message\":\"permission denied for table records\"}"));
        using var client = new SupabaseRecordsClient(
            ProjectUrl, ApiKey, AuthEmail, AuthPassword, handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.TestReadConnectionAsync());

        Assert.Contains("Supabase PI read-only check failed", exception.Message);
        Assert.Contains("permission denied for table records", exception.Message);
        Assert.Contains("SELECT access", exception.Message);
        Assert.Equal(2, handler.Requests.Count);
        Assert.Equal(HttpMethod.Get, handler.Requests[1].Method);
    }

    [Fact]
    public void ExtractErrorDetails_UsesStructuredDetailsAndBoundsLength()
    {
        var details = SupabaseRecordsClient.ExtractErrorDetails(
            "{\"message\":\"column does not exist\",\"code\":\"42703\",\"hint\":\"check schema\"}");

        Assert.Contains("column does not exist", details);
        Assert.Contains("42703", details);
        Assert.Contains("check schema", details);
        Assert.Equal(
            "Supabase returned a non-JSON error response.",
            SupabaseRecordsClient.ExtractErrorDetails("<html>gateway error</html>"));

        var longDetails = SupabaseRecordsClient.ExtractErrorDetails(
            "{\"message\":\"" + new string('x', 500) + "\"}");
        Assert.Equal(321, longDetails.Length);
        Assert.EndsWith("…", longDetails);
    }

    private static string LegacyAnonKey(string projectRef) => LegacyKey("anon", projectRef);

    private static string LegacyKey(string role, string projectRef)
    {
        var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes(
                "{\"role\":\"" + role + "\",\"ref\":\"" + projectRef + "\"}"))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return "header." + payload + ".signature";
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string body) =>
        new(status)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses;

        public ScriptedHandler(params HttpResponseMessage[] responses) =>
            _responses = new Queue<HttpResponseMessage>(responses);

        public List<CapturedRequest> Requests { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content == null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            var apiKey = request.Headers.TryGetValues("apikey", out var keyValues)
                ? keyValues.Single()
                : null;
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri!,
                apiKey,
                request.Headers.Authorization?.ToString(),
                request.Content?.Headers.ContentType?.MediaType,
                body));

            if (_responses.Count == 0)
                throw new InvalidOperationException("The test made more requests than expected.");
            return _responses.Dequeue();
        }
    }

    private sealed record CapturedRequest(
        HttpMethod Method,
        Uri Uri,
        string? ApiKey,
        string? Authorization,
        string? ContentType,
        string Body);
}
