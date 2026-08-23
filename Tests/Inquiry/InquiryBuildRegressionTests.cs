using ProGlassAutomation.Services.Database.SchemaMigrations;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    public class InquiryBuildRegressionTests
    {
        [Fact]
        public void Proforma_inquiry_link_migration_uses_reserved_upgrade_version()
        {
            var migration = Assert.Single(AppSchemaMigrations.ProformaInvoices);
            Assert.True(migration.Version >= 1000,
                "Must upgrade databases whose legacy PRAGMA user_version is already above 1.");
        }
    }
}
