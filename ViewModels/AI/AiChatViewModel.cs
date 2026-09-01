// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/AI/AiChatViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Chat (Chat 25 v5.0 — Whisper fallback + full thread safety)
//
// CHANGES vs Chat 25 v4.0:
//   ✓ NEW: whisper.ModelLoadFailed subscription — when Whisper model
//     fails to load (corrupted file, missing DLL, permission error),
//     VM auto-swaps stt to SpeechToTextService fallback.
//     Voice input continues to work with reduced accuracy.
//   ✓ ALL v4.0 fixes preserved: thread marshaling, no auto-send,
//     duplicate handler prevention, IsProcessingVoice reset.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.AI.AIContext;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;
namespace ProGlassAutomation.ViewModels.AI
{
    /// <summary>
    /// ViewModel for the floating AI chat panel.
    /// Thread-safe voice event handling with auto-fallback on Whisper failure.
    /// </summary>
    public partial class AiChatViewModel : ViewModelBase
    {
    }
}