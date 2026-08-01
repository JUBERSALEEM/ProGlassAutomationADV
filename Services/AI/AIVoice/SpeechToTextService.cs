// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIVoice/SpeechToTextService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Voice (Chat 25 v3.0 — factory floor tuning)
//
// CHANGES vs Chat 24 v1.3:
//   ✓ FIX: BabbleTimeout changed from TimeSpan.Zero (disabled) to
//     TimeSpan.FromSeconds(3). On a noisy factory floor, disabled
//     babble timeout causes recognizer to run indefinitely on background
//     noise. 3 seconds gives a reasonable cutoff.
//   ✓ FIX: InitialSilenceTimeout reduced from 6s to 3s. 6 seconds is
//     too long for a factory worker — they need fast response. 3 seconds
//     is enough time to start speaking without feeling laggy.
//   ✓ FIX: EndSilenceTimeout reduced from 1.5s to 1.0s for snappier
//     end-of-speech detection.
//   ✓ FIX: EndSilenceTimeoutAmbiguous reduced from 2.0s to 1.5s.
//   ✓ All grammar vocabulary, event handlers, Dispose preserved exactly.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Speech.Recognition;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Services.AI.AIVoice
{
    /// <summary>
    /// Enhanced Speech-to-Text using Windows built-in speech recognition.
    /// Fallback for when Whisper model is not downloaded.
    /// Tuned for factory floor use — fast timeouts, noise-resilient.
    /// </summary>
    public class SpeechToTextService : ISpeechToTextService, IDisposable
    {
        private SpeechRecognitionEngine _recognizer;
        private bool _isListening;
        private bool _isDisposed;

        public bool IsListening => _isListening;
        public bool IsAvailable { get; private set; }

        public event EventHandler<SpeechRecognizedEventArgs> SpeechRecognized;
        public event EventHandler<bool> ListeningStateChanged;
        public event EventHandler<string> PartialTextRecognized;

        public SpeechToTextService()
        {
            try
            {
                RecognizerInfo recognizerInfo = null;

                foreach (var info in
                    SpeechRecognitionEngine.InstalledRecognizers())
                {
                    if (info.Culture.Name == "en-US")
                    {
                        recognizerInfo = info;
                        break;
                    }
                    if (info.Culture.TwoLetterISOLanguageName == "en" &&
                        recognizerInfo == null)
                    {
                        recognizerInfo = info;
                    }
                }

                if (recognizerInfo == null)
                {
                    var installed =
                        SpeechRecognitionEngine.InstalledRecognizers();
                    if (installed.Count > 0)
                        recognizerInfo = installed[0];
                }

                if (recognizerInfo == null)
                {
                    IsAvailable = false;
                    return;
                }

                _recognizer = new SpeechRecognitionEngine(recognizerInfo);

                // ── Load grammars ────────────────────────────────────────
                var dictation = new DictationGrammar
                {
                    Name = "freeform",
                    Weight = 0.5f
                };
                _recognizer.LoadGrammar(dictation);

                var customGrammar = BuildCustomGrammar();
                if (customGrammar != null)
                {
                    customGrammar.Name = "industry";
                    customGrammar.Weight = 0.8f;
                    _recognizer.LoadGrammar(customGrammar);
                }

                // ── Timeout tuning — factory floor optimized ─────────────

                // FIX: 3s instead of 6s — faster response for factory worker
                _recognizer.InitialSilenceTimeout =
                    TimeSpan.FromSeconds(3);

                // FIX: 1.0s instead of 1.5s — snappier end detection
                _recognizer.EndSilenceTimeout =
                    TimeSpan.FromSeconds(1.0);

                // FIX: 1.5s instead of 2.0s
                _recognizer.EndSilenceTimeoutAmbiguous =
                    TimeSpan.FromSeconds(1.5);

                // FIX: 3s instead of Zero — prevents infinite run on
                // factory background noise (machines, air compressors, etc.)
                _recognizer.BabbleTimeout =
                    TimeSpan.FromSeconds(3);

                // Lower confidence threshold for noisy environments
                _recognizer.UpdateRecognizerSetting(
                    "CFGConfidenceRejectionThreshold", 25);

                // ── Wire events ──────────────────────────────────────────
                _recognizer.SpeechRecognized += OnSpeechRecognized;
                _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
                _recognizer.SpeechHypothesized += OnSpeechHypothesized;

                _recognizer.SetInputToDefaultAudioDevice();
                IsAvailable = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SpeechToText] Init failed: {ex.Message}");
                IsAvailable = false;
            }
        }

        private static Grammar BuildCustomGrammar()
        {
            try
            {
                var choices = new Choices();

                // App navigation
                choices.Add("open dashboard", "show dashboard",
                    "open invoices", "open proforma",
                    "open proforma invoice", "open sheet store",
                    "open job orders", "open job order",
                    "open delivery", "open delivery notes",
                    "open tax invoice", "open tax invoices",
                    "open sales report", "open settings",
                    "open about", "new invoice",
                    "create invoice", "create proforma");

                // Business questions
                choices.Add("what is my outstanding balance",
                    "what's my outstanding balance",
                    "show outstanding balance",
                    "what is my total sales",
                    "how many invoices", "how many job orders",
                    "how many jobs", "how many deliveries",
                    "pending deliveries", "show pending deliveries",
                    "overdue jobs", "show overdue jobs",
                    "total invoiced", "total outstanding",
                    "total overdue");

                // Glass industry terms
                choices.Add("tempered glass", "laminated glass",
                    "clear glass", "tinted glass", "frosted glass",
                    "reflective glass", "low e glass", "low E glass",
                    "double glazed unit", "DGU", "D G U", "IGU",
                    "insulated glass unit", "SGU", "single glazed unit",
                    "tempering process", "cutting process",
                    "edge deletion", "mitring", "polishing",
                    "pairing", "doubling", "lamination",
                    "shower door", "shower glass", "glass railing",
                    "glass partition", "glass facade",
                    "curtain wall", "spider fitting", "point fixing");

                // UAE business terms
                choices.Add("AED", "UAE dirham", "VAT",
                    "five percent VAT", "proforma", "tax invoice",
                    "delivery note", "job order", "account statement",
                    "sales report", "square meter", "SQM");

                // General commands
                choices.Add("hello", "hey", "hi",
                    "thank you", "thanks", "yes", "no",
                    "stop", "cancel", "clear chat",
                    "help", "what can you do", "explain");

                return new Grammar(new GrammarBuilder(choices));
            }
            catch
            {
                return null;
            }
        }

        public void StartListening()
        {
            if (!IsAvailable || _isListening || _isDisposed) return;
            try
            {
                _recognizer.RecognizeAsync(RecognizeMode.Multiple);
                _isListening = true;
                ListeningStateChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SpeechToText] StartListening failed: {ex.Message}");
            }
        }

        public void StopListening()
        {
            if (!_isListening || _isDisposed) return;
            try
            {
                _recognizer.RecognizeAsyncCancel();
                _isListening = false;
                ListeningStateChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SpeechToText] StopListening failed: {ex.Message}");
            }
        }

        public async Task<string> ListenOnceAsync(int timeoutMs = 10000)
        {
            if (!IsAvailable || _isDisposed) return null;

            string result = null;
            var tcs = new TaskCompletionSource<string>();
            var cts = new CancellationTokenSource(timeoutMs);

            void Handler(object s, SpeechRecognizedEventArgs e)
            {
                if (e.Confidence >= 0.25f)
                {
                    result = e.Text;
                    tcs.TrySetResult(e.Text);
                }
            }

            SpeechRecognized += Handler;
            cts.Token.Register(() => tcs.TrySetResult(null));
            StartListening();

            try { result = await tcs.Task; }
            finally
            {
                StopListening();
                SpeechRecognized -= Handler;
                cts.Dispose();
            }

            return result;
        }

        private void OnSpeechRecognized(
            object sender,
            System.Speech.Recognition.SpeechRecognizedEventArgs e)
        {
            if (e.Result == null) return;

            var text = e.Result.Text;
            var confidence = e.Result.Confidence;

            if (confidence < 0.4f && e.Result.Alternates != null)
            {
                foreach (var alt in e.Result.Alternates)
                {
                    if (alt.Confidence > confidence)
                    {
                        text = alt.Text;
                        confidence = alt.Confidence;
                    }
                }
            }

            if (confidence >= 0.25f)
            {
                SpeechRecognized?.Invoke(this, new SpeechRecognizedEventArgs
                {
                    Text = text,
                    Confidence = confidence,
                    IsFinal = true
                });
            }
        }

        private void OnSpeechRejected(
            object sender, SpeechRecognitionRejectedEventArgs e)
        {
            if (e.Result?.Alternates == null) return;
            foreach (var alt in e.Result.Alternates)
            {
                if (alt.Confidence >= 0.2f)
                {
                    SpeechRecognized?.Invoke(this, new SpeechRecognizedEventArgs
                    {
                        Text = alt.Text,
                        Confidence = alt.Confidence,
                        IsFinal = true
                    });
                    return;
                }
            }
        }

        private void OnSpeechHypothesized(
            object sender, SpeechHypothesizedEventArgs e)
        {
            if (e.Result != null)
                PartialTextRecognized?.Invoke(this, e.Result.Text);
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            StopListening();
            if (_recognizer != null)
            {
                _recognizer.SpeechRecognized -= OnSpeechRecognized;
                _recognizer.SpeechRecognitionRejected -= OnSpeechRejected;
                _recognizer.SpeechHypothesized -= OnSpeechHypothesized;
                _recognizer.Dispose();
                _recognizer = null;
            }
        }
    }
}