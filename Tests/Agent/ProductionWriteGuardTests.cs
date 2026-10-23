using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class ProductionWriteGuardTests
    {
        [Fact]
        public void Production_write_guard_requires_identity_permission_and_writable_license()
        {
            string source = ReadRepoFile("Services/Production/ProductionWriteGuard.cs");

            Assert.Contains("OperationGuard.Demand(AppPermissions.ProductionEdit", source);
            Assert.Contains("ReadOnlyGate.Demand(operation)", source);
        }

        [Theory]
        [InlineData("public override int SaveChanges(bool acceptAllChangesOnSuccess)")]
        [InlineData("public override Task<int> SaveChangesAsync(\n            bool acceptAllChangesOnSuccess,")]
        public void Production_db_context_commit_overloads_demand_the_write_guard(string signature)
        {
            string source = ReadRepoFile("Services/Production/ProductionDbContext.cs");
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.True(start >= 0, $"Missing production write boundary: {signature}");

            int nextMethod = source.IndexOf("public override", start + signature.Length,
                StringComparison.Ordinal);
            int end = nextMethod >= 0 ? nextMethod : source.Length;
            string method = source.Substring(start, end - start);
            Assert.Contains("ProductionWriteGuard.Demand(", method);
        }

        [Fact]
        public void Convenience_save_overloads_forward_to_guarded_commit_overloads()
        {
            string source = ReadRepoFile("Services/Production/ProductionDbContext.cs");
            Assert.Contains("SaveChanges(acceptAllChangesOnSuccess: true)", source);
            Assert.Contains("SaveChangesAsync(true, cancellationToken)", source);
        }

        [Fact]
        public void Public_production_write_workflows_check_the_guard_before_mutating()
        {
            string repository = ReadRepoFile("Services/Production/ProductionRepository.cs");
            string releaseService = ReadRepoFile("Services/Production/ProductionReleaseService.cs");

            AssertMethodContains(repository, "ReleaseAsync",
                "ProductionWriteGuard.Demand(");
            AssertMethodContains(repository, "SetDoneQtyAsync",
                "ProductionWriteGuard.Demand(");
            AssertMethodContains(repository, "SetDoneQtyBatchAsync",
                "ProductionWriteGuard.Demand(");
            AssertMethodContains(releaseService, "ReleaseAsync",
                "ProductionWriteGuard.Demand(");
        }

        private static void AssertMethodContains(
            string source, string methodName, string requiredText)
        {
            var root = CSharpSyntaxTree.ParseText(source).GetRoot();
            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(candidate =>
                    string.Equals(candidate.Identifier.ValueText, methodName,
                        StringComparison.Ordinal));
            Assert.NotNull(method);
            Assert.Contains(requiredText, method!.ToFullString());
        }

        private static string ReadRepoFile(string relativePath)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                var candidate = Path.Combine(directory.FullName, "ProGlassAutomation.csproj");
                if (File.Exists(candidate))
                    return File.ReadAllText(Path.Combine(directory.FullName, relativePath));
                directory = directory.Parent;
            }

            throw new InvalidOperationException(
                "Could not locate repository root from " + AppContext.BaseDirectory);
        }
    }
}
