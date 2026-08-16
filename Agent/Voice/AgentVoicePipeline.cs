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

                    _speaker.Speak(notReady);
                    return notReady;
                }

                string? transcript = await _listener
                    .CaptureTranscriptAsync(timeoutMs)
                    .ConfigureAwait(false);

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    var nothing = AgentResponse.Fail(
                        "I didn't hear anything. Try again and speak after the tone.");

                    _speaker.Speak(nothing);
                    return nothing;
                }

                var response = await _agent.HandleAsync(new AgentRequest
                {
                    Text = transcript,
                    FromVoice = true,
                    Username = username
                }).ConfigureAwait(false);

                _speaker.Speak(response);
                return response;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentVoice] pipeline failed: {ex.Message}");

                var failure = AgentResponse.Fail(
                    "Something went wrong with voice input, so I stopped.");

                _speaker.Speak(failure);
                return failure;
            }
        }
    }
}
