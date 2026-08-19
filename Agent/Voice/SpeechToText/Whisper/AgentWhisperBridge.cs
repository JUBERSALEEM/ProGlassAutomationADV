// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Voice/SpeechToText/Whisper/AgentWhisperBridge.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Wraps the EXISTING ISpeechToTextService. It adds no capture code of its
// own — only a readiness definition the agent can trust:
//
//   IsReady = the service is available  AND  the Whisper model is on disk
//
// Both halves matter. A microphone with no model transcribes nothing, and
// a model with no microphone captures nothing. Reporting readiness
// honestly is what lets the voice pipeline say "voice is not ready"
// instead of silently returning empty transcripts.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.AI.AIVoice;
using ProGlassAutomation.Services.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Voice.SpeechToText.Whisper
{
    /// <summary>Readiness-aware speech capture for the agent.</summary>
    public sealed class AgentWhisperBridge
    {
        private readonly ISpeechToTextService? _stt;

        public AgentWhisperBridge(ISpeechToTextService? stt)
        {
            _stt = stt;
        }

        /// <summary>
        /// True only when speech recognition is available AND the local
        /// Whisper model has been downloaded.
        /// </summary>
        public bool IsReady
        {
            get
            {
                try
                {
                    return _stt is { IsAvailable: true } &&
                           WhisperModelManager.IsModelDownloaded;
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[AgentWhisper] readiness check failed: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>Explains why voice is unavailable, for the UI.</summary>
        public string ReadinessMessage
        {
            get
            {
                try
                {
                    if (_stt is not { IsAvailable: true })
                        return "Speech recognition is not available on this computer.";

                    if (!WhisperModelManager.IsModelDownloaded)
                        return "The local speech model has not been downloaded yet.";

                    return "Voice input is ready.";
                }
                catch
                {
                    return "Voice input is not available.";
                }
            }
        }

        /// <summary>
        /// Captures one utterance. Returns null when nothing was heard or
        /// voice is not ready — callers must handle null, never assume text.
        /// </summary>
        public async Task<string?> CaptureTranscriptAsync(int timeoutMs = 10000)
        {
            if (!IsReady) return null;

            try
            {
                var text = await _stt!.ListenOnceAsync(timeoutMs)
                    .ConfigureAwait(false);

                return string.IsNullOrWhiteSpace(text) ? null : text.Trim();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentWhisper] capture failed: {ex.Message}");
                return null;
            }
        }
    }
}
