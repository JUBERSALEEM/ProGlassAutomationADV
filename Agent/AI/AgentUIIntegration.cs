using System;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Professional assistant UI integration guidelines (not overlay design).</summary>
    public static class AgentUIIntegration
    {
        /// <summary>Professional assistant modes.</summary>
        public enum AssistantMode
        {
            AssistantPanel,
            VoiceOnly,
            AgentTasks,
            Activity,
            Automation,
            DeveloperMode,
            Settings
        }

        /// <summary>Returns mode-appropriate label for UI.</summary>
        public static string ModeLabel(AssistantMode mode) => mode switch
        {
            AssistantMode.AssistantPanel => "AI Assistant",
            AssistantMode.VoiceOnly => "Voice Command",
            AssistantMode.AgentTasks => "Agent Tasks",
            AssistantMode.Activity => "Activity Log",
            AssistantMode.Automation => "Automation",
            AssistantMode.DeveloperMode => "Development Agent",
            AssistantMode.Settings => "AI Settings",
            _ => "AI"
        };

        /// <summary>Returns whether the mode requires the agent to be active.</summary>
        public static bool RequiresAgent(AssistantMode mode) => mode switch
        {
            AssistantMode.AssistantPanel => true,
            AssistantMode.VoiceOnly => true,
            AssistantMode.AgentTasks => true,
            AssistantMode.Activity => false,
            AssistantMode.Automation => true,
            AssistantMode.DeveloperMode => true,
            AssistantMode.Settings => false,
            _ => false
        };
    }
}
