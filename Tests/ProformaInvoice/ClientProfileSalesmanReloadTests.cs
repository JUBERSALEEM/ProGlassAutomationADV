// ═══════════════════════════════════════════════════════════════════════
// FILE: Tests/ProformaInvoice/ClientProfileSalesmanReloadTests.cs
// PROJECT: ProGlassAutomation.Tests
//
// Regression: "Step 2 fields blank on reopen" for the Sales Representative.
//
// SalesRepresentative binds to an EDITABLE ComboBox whose ItemsSource is
// SalesmanOptions. On reopen the wizard RESTORES the value first, then
// calls LoadSalesmanOptionsAsync() which clears + repopulates the list.
// Swapping an editable ComboBox's ItemsSource resets its edit text and can
// push an empty string back through the TwoWay binding, wiping the value —
// which the next save then persists as blank.
//
// These tests pin the VM-level guard that preserves SalesRepresentative
// across the options rebuild (no UI needed — the VM owns the value).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Models.MasterData;
using ProGlassAutomation.Services.MasterData.Repository;
using ProGlassAutomation.ViewModels.ProformaInvoice.Step2ClientProfile;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProGlassAutomation.Tests.ProformaInvoice
{
    public class ClientProfileSalesmanReloadTests
    {
        [Fact]
        public async Task SalesRepresentative_survives_options_reload_when_already_in_master_data()
        {
            var repo = new FakeSalesmenRepo("Nabil Ahmed", "Sara Khan");
            var vm = new ClientProfileViewModel(repo)
            {
                // Simulates the value restored from a saved invoice snapshot.
                SalesRepresentative = "Sara Khan"
            };

            // This is what the reopen flow runs AFTER restore.
            await vm.LoadSalesmanOptionsAsync();

            Assert.Equal("Sara Khan", vm.SalesRepresentative);
            Assert.Contains("Sara Khan", vm.SalesmanOptions);
        }

        [Fact]
        public async Task SalesRepresentative_survives_reload_when_not_in_master_data()
        {
            // A salesman typed on THIS invoice but never saved to master data.
            var repo = new FakeSalesmenRepo("Someone Else");
            var vm = new ClientProfileViewModel(repo)
            {
                SalesRepresentative = "Freshly Typed Name"
            };

            await vm.LoadSalesmanOptionsAsync();

            Assert.Equal("Freshly Typed Name", vm.SalesRepresentative);
            // The current value is offered by the list so the editable
            // ComboBox can display it as a proper item.
            Assert.Contains("Freshly Typed Name", vm.SalesmanOptions);
        }

        [Fact]
        public async Task Empty_SalesRepresentative_stays_empty_after_reload()
        {
            var repo = new FakeSalesmenRepo("Nabil Ahmed");
            var vm = new ClientProfileViewModel(repo);

            await vm.LoadSalesmanOptionsAsync();

            Assert.True(string.IsNullOrEmpty(vm.SalesRepresentative));
            Assert.Contains("Nabil Ahmed", vm.SalesmanOptions);
        }

        // ── Minimal fake: only salesmen matter for these tests ──────────
        private sealed class FakeSalesmenRepo : IMasterDataRepository
        {
            private readonly List<SavedSalesman> _salesmen;

            public FakeSalesmenRepo(params string[] names) =>
                _salesmen = names.Select(n => new SavedSalesman { Name = n }).ToList();

            public Task<List<SavedSalesman>> GetAllSalesmenAsync() =>
                Task.FromResult(_salesmen.ToList());

            // ── Everything else is unused here ──────────────────────────
            public Task InitializeAsync() => Task.CompletedTask;
            public Task<SavedCompany> SaveOrUpdateCompanyAsync(
                SavedCompany company, bool preserveUnsetProfileFields = false) =>
                Task.FromResult(company);
            public Task<List<SavedCompany>> GetAllCompaniesAsync() =>
                Task.FromResult(new List<SavedCompany>());
            public Task<List<SavedCompany>> SearchCompaniesAsync(string query) =>
                Task.FromResult(new List<SavedCompany>());
            public Task<SavedCompany?> FindCompanyByNameAsync(string companyName) =>
                Task.FromResult<SavedCompany?>(null);
            public Task<SavedClient> SaveOrUpdateClientAsync(SavedClient client) =>
                Task.FromResult(client);
            public Task<List<SavedClient>> GetAllClientsAsync() =>
                Task.FromResult(new List<SavedClient>());
            public Task<List<SavedClient>> SearchClientsAsync(string query) =>
                Task.FromResult(new List<SavedClient>());
            public Task<SavedClient?> FindClientByNameAsync(string customerName) =>
                Task.FromResult<SavedClient?>(null);
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
            public Task<List<SavedSalesman>> SearchSalesmenAsync(string query) =>
                Task.FromResult(new List<SavedSalesman>());
            public Task DeleteSalesmanAsync(int id) => Task.CompletedTask;
            public Task DeleteClientAsync(int id) => Task.CompletedTask;
            public Task DeleteCompanyAsync(int id) => Task.CompletedTask;
            public Task DeleteProjectAsync(int id) => Task.CompletedTask;
            public Task AutoSaveFromInvoiceAsync(
                string companyName, string companyTrn, string companyLocation,
                string customerName, string customerTrn, string customerReferenceCode,
                string salesRepresentative, string documentPreparedBy, string billingAddress,
                string attentionExecutiveName, string clientContactPhone,
                string projectIdentifierName, string projectContractNo, string projectReference,
                string siteDeliveryLocation, string lpoNumber,
                string? companyLogoPath = null, string? companyRepresentative = null,
                string? companyRepresentativeMobile = null, string? companyEmail = null,
                string? companyAccountNumber = null, string? companyBankName = null,
                CompanyLogoLayout companyLogoLayout = CompanyLogoLayout.None,
                double companyLogoWidthMm = 0, double companyLogoHeightMm = 0) =>
                Task.CompletedTask;
        }
    }
}
