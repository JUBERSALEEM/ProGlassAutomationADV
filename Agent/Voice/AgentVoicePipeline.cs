// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Voice/AgentVoicePipeline.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// listen → handle → speak, in one call.
//
// The two silent-failure cases are handled EXPLICITLY, because "nothing
// happened" is the worst possible voice UX:
//   • voice not ready   → says so, and why
//   • nothing heard     → says it did not hear anything
//
// Transcripts are untrusted input and are sanitized by the agent core
// before they influence anything.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Core;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Agent.Voice.SpeechToText.Whisper;
using ProGlassAutomation.Agent.Voice.TextToSpeech;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Voice
{
    /// <summary>End-to-end voice turn for the agent.</summary>
    public sealed class AgentVoicePipeline
    {
        private readonly ProGlassAgent _agent;
        private readonly AgentWhisperBridge _listener;
        private readonly AgentSpeechAdapter _speaker;

        public AgentVoicePipeline(
            ProGlassAgent agent,
            AgentWhisperBridge listener,
            AgentSpeechAdapter speaker)
        {
            _agent = agent;
            _listener = listener;
            _speaker = speaker;
        }

        /// <summary>True when a voice turn can actually be attempted.</summary>
        public bool IsReady => _listener.IsReady;

        /// <summary>
        /// Why voice is unavailable, surfaced so the CHAT can explain it.
        /// Previously this only existed on the listener, so the chat could
        /// not distinguish "model not downloaded" from "heard nothing".
        /// </summary>
        public string ReadinessMessage => _listener.ReadinessMessage;

        /// <summary>
        /// When true the agent never speaks. Checked inside Speak, so every
        /// route into speech - voice turns and typed turns alike - honours
        /// it, rather than only the one the mute button happens to know
        /// about.
        /// </summary>
        public bool IsMuted { get; set; }

        /// <summary>True while speech is in progress.</summary>
        public bool IsSpeaking => _speaker.IsSpeaking;

        /// <summary>
        /// Transcribes one utterance WITHOUT running it through the agent,
        /// so the caller can display what was heard before answering it.
        /// Returns null when nothing was captured.
        /// </summary>
        public Task<string?> CaptureTranscriptAsync(int timeoutMs = 10000) =>
            _listener.CaptureTranscriptAsync(timeoutMs);

        /// <summary>Silences speech in progress. Never throws.</summary>
        public void StopSpeaking()
        {
            try { _speaker.Stop(); }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentVoice] stop failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Speaks a response unless muted. Single choke point so mute
        /// cannot be bypassed by a future caller.
        /// </summary>
        /// <summary>
        /// Public, mute-aware speech entry point for callers outside the
        /// pipeline (the chat window speaks answers to spoken questions).
        /// </summary>
        public void SpeakResponse(AgentResponse? response) => SpeakIfAllowed(response);

        private void SpeakIfAllowed(AgentResponse? response)
        {
            if (IsMuted) return;
            _speaker.Speak(response);
        }

        /// <summary>
        /// Captures one utterance, runs it through the agent and speaks
        /// the answer. Never throws.
        /// </summary>
        public async Task<AgentResponse> ListenAndHandleAsync(
            string username, int timeoutMs = 10000)
        {
            try
            {
                if (!_listener.IsReady)
                {
                    var notReady = AgentResponse.Fail(
                        $"Voice input is not ready. {_listener.ReadinessMessage}");

                    SpeakIfAllowed(notReady);
                    return notReady;
                }

                string? transcript = await _listener
                    .CaptureTranscriptAsync(timeoutMs)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    var nothing = AgentResponse.Fail(
                        "I didn't hear anything. Try again and speak after the tone.");

                    SpeakIfAllowed(nothing);
                    return nothing;
                }

                var response = await _agent.HandleAsync(new AgentRequest
                {
                    Text = transcript,
                    FromVoice = true,
                    Username = username
                }).ConfigureAwait(false);

                SpeakIfAllowed(response);
                return response;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentVoice] pipeline failed: {ex.Message}");

                var failure = AgentResponse.Fail(
                    "Something went wrong with voice input, so I stopped.");

                SpeakIfAllowed(failure);
                return failure;
            }
        }
    }
}
