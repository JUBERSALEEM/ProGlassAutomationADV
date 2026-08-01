// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIVoice/IVoiceService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Voice (Chat 24 v1.0 — NEW)
//
// PURPOSE:
//   Interfaces for Speech-to-Text and Text-to-Speech services.
//   Uses Windows built-in System.Speech (free, offline, no API needed).
//
// FOLDER: Services/AI/AIVoice/
// NAMESPACE: ProGlassAutomation.Services.AI.AIVoice
//
// NOTE: Requires NuGet package System.Speech (or reference
//   System.Speech.dll from GAC — included in Windows).
//   Add to .csproj: <PackageReference Include="System.Speech" Version="8.0.0" />
//
// REGRESSION RISK: ZERO — new file, no existing code depends on it.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIVoice
{
    /// <summary>
    /// Speech-to-Text service — listens to microphone, returns recognized text.
    /// </summary>
    public interface ISpeechToTextService
    {
        /// <summary>True while actively listening to microphone.</summary>
        bool IsListening { get; }

        /// <summary>True if speech recognition is available on this system.</summary>
        bool IsAvailable { get; }

        /// <summary>
        /// Fires when speech is recognized during listening.
        /// EventArgs contains the recognized text + confidence score.
        /// </summary>
        event EventHandler<SpeechRecognizedEventArgs> SpeechRecognized;

        /// <summary>
        /// Fires when listening starts or stops.
        /// </summary>
        event EventHandler<bool> ListeningStateChanged;

        /// <summary>
        /// Start listening to the microphone. Recognized text fires
        /// via <see cref="SpeechRecognized"/> event.
        /// Call <see cref="StopListening"/> when done.
        /// </summary>
        void StartListening();

        /// <summary>Stop listening to the microphone.</summary>
        void StopListening();

        /// <summary>
        /// Listen for a single utterance and return the text.
        /// Starts listening, waits for speech + silence, stops, returns text.
        /// Returns null if nothing recognized or timeout.
        /// </summary>
        Task<string> ListenOnceAsync(int timeoutMs = 10000);

        /// <summary>Cleanup resources (microphone, recognition engine).</summary>
        void Dispose();
    }

    /// <summary>
    /// Text-to-Speech service — reads text aloud using system voice.
    /// </summary>
    public interface ITextToSpeechService
    {
        /// <summary>True while currently speaking.</summary>
        bool IsSpeaking { get; }

        /// <summary>True if speech synthesis is available on this system.</summary>
        bool IsAvailable { get; }

        /// <summary>Current speech rate (-10 slow to +10 fast, 0 = normal).</summary>
        int Rate { get; set; }

        /// <summary>Current volume (0-100).</summary>
        int Volume { get; set; }

        /// <summary>
        /// Speak the given text aloud. Non-blocking — returns immediately.
        /// Use <see cref="IsSpeaking"/> to check if still speaking.
        /// Call <see cref="Stop"/> to cancel mid-speech.
        /// </summary>
        void SpeakAsync(string text);

        /// <summary>Stop speaking immediately (cancel current speech).</summary>
        void Stop();

        /// <summary>Cleanup resources (synthesizer).</summary>
        void Dispose();
    }

    /// <summary>
    /// Event args for speech recognition results.
    /// </summary>
    public class SpeechRecognizedEventArgs : EventArgs
    {
        /// <summary>The recognized text.</summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Confidence score 0.0–1.0. Higher = more confident.
        /// Below 0.5 is usually unreliable.
        /// </summary>
        public float Confidence { get; init; }

        /// <summary>True if this is a final result (not interim/partial).</summary>
        public bool IsFinal { get; init; } = true;
    }
}