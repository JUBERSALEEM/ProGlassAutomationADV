// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/AI/AIVoice/WhisperSpeechService.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Voice (Chat 25 v5.0 — never hide mic + integrity check)
//
// CHANGES vs Chat 25 v4.0:
//   ✓ FIX CRITICAL (MIC BUTTON DISAPPEARS): Model load failure
//     was setting IsAvailable = false which collapsed the mic
//     button in XAML. User saw button vanish on click.
//     
//     New behavior: IsAvailable stays TRUE if microphone hardware
//     exists, regardless of model load status. Model load failure
//     is reported via StatusChanged event with specific error
//     message so user knows what happened.
//   
//   ✓ NEW: Model file integrity check before load.
//     Validates file size matches expected minimum for selected
//     model variant. Corrupted/partial downloads detected early.
//   
//   ✓ NEW: ModelLoadFailed event fires when load fails, allowing
//     VM to auto-swap to SpeechToTextService fallback.
//   
//   ✓ NEW: LoadFailureReason property — human-readable diagnosis
//     shown to user when Whisper can't work.
//   
//   ✓ All existing hallucination filters preserved.
//   ✓ All existing min audio length checks preserved.
//   ✓ All existing PTT logic preserved.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Whisper.net;

namespace ProGlassAutomation.Services.AI.AIVoice
{
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
        private bool _modelLoadAttempted;

        /// <summary>
        /// Human-readable reason for last model load failure.
        /// Displayed to user via StatusChanged when Whisper cannot work.
        /// Empty string if no failure.
        /// </summary>
        public string LoadFailureReason { get; private set; } = "";

        /// <summary>
        /// Minimum audio bytes required before processing.
        /// 16kHz × 16-bit × mono × 0.8 seconds ≈ 25,600 bytes.
        /// </summary>
        private const int MinAudioBytes = 25600;

        /// <summary>
        /// Minimum expected model file size in bytes.
        /// If file is smaller than this, treat as corrupted.
        /// Base model = 142MB. We use 100MB as minimum safe threshold
        /// (allows for compressed variants but catches partial downloads).
        /// </summary>
        private const long MinModelFileSize = 100L * 1024 * 1024;

        /// <summary>Common Whisper hallucination phrases.</summary>
        private static readonly string[] HallucinationPhrases =
        {
            "kawaru yo", "kawaru", "yoroshiku",
            "arigatou gozaimasu", "arigatou",
            "sumimasen", "gomen nasai",
            "thank you", "thanks", "thanks for watching",
            "thanks for listening", "thank you for watching",
            "please subscribe", "subscribe",
            "like and subscribe", "see you next time",
            "bye", "bye bye", "goodbye", "good bye",
            "okay", "ok", "um", "uh", "hmm",
            "you", "the", "a", "i", "it",
            "so", "and", "but", "or",
            "谢谢", "谢谢观看", "请订阅",
            "감사합니다",
            "subtitles by", "translated by",
            "captions by", "transcribed by",
            "[music]", "[blank_audio]", "[silence]",
            "(music)", "(silence)", "..."
        };

        // ── Properties ────────────────────────────────────────────────

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
        /// True if microphone hardware is available.
        /// FIX: No longer flipped to false when model load fails.
        /// The mic BUTTON stays visible. Load errors reported via
        /// StatusChanged so user knows and can take action.
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

        // ── Events ────────────────────────────────────────────────────

        public event EventHandler<SpeechRecognizedEventArgs> SpeechRecognized;
        public event EventHandler<bool> ListeningStateChanged;
        public event EventHandler<string> StatusChanged;
        public event EventHandler<float> AudioLevelChanged;

        /// <summary>
        /// Fires when Whisper model load fails permanently.
        /// VM should swap to SpeechToTextService fallback.
        /// EventArgs contains human-readable failure reason.
        /// </summary>
        public event EventHandler<string> ModelLoadFailed;

        // ── Constructor ───────────────────────────────────────────────

        public WhisperSpeechService()
        {
            _audioCapture = new AudioCaptureService();

            _audioCapture.AudioLevelChanged += (s, level) =>
                AudioLevelChanged?.Invoke(this, level);

            RefreshAvailability();
        }

        /// <summary>
        /// Availability = microphone exists.
        /// FIX: Removed WhisperModelManager.IsModelDownloaded check.
        /// Mic button visibility should depend on HARDWARE availability,
        /// not model download status. If model missing/broken, we handle
        /// it inside StartListening with clear error message.
        /// </summary>
        public void RefreshAvailability()
        {
            IsAvailable = _audioCapture.IsMicrophoneAvailable;
        }

        // ── Model file validation ─────────────────────────────────────

        /// <summary>
        /// Validates model file exists AND has plausible size.
        /// Returns tuple (isValid, reasonIfInvalid).
        /// </summary>
        private static (bool valid, string reason) ValidateModelFile()
        {
            if (!WhisperModelManager.IsModelDownloaded)
                return (false,
                    "Whisper model not downloaded. " +
                    "Go to Settings → Voice Assistant to download.");

            var path = WhisperModelManager.DefaultModelPath;

            try
            {
                var fi = new FileInfo(path);

                if (!fi.Exists)
                    return (false,
                        $"Model file not found at {path}");

                if (fi.Length < MinModelFileSize)
                    return (false,
                        $"Model file is corrupted or incomplete " +
                        $"({fi.Length / (1024 * 1024)} MB, " +
                        $"expected at least {MinModelFileSize / (1024 * 1024)} MB). " +
                        "Delete and re-download in Settings.");

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, $"Cannot access model file: {ex.Message}");
            }
        }

        // ── Model loading ─────────────────────────────────────────────

        public async Task<bool> LoadModelAsync()
        {
            // Validate file first
            var (valid, reason) = ValidateModelFile();
            if (!valid)
            {
                LoadFailureReason = reason;
                StatusChanged?.Invoke(this, $"⚠ {reason}");
                ModelLoadFailed?.Invoke(this, reason);
                return false;
            }

            var currentModelPath = WhisperModelManager.DefaultModelPath;

            if (_modelLoaded && _loadedModelPath == currentModelPath)
                return true;

            if (_processor != null)
            {
                try { _processor.Dispose(); } catch { }
                _processor = null;
                _modelLoaded = false;
            }

            try
            {
                StatusChanged?.Invoke(this, "Loading Whisper model (first use)...");

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
                _modelLoadAttempted = true;
                LoadFailureReason = "";

                StatusChanged?.Invoke(this, "Whisper ready — click 🎤 to speak");
                return true;
            }
            catch (Exception ex)
            {
                _modelLoaded = false;
                _modelLoadAttempted = true;

                // Detailed error diagnosis
                LoadFailureReason = ex switch
                {
                    DllNotFoundException =>
                        "Whisper native runtime missing. " +
                        "Reinstall the application or install " +
                        "Visual C++ Redistributable.",
                    UnauthorizedAccessException =>
                        "Cannot read model file — permission denied. " +
                        "Run as administrator or fix folder permissions.",
                    _ when ex.Message.Contains("invalid",
                        StringComparison.OrdinalIgnoreCase) =>
                        "Model file is corrupted. " +
                        "Delete and re-download in Settings.",
                    _ => $"Model load failed: {ex.Message}"
                };

                System.Diagnostics.Debug.WriteLine(
                    $"[WhisperSTT] Load failed: {ex.GetType().Name}: {ex.Message}");
                StatusChanged?.Invoke(this, $"⚠ {LoadFailureReason}");

                // Signal VM to fall back to System.Speech
                ModelLoadFailed?.Invoke(this, LoadFailureReason);
                return false;
            }
        }

        // ── PTT: Start/Stop ───────────────────────────────────────────

        public void StartListening()
        {
            if (!IsAvailable || _isListening || _isDisposed) return;

            // If model load already failed once, don't retry
            // VM should have swapped to fallback by now
            if (_modelLoadAttempted && !_modelLoaded)
            {
                StatusChanged?.Invoke(this,
                    string.IsNullOrEmpty(LoadFailureReason)
                        ? "⚠ Whisper unavailable"
                        : $"⚠ {LoadFailureReason}");
                return;
            }

            if (!_modelLoaded)
            {
                _ = Task.Run(async () =>
                {
                    var loaded = await LoadModelAsync();
                    if (loaded)
                    {
                        System.Windows.Application.Current?.Dispatcher
                            .Invoke(StartListeningInternal);
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
            StatusChanged?.Invoke(this,
                "🎤 Listening — click again to stop");
        }

        public async void StopListening()
        {
            if (!_isListening || _isDisposed) return;

            IsListening = false;
            ListeningStateChanged?.Invoke(this, false);
            StatusChanged?.Invoke(this, "Processing speech...");

            try
            {
                var audioBytes = await _audioCapture.StopAndGetAudioAsync();

                if (audioBytes == null || audioBytes.Length < MinAudioBytes)
                {
                    StatusChanged?.Invoke(this,
                        audioBytes == null
                            ? "No audio captured"
                            : "Recording too short — speak for at least 1 second");
                    return;
                }

                var text = await ProcessAudioAsync(audioBytes);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    SpeechRecognized?.Invoke(this,
                        new SpeechRecognizedEventArgs
                        {
                            Text = text.Trim(),
                            Confidence = 0.95f,
                            IsFinal = true
                        });
                    StatusChanged?.Invoke(this,
                        $"✓ Recognized: \"{text.Trim()}\"");
                }
                else
                {
                    StatusChanged?.Invoke(this,
                        "Could not recognize speech — try again");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WhisperSTT] Processing failed: {ex.Message}");
                StatusChanged?.Invoke(this, $"Error: {ex.Message}");
            }
        }

        public async Task<string> ListenOnceAsync(int timeoutMs = 10000)
        {
            if (!IsAvailable || _isDisposed) return null;

            if (!_modelLoaded)
            {
                var loaded = await LoadModelAsync();
                if (!loaded) return null;
            }

            var tcs = new TaskCompletionSource<string>();

            void Handler(object s, SpeechRecognizedEventArgs e)
                => tcs.TrySetResult(e.Text);

            SpeechRecognized += Handler;

            _audioCapture.StartRecording();
            IsListening = true;
            ListeningStateChanged?.Invoke(this, true);
            StatusChanged?.Invoke(this, "Listening...");

            _ = Task.Delay(timeoutMs).ContinueWith(_ =>
                tcs.TrySetResult(null));

            var result = await tcs.Task;

            if (_isListening)
            {
                IsListening = false;
                ListeningStateChanged?.Invoke(this, false);

                if (result == null)
                {
                    StatusChanged?.Invoke(this, "Processing speech...");
                    var audioBytes =
                        await _audioCapture.StopAndGetAudioAsync();

                    if (audioBytes != null &&
                        audioBytes.Length >= MinAudioBytes)
                    {
                        var text = await ProcessAudioAsync(audioBytes);
                        if (!string.IsNullOrWhiteSpace(text))
                            result = text.Trim();
                    }
                }
                else
                {
                    _audioCapture.CancelRecording();
                }
            }

            SpeechRecognized -= Handler;
            StatusChanged?.Invoke(this,
                result != null
                    ? "Speech recognized"
                    : "No speech detected");

            return result;
        }

        // ── Whisper inference ─────────────────────────────────────────

        private async Task<string> ProcessAudioAsync(byte[] wavBytes)
        {
            if (_processor == null || wavBytes == null) return null;

            try
            {
                using var stream = new MemoryStream(wavBytes);
                var sb = new StringBuilder();

                await foreach (var segment in
                    _processor.ProcessAsync(stream))
                {
                    sb.Append(segment.Text);
                }

                var result = sb.ToString().Trim();

                if (string.IsNullOrWhiteSpace(result)) return null;
                if (result.StartsWith("[") && result.EndsWith("]")) return null;
                if (result.StartsWith("(") && result.EndsWith(")")) return null;

                if (IsHallucination(result))
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WhisperSTT] Hallucination filtered: \"{result}\"");
                    return null;
                }

                if (result.Length < 3)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[WhisperSTT] Too short, filtered: \"{result}\"");
                    return null;
                }

                return result;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[WhisperSTT] ProcessAudioAsync failed: {ex.Message}");
                return null;
            }
        }

        private static bool IsHallucination(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;

            var cleaned = text
                .Trim()
                .TrimEnd('.', ',', '!', '?', '。', '、')
                .Trim()
                .ToLowerInvariant();

            foreach (var phrase in HallucinationPhrases)
            {
                if (cleaned == phrase.ToLowerInvariant())
                    return true;
            }

            var words = cleaned.Split(' ',
                StringSplitOptions.RemoveEmptyEntries);
            if (words.Length >= 2 && words.All(w => w == words[0]))
                return true;

            return false;
        }

        // ── Dispose ───────────────────────────────────────────────────

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