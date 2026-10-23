using ProGlassAutomation.Services.RecordsSync;
using Xunit;

namespace ProGlassAutomation.Tests.RecordsSync
{
    public sealed class RecordsSyncCredentialBindingTests
    {
        private const string ProjectUrl = "https://afpbdyamedpanwijxghk.supabase.co";
        private const string AuthEmail = "records-sync@example.com";

        [Fact]
        public void ExpectedProjectUrl_matches_requested_production_project_and_rejects_other_projects()
        {
            Assert.Equal(ProjectUrl, RecordsSyncService.ExpectedProjectUrl);
            Assert.True(RecordsSyncService.IsExpectedProjectUrl("  " + ProjectUrl + "/  "));
            Assert.False(RecordsSyncService.IsExpectedProjectUrl(
                "https://another-project.supabase.co"));
        }

        [Fact]
        public void Missing_binding_does_not_reuse_legacy_saved_credentials()
        {
            var settings = new RecordsSyncSettings
            {
                SupabaseUrl = ProjectUrl,
                AuthEmail = AuthEmail,
            };

            Assert.False(RecordsSyncService.CredentialBindingMatches(
                settings, ProjectUrl, AuthEmail));
        }

        [Fact]
        public void Matching_project_and_email_accepts_saved_credential_binding()
        {
            var settings = BoundSettings();

            Assert.True(RecordsSyncService.CredentialBindingMatches(
                settings, ProjectUrl + "/", "  RECORDS-SYNC@example.com  "));
        }

        [Theory]
        [InlineData("https://another-project.supabase.co", AuthEmail)]
        [InlineData(ProjectUrl, "another-user@example.com")]
        public void Changed_project_or_email_invalidates_saved_credential_binding(
            string currentProjectUrl,
            string currentAuthEmail)
        {
            Assert.False(RecordsSyncService.CredentialBindingMatches(
                BoundSettings(), currentProjectUrl, currentAuthEmail));
        }

        private static RecordsSyncSettings BoundSettings() => new()
        {
            SupabaseUrl = ProjectUrl,
            AuthEmail = AuthEmail,
            CredentialBindingProjectUrl = ProjectUrl,
            CredentialBindingAuthEmail = AuthEmail,
        };
    }
}
