using System.Collections.Generic;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Task categories for intelligent routing.</summary>
    public enum AITaskCategory
    {
        Conversation,
        VoiceCommand,
        ERPQuery,
        Reasoning,
        Coding,
        LongContext,
        DevelopmentAgent,
        ScreenUnderstanding
    }

    /// <summary>Intelligent model routing by task.</summary>
    public interface IAIModelRouter
    {
        IAIModelProvider Select(AITaskCategory category);
        IReadOnlyList<string> AvailableProviders { get; }
    }
}
