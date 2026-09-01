// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AiProviderEvents.cs
// PROJECT: ProGlassAutomation
// ─────────────────────────────────────────────────────────────────────────
// Phase 3 — replaces the static App.AiProviderChanged / RaiseAiProviderChanged
// pair with an injectable event service. SettingsViewModel raises when the
// active AI provider is swapped; AiChatViewModel subscribes to refresh its
// provider display. Singleton in DI, so the subscription lives exactly as
// long as the app (same lifetime the static event had).
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.Services.AI
{
    public interface IAiProviderEvents
    {
        /// <summary>Raised after the active AI provider is swapped.</summary>
        event EventHandler Changed;

        /// <summary>Notifies all subscribers that the provider changed.</summary>
        void RaiseChanged();
    }

    public class AiProviderEvents : IAiProviderEvents
    {
        public event EventHandler? Changed;

        public void RaiseChanged() => Changed?.Invoke(null, EventArgs.Empty);
    }
}
