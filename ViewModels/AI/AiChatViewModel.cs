// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/AI/AiChatViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Chat (Chat 25 v1.0 — provider live swap + PTT voice fix)
//
// CHANGES vs Chat 24 v1.2:
//   ✓ FIX BUG #1/#2: AI guard now shows helpful error instead of silent
//     failure. Error message tells user exactly what's wrong.
//   ✓ FIX WARNING #2: _ai no longer captured at construction time.
//     Now reads App.Ai at call time so provider switching in Settings
//     takes effect immediately without VM reconstruction.
//   ✓ FIX BUG #5: VoiceInputCommand now uses PTT model (Start/Stop)
//     instead of ListenOnceAsync(10000) which waited 10 full seconds.
//     First click = start recording, second click = stop + process.
//     Result appears in 1-3 seconds, not 10.
//   ✓ FIX BUG #4: IsVoiceAvailable now bound properly — WhisperSpeechService
//     raises PropertyChanged on IsAvailable, VM re-evaluates.
//   ✓ Added StatusMessage property for voice status display in chat.
//   ✓ All existing commands preserved.
//
// REGRESSION RISK: LOW — all existing bindings preserved.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIContext;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.AI
{
    /// <summary>
    /// ViewModel for the floating AI chat panel.
    /// Reads App.Ai at call time (not construction) so provider
    /// switching in Settings is reflected immediately.
    /// </summary>
    public class AiChatViewModel : ViewModelBase
    {
        // ── Voice services (nullable — disabled gracefully if null) ───
        private readonly ISpeechToTextService _stt;
        private readonly ITextToSpeechService _tts;

        private string _currentInput;
        private bool _isBusy;
        private bool _isOpen;
        private bool _isListening;
        private bool _isSpeakerOn;
        private string _statusMessage;

        // ══════════════════════════════════════════════════════════════
        //  COLLECTIONS
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public ObservableCollection<string> Suggestions { get; } = new()
        {
            "What's the best glass for a shower door?",
            "How do I create a proforma invoice?",
            "Explain the tempering process",
            "What's my outstanding balance?",
            "Show me pending deliveries"
        };

        // ══════════════════════════════════════════════════════════════
        //  PROPERTIES
        // ══════════════════════════════════════════════════════════════

        public string CurrentInput
        {
            get => _currentInput;
            set { _currentInput = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSend));
            }
        }

        public bool IsOpen
        {
            get => _isOpen;
            set { _isOpen = value; OnPropertyChanged(); }
        }

        public bool HasMessages => Messages.Count > 0;
        public bool CanSend => !IsBusy;

        // ── Voice ─────────────────────────────────────────────────────

        public bool IsListening
        {
            get => _isListening;
            set { _isListening = value; OnPropertyChanged(); }
        }

        public bool IsSpeakerOn
        {
            get => _isSpeakerOn;
            set { _isSpeakerOn = value; OnPropertyChanged(); }
        }

        /// <summary>
        /// True if speech recognition is available.
        /// Re-evaluated when WhisperSpeechService.IsAvailable changes
        /// (e.g. after model download in Settings).
        /// </summary>
        public bool IsVoiceAvailable => _stt?.IsAvailable ?? false;

        public bool IsTtsAvailable => _tts?.IsAvailable ?? false;

        /// <summary>
        /// Short status message shown below input area.
        /// Used for voice feedback: "Listening...", "Processing...", etc.
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        // ── Provider indicator — reads App.Ai live ────────────────────

        /// <summary>
        /// FIX WARNING #2: Reads App.Ai at call time, not captured at
        /// construction. Provider switching in Settings takes effect
        /// immediately — no app restart needed.
        /// </summary>
        public string ActiveProviderName => App.Ai?.ProviderName ?? "AI";
        public string ActiveProviderIcon => App.Ai?.ProviderIcon ?? "🤖";
        public string ActiveProviderSubtitle => App.Ai != null
            ? $"Powered by {App.Ai.ProviderName}"
            : "Configure provider in Settings";

        // ══════════════════════════════════════════════════════════════
        //  COMMANDS
        // ══════════════════════════════════════════════════════════════

        public ICommand SendCommand { get; }
        public ICommand ToggleChatCommand { get; }
        public ICommand ClearChatCommand { get; }
        public ICommand CloseChatCommand { get; }
        public ICommand SuggestionCommand { get; }
        public ICommand VoiceInputCommand { get; }
        public ICommand ToggleSpeakerCommand { get; }
        public ICommand StopSpeakingCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════

        public AiChatViewModel(
            IAiService ai,               // kept for signature compat — not stored
            ISpeechToTextService stt = null,
            ITextToSpeechService tts = null)
        {
            // NOTE: ai parameter intentionally NOT stored as _ai field.
            // All AI calls use App.Ai directly so provider switching
            // in Settings is reflected without VM reconstruction.
            _stt = stt;
            _tts = tts;

            // ── Subscribe to voice availability changes ────────────────
            // When user downloads Whisper model in Settings and download
            // completes, WhisperSpeechService raises PropertyChanged on
            // IsAvailable. We re-notify our own IsVoiceAvailable.
            if (_stt is WhisperSpeechService whisper)
            {
                whisper.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName == nameof(WhisperSpeechService.IsAvailable))
                        OnPropertyChanged(nameof(IsVoiceAvailable));
                };

                whisper.StatusChanged += (_, status) =>
                {
                    StatusMessage = status;
                };
            }

            if (_stt != null)
            {
                _stt.ListeningStateChanged += (_, listening) =>
                {
                    IsListening = listening;
                };
            }

            // ── Text commands ─────────────────────────────────────────
            SendCommand = new RelayCommand(
                async _ => await SendAsync(),
                _ => CanSend);

            ToggleChatCommand = new RelayCommand(_ => IsOpen = !IsOpen);

            CloseChatCommand = new RelayCommand(_ =>
            {
                IsOpen = false;
                StopAllVoice();
            });

            ClearChatCommand = new RelayCommand(_ =>
            {
                Messages.Clear();
                OnPropertyChanged(nameof(HasMessages));
                StopAllVoice();
                StatusMessage = string.Empty;
            });

            SuggestionCommand = new RelayCommand(async param =>
            {
                if (param is string text && !string.IsNullOrWhiteSpace(text))
                {
                    CurrentInput = text;
                    await SendAsync();
                }
            });

            // ── Voice commands — PTT model ────────────────────────────
            // FIX BUG #5: Push-To-Talk
            // First click → StartListening (recording begins)
            // Second click → StopListening (recording stops, Whisper processes)
            // Result fires via SpeechRecognized event → auto-fills + sends
            VoiceInputCommand = new RelayCommand(_ =>
            {
                if (_stt == null || !_stt.IsAvailable) return;

                if (IsListening)
                {
                    // User clicked again — stop recording, process audio
                    _stt.StopListening();
                    // IsListening updated via ListeningStateChanged event
                }
                else
                {
                    // Stop any current TTS so mic doesn't pick up speaker
                    _tts?.Stop();

                    // Wire one-time handler for this recording session
                    void OnRecognized(object s, SpeechRecognizedEventArgs e)
                    {
                        _stt.SpeechRecognized -= OnRecognized;

                        // Marshal to UI thread
                        System.Windows.Application.Current?.Dispatcher.InvokeAsync(
                            async () =>
                            {
                                if (!string.IsNullOrWhiteSpace(e.Text))
                                {
                                    CurrentInput = e.Text;
                                    StatusMessage = $"Recognized: \"{e.Text}\"";
                                    await SendAsync();
                                }
                            });
                    }

                    _stt.SpeechRecognized += OnRecognized;
                    _stt.StartListening();
                }
            });

            ToggleSpeakerCommand = new RelayCommand(_ =>
            {
                IsSpeakerOn = !IsSpeakerOn;
                if (!IsSpeakerOn)
                    _tts?.Stop();
            });

            StopSpeakingCommand = new RelayCommand(_ =>
            {
                _tts?.Stop();
            });

            Messages.CollectionChanged += (_, __) =>
                OnPropertyChanged(nameof(HasMessages));
        }

        // ══════════════════════════════════════════════════════════════
        //  SEND MESSAGE
        // ══════════════════════════════════════════════════════════════

        private async Task SendAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentInput)) return;

            // Stop any current speech
            _tts?.Stop();

            // ── Read App.Ai live — reflects provider switching ─────────
            var ai = App.Ai;

            // ── Guard: AI not configured ──────────────────────────────
            if (ai == null)
            {
                Messages.Add(new ChatMessage
                {
                    Text = "⚠ No AI provider configured.\n\n" +
                           "Go to ⚙ Settings → AI Providers and enter " +
                           "your API key for Gemini, Groq, or Cohere.",
                    IsUser = false,
                    IsError = true
                });
                return;
            }

            if (!ai.IsEnabled)
            {
                Messages.Add(new ChatMessage
                {
                    Text = "⚠ AI Assistant is disabled.\n\n" +
                           "Go to ⚙ Settings → AI Assistant and toggle it ON.",
                    IsUser = false,
                    IsError = true
                });
                return;
            }

            if (!ai.IsConfigured)
            {
                Messages.Add(new ChatMessage
                {
                    Text = $"⚠ {ai.ProviderName} API key is missing.\n\n" +
                           "Go to ⚙ Settings → AI Providers, enter your " +
                           $"{ai.ProviderName} API key, and click " +
                           "💾 SAVE & TEST.",
                    IsUser = false,
                    IsError = true
                });
                return;
            }

            // ── Guard: Offline ────────────────────────────────────────
            if (App.Connectivity != null && !App.Connectivity.IsOnline)
            {
                Messages.Add(new ChatMessage
                {
                    Text = "⚠ You appear to be offline.\n\n" +
                           $"AI requires internet to reach {ai.ProviderName}. " +
                           "Please check your network connection.",
                    IsUser = false,
                    IsError = true
                });
                return;
            }

            // ── Add user message ──────────────────────────────────────
            var userMsg = CurrentInput.Trim();
            Messages.Add(new ChatMessage { Text = userMsg, IsUser = true });
            CurrentInput = string.Empty;
            StatusMessage = string.Empty;

            IsBusy = true;

            // ── Build system prompt + RAG context ─────────────────────
            var systemPrompt = BuildSystemPrompt();

            string dataContext = string.Empty;
            if (App.Config?.Ai?.UseRealData == true)
            {
                try
                {
                    dataContext = await AiContextBuilder.BuildContextAsync();
                }
                catch { /* RAG failure must not block AI */ }
            }

            var fullSystemPrompt = string.IsNullOrEmpty(dataContext)
                ? systemPrompt
                : systemPrompt + "\n" + dataContext;

            // ── Send to AI ────────────────────────────────────────────
            var response = await ai.AskAsync(fullSystemPrompt, userMsg);

            IsBusy = false;

            if (response.Success)
            {
                var aiText = response.Text?.Trim() ?? "(empty response)";

                Messages.Add(new ChatMessage
                {
                    Text = aiText,
                    IsUser = false,
                    ProviderName = response.ProviderName ?? ai.ProviderName,
                    ProviderIcon = ai.ProviderIcon
                });

                // Notify provider indicator in case it changed
                OnPropertyChanged(nameof(ActiveProviderName));
                OnPropertyChanged(nameof(ActiveProviderIcon));
                OnPropertyChanged(nameof(ActiveProviderSubtitle));

                // Read aloud if speaker ON
                if (IsSpeakerOn && _tts != null && _tts.IsAvailable)
                    _tts.SpeakAsync(aiText);
            }
            else
            {
                Messages.Add(new ChatMessage
                {
                    Text = $"⚠ Error from {response.ProviderName ?? ai.ProviderName}:\n" +
                           $"{response.ErrorMessage ?? "Unknown error"}\n\n" +
                           "Try again or switch providers in ⚙ Settings.",
                    IsUser = false,
                    IsError = true,
                    ProviderName = response.ProviderName ?? ai.ProviderName
                });
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  VOICE HELPERS
        // ══════════════════════════════════════════════════════════════

        private void StopAllVoice()
        {
            if (_stt != null && _stt.IsListening)
            {
                _stt.StopListening();
                IsListening = false;
            }
            _tts?.Stop();
        }

        // ══════════════════════════════════════════════════════════════
        //  SYSTEM PROMPT
        // ══════════════════════════════════════════════════════════════

        private static string BuildSystemPrompt()
        {
            return
                "You are ProGlass AI — an expert assistant for glass manufacturing " +
                "in the UAE. You help users of the ProGlass Automation ERP system.\n\n" +

                "YOUR KNOWLEDGE AREAS:\n" +
                "• Glass types: clear, tinted, frosted, tempered, laminated, " +
                "insulated (DGU/IGU), low-E coated, reflective, patterned\n" +
                "• Processing: cutting, holes/cutouts, polish, edge deletion, " +
                "mitring, tempering, pairing, doubling/DGU assembly\n" +
                "• UAE market: AED currency, 5% VAT, FTA compliance, " +
                "delivery logistics\n" +
                "• App modules: Dashboard, Sheet Store (inventory), " +
                "Proforma Invoice, Job Order, Delivery Note, Tax Invoice, " +
                "Sales Report/Statements, AI Assistant\n\n" +

                "GUIDELINES:\n" +
                "• Give concise, practical answers (prefer bullet points)\n" +
                "• Use the LIVE DATA section below if available for real numbers\n" +
                "• If asked about a specific invoice/job you don't have data for, " +
                "say so honestly\n" +
                "• Show calculations for formulas (SQM, VAT, pricing)\n" +
                "• Keep responses under 300 words unless asked for detail\n" +
                "• Format monetary values as AED with commas (e.g. AED 12,500.00)\n" +
                "• Be professional but friendly — factory floor assistant\n" +
                "• When the user is speaking via voice, keep answers shorter " +
                "and more conversational (under 100 words preferred for voice)";
        }
    }
}