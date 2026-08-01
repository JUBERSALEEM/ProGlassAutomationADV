// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIVoice/WhisperSpeechService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Voice (Chat 25 v1.0 — IsAvailable fix + multi-model + fast PTT)
//
// CHANGES vs Chat 25 v1.0 (previous fix):
//   ✓ FIX BUG #4: IsAvailable set once in constructor, never raised
//     PropertyChanged. Mic button always hidden after model download
//     until app restart. Now IsAvailable is a proper notifying property.
//   ✓ FIX BUG #5: ListenOnceAsync waited full 10s timeout always.
//     Replaced with Push-To-Talk model: StartListening / StopListening
//     pair. VoiceInputCommand calls Start on press, Stop on release.
//     Audio processed immediately when StopListening called — no wait.
//   ✓ FIX: Model path now reads from WhisperModelManager.DefaultModelPath
//     dynamically — respects user's model selection (base/medium/large).
//   ✓ FIX: LoadModelAsync reloads if model path changed (user switched
//     model in Settings). Old processor disposed, new one created.
//   ✓ Added RefreshAvailability() — called after model download completes
//     so UI updates without app restart.
//
// REGRESSION RISK: LOW — interface preserved, behavior improved.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels;
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;

namespace ProGlassAutomation.Services.AI.AIVoice
{
    /// <summary>
    /// Neural Speech-to-Text using OpenAI Whisper (local inference).
    /// 95%+ accuracy, auto-punctuation, accent-resistant, offline.
    /// Push-to-Talk model: StartListening/StopListening pair.
    /// </summary>
    public class WhisperSpeechService : ViewModelBase,
                                        ISpeechToTextService,
                                        IDisposable
    {
        private readonly AudioCaptureService _audioCapture;
        private WhisperProcessor _processor;
        private string _loadedModelPath;
        private bool _isListening;
        private bool _isDisposed;
        private bool _modelLoaded;
        private bool _isAvailable;

        // ── ISpeechToTextService ──────────────────────────────────────

        public bool IsListening
        {
            get => _isListening;
            private set
            {
                if (_isListening == value) return;
                _isListening = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// True if mic is available AND model is downloaded.
        /// Now a notifying property — UI updates without restart.
        /// Call RefreshAvailability() after model download completes.
        /// </summary>
        public bool IsAvailable
        {
            get => _isAvailable;
            private set
            {
                if (_isAvailable == value) return;
                _isAvailable = value;
                OnPropertyChanged();
            }
        }

        public event EventHandler<SpeechRecognizedEventArgs> SpeechRecognized;
        public event EventHandler<bool> ListeningStateChanged;

        /// <summary>
        /// Fires with status updates during model loading / processing.
        /// Used by UI to show "Loading model..." / "Processing..." etc.
        /// </summary>
        public event EventHandler<string> StatusChanged;

        public WhisperSpeechService()
        {
            _audioCapture = new AudioCaptureService();
            RefreshAvailability();
        }

        /// <summary>
        /// Refreshes IsAvailable based on current mic + model state.
        /// Call this after model download completes so UI updates
        /// without requiring an app restart.
        /// </summary>
        public void RefreshAvailability()
        {
            IsAvailable = _audioCapture.IsMicrophoneAvailable &&
                          WhisperModelManager.IsModelDownloaded;
        }

        // ══════════════════════════════════════════════════════════════
        //  MODEL LOADING
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Loads (or reloads) the Whisper model into memory.
        /// Automatically detects if the model path changed (user switched
        /// model in Settings) and reloads accordingly.
        /// Takes 3-5 seconds for base, 8-15 for large.
        /// </summary>
        public async Task<bool> LoadModelAsync()
        {
            if (!WhisperModelManager.IsModelDownloaded) return false;

            var currentModelPath = WhisperModelManager.DefaultModelPath;

            // Already loaded the correct model — skip
            if (_modelLoaded && _loadedModelPath == currentModelPath)
                return true;

            // Model path changed or not loaded — (re)load
            if (_processor != null)
            {
                try { _processor.Dispose(); } catch { }
                _processor = null;
                _modelLoaded = false;
            }

            try
            {
                StatusChanged?.Invoke(this, "Loading Whisper model...");

                _processor = await Task.Run(() =>
                {
                    var factory = WhisperFactory.FromPath(currentModelPath);
                    return factory.CreateBuilder()
                        .WithLanguage("en")
                        .WithNoContext()
                        .WithSingleSegment()
                        .Build();
                });

                _loadedModelPath = currentModelPath;
                _modelLoaded = true;
                IsAvailable = true;
                StatusChanged?.Invoke(this, "Whisper model ready");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WhisperSTT] Model load failed: {ex.Message}");
                StatusChanged?.Invoke(this, $"Model load failed: {ex.Message}");
                _modelLoaded = false;
                IsAvailable = false;
                return false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  PUSH-TO-TALK — StartListening / StopListening pair
        //  BUG #5 FIX: No more 10-second wait.
        //  Start when mic button pressed, Stop when released/clicked again.
        //  Audio processed immediately on Stop.
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Start recording from microphone. Call StopListening() when
        /// user finishes speaking. Audio processed at that point.
        /// </summary>
        public void StartListening()
        {
            if (!IsAvailable || _isListening || _isDisposed) return;

            if (!_modelLoaded)
            {
                // Load model on background thread, then start recording
                _ = Task.Run(async () =>
                {
                    var loaded = await LoadModelAsync();
                    if (loaded)
                    {
                        System.Windows.Application.Current?.Dispatcher.Invoke(
                            StartListeningInternal);
                    }
                });
                return;
            }

            StartListeningInternal();
        }

        private void StartListeningInternal()
        {
            if (_isListening || _isDisposed) return;

            _audioCapture.StartRecording();
            IsListening = true;
            ListeningStateChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, "Listening...");
        }

        /// <summary>
        /// Stop recording and process the captured audio through Whisper.
        /// Recognition result fires via SpeechRecognized event.
        /// Results available in 1-3 seconds after calling this.
        /// async void intentional — fire-and-forget event pattern.
        /// All exceptions caught internally.
        /// </summary>
        public async void StopListening()
        {
            if (!_isListening || _isDisposed) return;

            IsListening = false;
            ListeningStateChanged?.Invoke(this, false);
            StatusChanged?.Invoke(this, "Processing speech...");

            try
            {
                var audioBytes = await _audioCapture.StopAndGetAudioAsync();

                if (audioBytes == null || audioBytes.Length < 16000)
                {
                    StatusChanged?.Invoke(this, "No speech detected");
                    return;
                }

                var text = await ProcessAudioAsync(audioBytes);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    SpeechRecognized?.Invoke(this, new SpeechRecognizedEventArgs
                    {
                        Text = text.Trim(),
                        Confidence = 0.95f,
                        IsFinal = true
                    });
                    StatusChanged?.Invoke(this, "Speech recognized");
                }
                else
                {
                    StatusChanged?.Invoke(this, "Could not recognize speech");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WhisperSTT] Processing failed: {ex.Message}");
                StatusChanged?.Invoke(this, $"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Listen for a single utterance with a timeout safety net.
        /// For programmatic use (not PTT). Waits for speech or timeout.
        /// </summary>
        public async Task<string> ListenOnceAsync(int timeoutMs = 10000)
        {
            if (!IsAvailable || _isDisposed) return null;

            if (!_modelLoaded)
            {
                var loaded = await LoadModelAsync();
                if (!loaded) return null;
            }

            var tcs = new System.Threading.Tasks.TaskCompletionSource<string>();

            void Handler(object s, SpeechRecognizedEventArgs e)
            {
                tcs.TrySetResult(e.Text);
            }

            SpeechRecognized += Handler;

            _audioCapture.StartRecording();
            IsListening = true;
            ListeningStateChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, "Listening...");

            // Timeout safety net — auto-stop after timeoutMs
            _ = Task.Delay(timeoutMs).ContinueWith(_ =>
            {
                tcs.TrySetResult(null);
            });

            // Wait for result or timeout
            var result = await tcs.Task;

            // If we got here via timeout (not recognition), process audio
            if (result == null && _isListening)
            {
                IsListening = false;
                ListeningStateChanged?.Invoke(this, false);
                StatusChanged?.Invoke(this, "Processing speech...");

                var audioBytes = await _audioCapture.StopAndGetAudioAsync();
                if (audioBytes != null && audioBytes.Length >= 16000)
                {
                    var text = await ProcessAudioAsync(audioBytes);
                    if (!string.IsNullOrWhiteSpace(text))
                        result = text.Trim();
                }
            }
            else if (_isListening)
            {
                IsListening = false;
                ListeningStateChanged?.Invoke(this, false);
                _audioCapture.CancelRecording();
            }

            SpeechRecognized -= Handler;
            StatusChanged?.Invoke(this,
                result != null ? "Speech recognized" : "No speech detected");

            return result;
        }

        // ══════════════════════════════════════════════════════════════
        //  WHISPER INFERENCE
        // ══════════════════════════════════════════════════════════════

        private async Task<string> ProcessAudioAsync(byte[] wavBytes)
        {
            if (_processor == null || wavBytes == null) return null;

            try
            {
                using var stream = new MemoryStream(wavBytes);
                var sb = new StringBuilder();

                await foreach (var segment in _processor.ProcessAsync(stream))
                {
                    sb.Append(segment.Text);
                }

                var result = sb.ToString().Trim();

                if (string.IsNullOrWhiteSpace(result)) return null;
                if (result == "[MUSIC]") return null;
                if (result == "[BLANK_AUDIO]") return null;
                if (result.StartsWith("[") && result.EndsWith("]")) return null;

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WhisperSTT] ProcessAudioAsync failed: {ex.Message}");
                return null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  DISPOSE
        // ══════════════════════════════════════════════════════════════

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            _audioCapture?.CancelRecording();
            _audioCapture?.Dispose();

            try { _processor?.Dispose(); } catch { }
            _processor = null;
            _modelLoaded = false;
        }
    }
}