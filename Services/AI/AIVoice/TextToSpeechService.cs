// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIVoice/TextToSpeechService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Voice (Chat 24 v1.0 — NEW)
//
// PURPOSE:
//   Text-to-Speech using Windows built-in System.Speech.Synthesis.
//   Free, offline, no API key needed. Reads AI responses aloud.
//
// HOW IT WORKS:
//   1. Creates a SpeechSynthesizer with default voice
//   2. SpeakAsync(text) queues text for speaking (non-blocking)
//   3. Stop() cancels current speech immediately
//   4. Rate and Volume are adjustable
//
// VOICE QUALITY:
//   Windows 10/11 includes Microsoft David/Zira (EN-US) voices.
//   They sound robotic compared to neural TTS (Azure/ElevenLabs),
//   but they're FREE, OFFLINE, and instant — no API calls needed.
//
//   Future upgrade path: swap this service with an Azure Neural TTS
//   implementation using the same ITextToSpeechService interface.
//
// REQUIREMENTS:
//   • Windows 10/11
//   • NuGet: System.Speech (or GAC reference)
//
// REGRESSION RISK: ZERO — new file, no existing code depends on it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Speech.Synthesis;

namespace ProGlassAutomation.Services.AI.AIVoice
{
    /// <summary>
    /// Text-to-Speech using Windows built-in speech synthesis.
    /// Free, offline, no API key required.
    /// </summary>
    public class TextToSpeechService : ITextToSpeechService, IDisposable
    {
        private SpeechSynthesizer _synth;
        private bool _isSpeaking;
        private bool _isDisposed;

        public bool IsSpeaking => _isSpeaking;
        public bool IsAvailable { get; private set; }

        /// <summary>Speech rate: -10 (slowest) to +10 (fastest), 0 = normal.</summary>
        public int Rate
        {
            get => _synth?.Rate ?? 0;
            set { if (_synth != null) _synth.Rate = Math.Clamp(value, -10, 10); }
        }

        /// <summary>Volume: 0 (silent) to 100 (loudest).</summary>
        public int Volume
        {
            get => _synth?.Volume ?? 100;
            set { if (_synth != null) _synth.Volume = Math.Clamp(value, 0, 100); }
        }

        public TextToSpeechService()
        {
            try
            {
                _synth = new SpeechSynthesizer();

                // Use default audio output device
                _synth.SetOutputToDefaultAudioDevice();

                // Moderate speed — not too fast, not too slow
                _synth.Rate = 1;
                _synth.Volume = 90;

                // Track speaking state
                _synth.SpeakStarted += (s, e) => _isSpeaking = true;
                _synth.SpeakCompleted += (s, e) => _isSpeaking = false;

                IsAvailable = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TextToSpeech] Initialization failed: {ex.Message}");
                IsAvailable = false;
            }
        }

        /// <summary>
        /// Speak the given text aloud. Non-blocking.
        /// Queues behind any currently-speaking text.
        /// </summary>
        public void SpeakAsync(string text)
        {
            if (!IsAvailable || _isDisposed || string.IsNullOrWhiteSpace(text))
                return;

            try
            {
                // Clean up markdown/special chars that sound weird spoken aloud
                var cleanText = CleanForSpeech(text);
                _synth.SpeakAsync(cleanText);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TextToSpeech] SpeakAsync failed: {ex.Message}");
            }
        }

        /// <summary>Stop speaking immediately.</summary>
        public void Stop()
        {
            if (!IsAvailable || _isDisposed) return;

            try
            {
                _synth.SpeakAsyncCancelAll();
                _isSpeaking = false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[TextToSpeech] Stop failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Cleans text for natural speech output.
        /// Removes markdown formatting, code blocks, bullet chars, etc.
        /// that would sound awkward when spoken aloud.
        /// </summary>
        private static string CleanForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var cleaned = text;

            // Remove markdown bold/italic markers
            cleaned = cleaned.Replace("**", "");
            cleaned = cleaned.Replace("__", "");
            cleaned = cleaned.Replace("*", "");
            cleaned = cleaned.Replace("_", " ");

            // Remove markdown headers
            cleaned = cleaned.Replace("###", "");
            cleaned = cleaned.Replace("##", "");
            cleaned = cleaned.Replace("#", "");

            // Remove code block markers
            cleaned = cleaned.Replace("```", "");
            cleaned = cleaned.Replace("`", "");

            // Replace bullet points with natural pause
            cleaned = cleaned.Replace("• ", ". ");
            cleaned = cleaned.Replace("- ", ". ");

            // Replace multiple newlines with period (natural pause)
            cleaned = System.Text.RegularExpressions.Regex.Replace(
                cleaned, @"\n{2,}", ". ");
            cleaned = cleaned.Replace("\n", " ");

            // Clean up extra spaces
            cleaned = System.Text.RegularExpressions.Regex.Replace(
                cleaned, @"\s{2,}", " ");

            return cleaned.Trim();
        }

        // ── Cleanup ──────────────────────────────────────────────────

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            Stop();

            if (_synth != null)
            {
                _synth.Dispose();
                _synth = null;
            }
        }
    }
}