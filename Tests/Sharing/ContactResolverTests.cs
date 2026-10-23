// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/Sharing/ContactResolverTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Pins TO-resolution: preference order (attention → customer →
// company rep → salesman), direct-channel priority, MasterData
// gap-fill via an injected fake (never the live database), attention
// dedupe, and the sender's non-blank guarantee.
//
// NOTE: the sender assertion is deliberately name-only —
// AuditService.CurrentUser is static/shared, so the live-user versus
// prepared-by branch depends on ambient test state.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.Services.Sharing;
using ProGlassAutomation.Services.Sharing.Contacts;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.Sharing
{
    public class ContactResolverTests
    {
        private sealed class FakeMasterData : IMasterDataRepository
        {
            public SavedClient? ClientToReturn { get; set; }
            public SavedCompany? CompanyToReturn { get; set; }

            public Task<SavedCompany> SaveOrUpdateCompanyAsync(
                SavedCompany company, bool preserveUnsetProfileFields = false) =>
                Task.FromResult(company);

            public Task<List<SavedCompany>> GetAllCompaniesAsync() =>
                Task.FromResult(new List<SavedCompany>());

            public Task<List<SavedCompany>> SearchCompaniesAsync(string query) =>
                Task.FromResult(new List<SavedCompany>());

            public Task<SavedCompany?> FindCompanyByNameAsync(string companyName) =>
                Task.FromResult(CompanyToReturn);

            public Task<SavedClient> SaveOrUpdateClientAsync(SavedClient client) =>
                Task.FromResult(client);

            public Task<List<SavedClient>> GetAllClientsAsync() =>
                Task.FromResult(new List<SavedClient>());

            public Task<List<SavedClient>> SearchClientsAsync(string query) =>
                Task.FromResult(new List<SavedClient>());

            public Task<SavedClient?> FindClientByNameAsync(string customerName) =>
                Task.FromResult(ClientToReturn);

            public Task<SavedProject> SaveOrUpdateProjectAsync(SavedProject project) =>
                Task.FromResult(project);

            public Task<List<SavedProject>> GetAllProjectsAsync() =>
                Task.FromResult(new List<SavedProject>());

            public Task<List<SavedProject>> SearchProjectsAsync(string query) =>
                Task.FromResult(new List<SavedProject>());

            public Task<List<SavedProject>> GetProjectsByCustomerAsync(string customerName) =>
                Task.FromResult(new List<SavedProject>());

            public Task<SavedProject?> FindProjectByNameAsync(
                string projectName, string customerName) =>
                Task.FromResult<SavedProject?>(null);

            public Task<SavedSalesman> SaveOrUpdateSalesmanAsync(SavedSalesman salesman) =>
                Task.FromResult(salesman);

            public Task<List<SavedSalesman>> GetAllSalesmenAsync() =>
                Task.FromResult(new List<SavedSalesman>());

            public Task<List<SavedSalesman>> SearchSalesmenAsync(string query) =>
                Task.FromResult(new List<SavedSalesman>());

            public Task InitializeAsync() => Task.CompletedTask;
            public Task DeleteCompanyAsync(int id) => Task.CompletedTask;
            public Task DeleteClientAsync(int id) => Task.CompletedTask;
            public Task DeleteProjectAsync(int id) => Task.CompletedTask;
            public Task DeleteSalesmanAsync(int id) => Task.CompletedTask;
            public Task AutoSaveFromInvoiceAsync(string companyName, string companyTrn, string companyLocation, string customerName, string customerTrn, string customerReferenceCode, string salesRepresentative, string documentPreparedBy, string billingAddress, string attentionExecutiveName, string clientContactPhone, string projectIdentifierName, string projectContractNo, string projectReference, string siteDeliveryLocation, string lpoNumber, string? companyLogoPath = null, string? companyRepresentative = null, string? companyRepresentativeMobile = null, string? companyEmail = null, string? companyAccountNumber = null, string? companyBankName = null, CompanyLogoLayout companyLogoLayout = CompanyLogoLayout.None, double companyLogoWidthMm = 0, double companyLogoHeightMm = 0, string? companyTelephone = null, string? companyWebsite = null) => Task.CompletedTask;
        }

        [Fact]
        public async Task Recipients_prefer_attention_then_customer()
        {
            var resolver = new ContactResolver(new FakeMasterData());
            var context = new ShareContext
            {
                CustomerName = "Skyline Interiors LLC",
                AttentionName = "Mr. Ahmed",
                CustomerPhone = "050 123 4567",
                CustomerEmail = "ahmed@skyline.example"
            };

            List<ShareRecipient> recipients =
                await resolver.ResolveRecipientsAsync(context);

            Assert.Equal(2, recipients.Count);
            Assert.Equal(ShareRecipientKind.CustomerAttention,
                recipients[0].Kind);
            Assert.Equal("Mr. Ahmed", recipients[0].Name);
            Assert.Equal("050 123 4567", recipients[0].Phone);
            Assert.Equal("ahmed@skyline.example", recipients[0].Email);
            Assert.Equal(ShareRecipientKind.Customer, recipients[1].Kind);
            Assert.Equal("ahmed@skyline.example", recipients[1].Email);
        }

        [Fact]
        public async Task Recipients_gapfill_attention_and_phone_from_master_data()
        {
            var fake = new FakeMasterData
            {
                ClientToReturn = new SavedClient
                {
                    CustomerName = "Skyline Interiors LLC",
                    AttentionExecutiveName = "Ms. Sara",
                    ClientContactPhone = "055 999 1122"
                }
            };
            var resolver = new ContactResolver(fake);
            var context = new ShareContext
            {
                CustomerName = "Skyline Interiors LLC"
            };

            List<ShareRecipient> recipients =
                await resolver.ResolveRecipientsAsync(context);

            var attention = recipients.FirstOrDefault(
                r => r.Kind == ShareRecipientKind.CustomerAttention);
            Assert.NotNull(attention);
            Assert.Equal("Ms. Sara", attention!.Name);
            Assert.Equal("055 999 1122", attention.Phone);
            Assert.Contains(recipients,
                r => r.Kind == ShareRecipientKind.Customer
                    && r.Phone == "055 999 1122");
        }

        [Fact]
        public async Task Recipients_gapfill_company_rep_from_master_data()
        {
            var fake = new FakeMasterData
            {
                CompanyToReturn = new SavedCompany
                {
                    CompanyName = "ProGlass LLC",
                    Representative = "Juber Saleem",
                    RepresentativeMobile = "052 111 2233",
                    CompanyEmail = "ops@proglass.example"
                }
            };
            var resolver = new ContactResolver(fake);
            var context = new ShareContext
            {
                CustomerName = "Skyline Interiors LLC",
                CompanyName = "ProGlass LLC"
            };

            List<ShareRecipient> recipients =
                await resolver.ResolveRecipientsAsync(context);

            var rep = recipients.FirstOrDefault(
                r => r.Kind == ShareRecipientKind.CompanyRepresentative);
            Assert.NotNull(rep);
            Assert.Equal("Juber Saleem", rep!.Name);
            Assert.Equal("052 111 2233", rep.Phone);
            Assert.Equal("ops@proglass.example", rep.Email);
        }

        [Fact]
        public async Task Recipients_skip_attention_duplicating_customer()
        {
            var resolver = new ContactResolver(new FakeMasterData());
            var context = new ShareContext
            {
                CustomerName = "Skyline Interiors LLC",
                AttentionName = "skyline interiors llc",
                CustomerPhone = "050 123 4567"
            };

            List<ShareRecipient> recipients =
                await resolver.ResolveRecipientsAsync(context);

            Assert.Single(recipients);
            Assert.Equal(ShareRecipientKind.Customer, recipients[0].Kind);
        }

        [Fact]
        public async Task Recipients_include_name_only_salesman_last()
        {
            var resolver = new ContactResolver(new FakeMasterData());
            var context = new ShareContext
            {
                CustomerName = "Skyline Interiors LLC",
                SalesmanName = "Ravi"
            };

            List<ShareRecipient> recipients =
                await resolver.ResolveRecipientsAsync(context);

            var salesman = recipients.FirstOrDefault(
                r => r.Kind == ShareRecipientKind.Salesman);
            Assert.NotNull(salesman);
            Assert.Equal("Ravi", salesman!.Name);
            Assert.Equal("", salesman.Phone);
            Assert.Equal(recipients.Count - 1,
                recipients.IndexOf(salesman!));
        }

        [Fact]
        public async Task Recipients_never_throw_and_yield_empty_for_blank_context()
        {
            var resolver = new ContactResolver(new FakeMasterData());

            List<ShareRecipient> recipients =
                await resolver.ResolveRecipientsAsync(new ShareContext());

            Assert.NotNull(recipients);
            Assert.Empty(recipients);
        }

        [Fact]
        public void Sender_always_has_a_display_name()
        {
            var resolver = new ContactResolver(new FakeMasterData());
            var context = new ShareContext { PreparedByName = "Juber" };

            ShareSender sender = resolver.ResolveSender(context);

            Assert.NotNull(sender);
            Assert.False(string.IsNullOrWhiteSpace(sender.Name));
        }
    }
}
