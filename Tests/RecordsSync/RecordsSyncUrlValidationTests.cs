using ProGlassAutomation.Services.RecordsSync;
using Xunit;

namespace ProGlassAutomation.Tests.RecordsSync
{
    public sealed class RecordsSyncUrlValidationTests
    {
        [Theory]
        [InlineData("https://project.supabase.co/", "https://project.supabase.co")]
        [InlineData("  https://project.supabase.co  ", "https://project.supabase.co")]
        [InlineData("http://localhost:54321/", "http://localhost:54321")]
        [InlineData("http://127.0.0.1:54321/", "http://127.0.0.1:54321")]
        public void NormalizeProjectUrl_accepts_root_https_and_loopback_dev_urls(
            string input,
            string expected)
        {
            Assert.Equal(expected, RecordsSyncService.NormalizeProjectUrl(input));
        }

        [Theory]
        [InlineData("https://project.supabase.co/rest/v1/records")]
        [InlineData("https://user:password@project.supabase.co")]
        [InlineData("https://project.supabase.co?apikey=secret")]
        [InlineData("http://example.com")]
        [InlineData("not-a-url")]
        public void NormalizeProjectUrl_rejects_paths_credentials_queries_and_non_loopback_http(
            string input)
        {
            Assert.Throws<ArgumentException>(
                () => RecordsSyncService.NormalizeProjectUrl(input));
        }
    }
}
