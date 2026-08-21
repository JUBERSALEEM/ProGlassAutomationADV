using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.AI.Tests
{
    /// <summary>Sample integration test fixtures for the unified AI platform.</summary>
    public static class AgentTestFixtures
    {
        /// <summary>Creates a mock provider for testing.</summary>
        public static MockAIProvider CreateMockProvider() => new MockAIProvider();

        /// <summary>Verifies regression checklist exists for given phase.</summary>
        public static bool ChecklistExists(int phase) => AIRegressionChecklist.GetPhaseVerificationItems(phase).Count > 0;

        /// <summary>Sample test: mock provider responds correctly.</summary>
        public static System.Collections.Generic.IReadOnlyList<string> SampleVerificationItems() =>
            new List<string> { "Mock provider responds", "Router selects provider", "Orchestrator integrates" }.AsReadOnly();
    }
}
