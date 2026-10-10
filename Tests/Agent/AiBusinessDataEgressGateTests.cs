using System.IO;
using System.Threading.Tasks;
using ProGlassAutomation.Services.AI.AIContext;
using Xunit;

namespace ProGlassAutomation.Tests.Agent;

public sealed class AiBusinessDataEgressGateTests
{
    [Fact]
    public async Task Disabled_business_data_sharing_returns_before_app_or_database_access()
    {
        string context = await AiContextBuilder.BuildContextAsync(
            allowRealBusinessData: false);

        Assert.Equal(string.Empty, context);
    }

    [Fact]
    public void Cloud_chat_uses_opt_in_context_and_does_not_attach_learning_or_audit_context()
    {
        string root = LocateProjectRoot();
        string viewModel = File.ReadAllText(Path.Combine(root,
            "ViewModels", "AI", "AiChatViewModel.Part1.cs"));
        string builder = File.ReadAllText(Path.Combine(root,
            "Services", "AI", "AIContext", "AiContextBuilder.cs"));

        Assert.Contains("AiContextBuilder.BuildContextAsync(", viewModel);
        Assert.Contains("_config?.Ai?.UseRealData == true", viewModel);
        Assert.DoesNotContain("BuildLearningContextAsync", viewModel);
        Assert.DoesNotContain("BuildMemoryContextAsync", viewModel);
        Assert.DoesNotContain("BuildLearningContextAsync", builder);
        Assert.DoesNotContain("Audit.AuditService.GetRecentAsync", builder);
    }

    private static string LocateProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var probe = Path.Combine(dir.FullName,
                "ProGlassAutomation.csproj");
            if (File.Exists(probe)) return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            "Could not locate the project root above " + AppContext.BaseDirectory);
    }
}
