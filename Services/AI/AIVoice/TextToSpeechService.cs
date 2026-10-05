// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIVoice/TextToSpeechService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Voice (Chat 25 v3.0 — English voice selection for UAE)
//
// CHANGES vs Chat 24 v1.0:
//   ✓ FIX: Explicit English voice selection added.
//     On UAE Windows machines with Arabic language packs the default
//     voice may be Arabic. Now explicitly searches for an English
//     voice (en-US preferred, any English fallback, any voice last).
//     This ensures AI responses are always spoken in English.
//   ✓ Added debug logging to show which voice was selected at startup.
//   ✓ All other logic preserved exactly.
// ═══════════════════════════════════════════════════════════════════════

using System;
using ProGlassAutomation.Services.Logging;
using System.Linq;
using System.Speech.Synthesis;

namespace ProGlassAutomation.Services.AI.AIVoice
{
    /// <summary>
    /// Text-to-Speech using Windows built-in speech synthesis.
    /// Free, offline, no API key required.
    /// English voice explicitly selected for UAE deployments.
    /// </summary>
    public class TextToSpeechService : ITextToSpeechService, IDisposable
    {
        private SpeechSynthesizer _synth;
        private bool _isSpeaking;
        private bool _isDisposed;

        public bool IsSpeaking => _isSpeaking;
        public bool IsAvailable { get; private set; }

        public int Rate
        {
            get => _synth?.Rate ?? 0;
            set { if (_synth != null) _synth.Rate = Math.Clamp(value, -10, 10); }
        }

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
                _synth.SetOutputToDefaultAudioDevice();

                // ── FIX: Explicit English voice selection ────────────────
                // On UAE machines Arabic voices may be default.
                // We need English for AI responses.
                // Priority: en-US > any English > any available voice.
                SelectEnglishVoice();

                _synth.Rate = 1;   // Slightly faster than default
                _synth.Volume = 90;

                _synth.SpeakStarted += (s, e) => _isSpeaking = true;
                _synth.SpeakCompleted += (s, e) => _isSpeaking = false;

                IsAvailable = true;

                AppLogService.Debug(
                    $"[TextToSpeech] Ready. " +
                    $"Voice: {_synth.Voice?.Name ?? "unknown"} | " +
                    $"Culture: {_synth.Voice?.Culture?.Name ?? "unknown"}");
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[TextToSpeech] Init failed: {ex.Message}");
                IsAvailable = false;
            }
        }

        /// <summary>
        /// Selects the best available English voice.
        /// Priority: en-US > en-GB > en-AU > any English > any available.
        /// Logs selected voice to Debug output.
        /// </summary>
        private void SelectEnglishVoice()
        {
            try
            {
                var voices = _synth.GetInstalledVoices()
                    .Where(v => v.Enabled)
                    .ToList();

                if (voices.Count == 0)
                {
                    AppLogService.Debug(
                        "[TextToSpeech] No voices found — using system default.");
                    return;
                }

                // 1. Prefer en-US
                var enUs = voices.FirstOrDefault(v =>
                    v.VoiceInfo.Culture.Name
                     .StartsWith("en-US",
                         StringComparison.OrdinalIgnoreCase));

                if (enUs != null)
                {
                    _synth.SelectVoice(enUs.VoiceInfo.Name);
                    AppLogService.Debug(
                        $"[TextToSpeech] Selected en-US voice: " +
                        $"{enUs.VoiceInfo.Name}");
                    return;
                }

                // 2. Any English voice
                var anyEnglish = voices.FirstOrDefault(v =>
                    v.VoiceInfo.Culture.TwoLetterISOLanguageName
                     .Equals("en", StringComparison.OrdinalIgnoreCase));

                if (anyEnglish != null)
                {
                    _synth.SelectVoice(anyEnglish.VoiceInfo.Name);
                    AppLogService.Debug(
                        $"[TextToSpeech] Selected English voice: " +
                        $"{anyEnglish.VoiceInfo.Name} " +
                        $"({anyEnglish.VoiceInfo.Culture.Name})");
                    return;
                }

                // 3. Any available voice — log warning
                var anyVoice = voices.First();
                _synth.SelectVoice(anyVoice.VoiceInfo.Name);
                AppLogService.Debug(
                    $"[TextToSpeech] ⚠ No English voice found. " +
                    $"Using: {anyVoice.VoiceInfo.Name} " +
                    $"({anyVoice.VoiceInfo.Culture.Name}). " +
                    $"Install an English language pack for correct TTS.");
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[TextToSpeech] Voice selection failed: {ex.Message}. " +
                    $"Using system default.");
            }
        }

        public void SpeakAsync(string text)
        {
            if (!IsAvailable || _isDisposed ||
                string.IsNullOrWhiteSpace(text)) return;
            try
            {
                _synth.SpeakAsync(CleanForSpeech(text));
            }
            catch (Exception ex)
            {
                AppLogService.Debug(
                    $"[TextToSpeech] SpeakAsync failed: {ex.Message}");
            }
        }

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
                AppLogService.Debug(
                    $"[TextToSpeech] Stop failed: {ex.Message}");
            }
        }

        private static string CleanForSpeech(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            var cleaned = text
                .Replace("**", "").Replace("__", "")
                .Replace("*", "").Replace("_", " ")
                .Replace("###", "").Replace("##", "").Replace("#", "")
                .Replace("```", "").Replace("`", "")
                .Replace("• ", ". ").Replace("- ", ". ");

            cleaned = System.Text.RegularExpressions.Regex
                .Replace(cleaned, @"\n{2,}", ". ");
            cleaned = cleaned.Replace("\n", " ");
            cleaned = System.Text.RegularExpressions.Regex
                .Replace(cleaned, @"\s{2,}", " ");

            return cleaned.Trim();
        }

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