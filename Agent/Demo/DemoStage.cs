using System;

namespace ProGlassAutomation.Agent.Demo
{
    /// <summary>Live status from the running workflow back to the director.</summary>
    public static class DemoStage
    {
        public static Action<string>? Report;

        public static void Now(string message)
        {
            try { Report?.Invoke(message); } catch (System.Exception ex) { ProGlassAutomation.Services.Logging.AppLogService.Debug("[DemoStage] Now: UI must not break the walk: " + ex.Message); }
        }
    }
}
