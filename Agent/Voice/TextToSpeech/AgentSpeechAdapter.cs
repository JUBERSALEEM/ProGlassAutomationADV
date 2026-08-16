// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Voice/TextToSpeech/AgentSpeechAdapter.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Speaks an AgentResponse aloud using the app's existing TTS service.
//
// It reads SpeakText, not Message: the on-screen answer can be a long
// formatted list, while the spoken form stays short. Speaking a 40-line
// inventory dump would be useless, so the agent keeps the two separate.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Core;
using ProGlassAutomation.Services.AI.AIVoice;
using System;
using System.Diagnostics;

namespace ProGlassAutomation.Agent.Voice.TextToSpeech
{
    /// <summary>Speaks agent responses. Silent when TTS is unavailable.</summary>
    public sealed class AgentSpeechAdapter
    {
        /// <summary>Spoken text is trimmed to this many characters.</summary>
        public const int MaxSpokenLength = 600;

        private readonly ITextToSpeechService? _tts;

        public AgentSpeechAdapter(ITextToSpeechService? tts)
        {
            _tts = tts;
        }

        /// <summary>True when speech synthesis can actually be used.</summary>
        public bool IsAvailable
        {
            get
            {
                try { return _tts is { IsAvailable: true }; }
                catch { return false; }
            }
        }

        /// <summary>Speaks a response. Never throws.</summary>
        public void Speak(AgentResponse? response)
        {
            if (response == null) return;

            string text = string.IsNullOrWhiteSpace(response.SpeakText)
                ? response.Message
                : response.SpeakText;

            Speak(text);
        }

        /// <summary>Speaks arbitrary text. Never throws.</summary>
        public void Speak(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;
            if (!IsAvailable) return;

            try
            {
                string spoken = text.Trim();
                if (spoken.Length > MaxSpokenLength)
                    spoken = spoken[..MaxSpokenLength];

                _tts!.SpeakAsync(spoken);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentSpeech] speak failed: {ex.Message}");
            }
        }

        /// <summary>Stops any speech in progress.</summary>
        public void Stop()
        {
            try { _tts?.Stop(); }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentSpeech] stop failed: {ex.Message}");
            }
        }
    }
}
