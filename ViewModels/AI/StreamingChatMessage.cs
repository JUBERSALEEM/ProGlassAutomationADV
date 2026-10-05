// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/AI/StreamingChatMessage.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Chat — live streaming bubble (v3 JARVIS pass)
//
// ChatMessage is intentionally IMMUTABLE (init-only POCO) — perfect for
// finished messages, but a streaming answer needs live Text updates.
// This additive class backs the live bubble shown while tokens arrive.
// When streaming finishes the VM converts the final text into a normal
// immutable ChatMessage and clears this one.
//
// Exposes the SAME display property names the message DataTemplate
// binds (Text, IsUser, IsError, IsAiResponse, ProviderName,
// ProviderIcon, TimeLabel) so the AI bubble style renders it.
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.ViewModels.AI
{
    /// <summary>
    /// Mutable AI message bubble updated token-by-token during streaming.
    /// </summary>
    public sealed class StreamingChatMessage : ViewModelBase
    {
        private string _text = string.Empty;
        private bool _isStreaming = true;

        /// <summary>Live-updating answer text.</summary>
        public string Text
        {
            get => _text;
            set { _text = value ?? string.Empty; OnPropertyChanged(); }
        }

        /// <summary>True while tokens are still arriving.</summary>
        public bool IsStreaming
        {
            get => _isStreaming;
            set { _isStreaming = value; OnPropertyChanged(); }
        }

        // ── Same surface as ChatMessage (for the shared bubble style) ──

        public bool IsUser => false;
        public bool IsError => false;
        public bool IsAiResponse => true;

        public string ProviderName { get; set; } = string.Empty;
        public string ProviderIcon { get; set; } = string.Empty;

        public string SenderLabel => ProviderName;
        public string TimeLabel { get; } = DateTime.Now.ToString("h:mm tt");
    }
}
