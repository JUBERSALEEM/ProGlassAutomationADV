using ProGlassAutomation.Services.Database.SchemaMigrations;
using Xunit;

namespace ProGlassAutomation.Tests.Inquiry
{
    public class InquiryBuildRegressionTests
    {
        [Fact]
        public void Proforma_invoice_migrations_use_reserved_increasing_upgrade_versions()
        {
            Assert.NotEmpty(AppSchemaMigrations.ProformaInvoices);
            int previous = 0;
            foreach (var migration in AppSchemaMigrations.ProformaInvoices)
            {
                Assert.True(migration.Version > previous,
                    $"Proforma Invoice migration versions must strictly increase (previous {previous}, got {migration.Version}).");
                previous = migration.Version;
            }
            Assert.True(previous >= 1000,
                "Must upgrade databases whose legacy PRAGMA user_version is already above 1.");
        }

        [Fact]
        public void Inquiry_convergence_migration_is_registered_with_strictly_increasing_reserved_versions()
        {
            // SQLite error 1 "no such column i.InquiryValue": legacy databases
            // created by interim builds must converge via a registered
            // migration — EnsureCreated never alters an existing database.
            Assert.NotEmpty(AppSchemaMigrations.Inquiries);

            int previous = 0;
            foreach (var migration in AppSchemaMigrations.Inquiries)
            {
                Assert.True(migration.Version > previous,
                    $"Inquiry migration versions must strictly increase (previous {previous}, got {migration.Version}).");
                previous = migration.Version;
            }
            Assert.True(previous >= 1000,
                "Must upgrade databases whose legacy PRAGMA user_version is already above 1.");
        }
    }
}
