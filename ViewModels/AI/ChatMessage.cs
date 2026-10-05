// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/AI/ChatMessage.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Chat (Chat 24 v1.1)
//
// FOLDER: ViewModels/AI/            (SAME as before — no move)
// NAMESPACE: ProGlassAutomation.ViewModels.AI  (UNCHANGED)
//
// CHANGES vs previous:
//   ✓ REMOVED ViewModelBase inheritance (messages are immutable POCOs —
//     never raise PropertyChanged, so inheriting was wasteful)
//   ✓ ADDED ProviderName property (tracks which AI responded)
//   ✓ ADDED ProviderIcon property (emoji for compact display)
//   ✓ ADDED IsAiResponse computed property (used by new AiBubbleStyle trigger)
//   ✓ TimeLabel + SenderLabel now USED by the view (were dead code before)
//   ✓ Changed { get; set; } → { get; init; } (enforces immutability)
//
// REGRESSION RISK: LOW
//   Removing ViewModelBase has no runtime impact — old properties never
//   called OnPropertyChanged. init setters work with object initializers
//   (same pattern the VM already uses to create messages).
// ═══════════════════════════════════════════════════════════════════════

using System;

namespace ProGlassAutomation.ViewModels.AI
{
    /// <summary>
    /// Single message in the AI chat. Immutable after construction.
    /// Three visual states: User bubble, AI bubble, Error bubble.
    /// </summary>
    public sealed class ChatMessage
    {
        /// <summary>Message text content.</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>True = user sent this message. False = AI response.</summary>
        public bool IsUser { get; init; }

        /// <summary>True = this is an error message (red bubble).</summary>
        public bool IsError { get; init; }

        /// <summary>When the message was created.</summary>
        public DateTime Timestamp { get; init; } = DateTime.Now;

        /// <summary>Which AI provider generated this response.</summary>
        public string ProviderName { get; init; } = string.Empty;

        /// <summary>Provider emoji icon (e.g. "🔵" for Gemini).</summary>
        public string ProviderIcon { get; init; } = string.Empty;

        // ── Computed display properties (used by view bindings) ────────

        /// <summary>"You" for user messages, provider name for AI.</summary>
        public string SenderLabel => IsUser ? "You" : ProviderName;

        /// <summary>Formatted time (e.g. "4:35 PM").</summary>
        public string TimeLabel => Timestamp.ToString("h:mm tt");

        /// <summary>True if AI response (not user, not error).</summary>
        public bool IsAiResponse => !IsUser && !IsError;
    }
}