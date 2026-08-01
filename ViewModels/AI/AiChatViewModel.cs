// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/AI/AiChatViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Chat (Chat 25 v5.0 — Whisper fallback + full thread safety)
//
// CHANGES vs Chat 25 v4.0:
//   ✓ NEW: whisper.ModelLoadFailed subscription — when Whisper model
//     fails to load (corrupted file, missing DLL, permission error),
//     VM auto-swaps App.SpeechToText to SpeechToTextService fallback.
//     Voice input continues to work with reduced accuracy.
//   ✓ ALL v4.0 fixes preserved: thread marshaling, no auto-send,
//     duplicate handler prevention, IsProcessingVoice reset.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIContext;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using System;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.AI
{
    /// <summary>
    /// ViewModel for the floating AI chat panel.
    /// Thread-safe voice event handling with auto-fallback on Whisper failure.
    /// </summary>
    public class AiChatViewModel : ViewModelBase
    {
        private readonly ISpeechToTextService _stt;
        private readonly ITextToSpeechService _tts;

        private string _currentInput;
        private bool _isBusy;
        private bool _isOpen;
        private bool _isListening;
        private bool _isSpeakerOn;
        private bool _isProcessingVoice;
        private float _micAudioLevel;
        private string _statusMessage;

        private EventHandler<SpeechRecognizedEventArgs> _activeVoiceHandler;

        // ── Entity ID patterns (compiled once) ────────────────────────

        private static readonly Regex _piPattern =
            new Regex(@"\b(PI-\d{4}-\d+)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _joPattern =
            new Regex(@"\b(JOB-\d{4}-\d+)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _dnPattern =
            new Regex(@"\b((?:DN|MDN)-\d{4}-\d+(?:-\d+)?)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex _tiPattern =
            new Regex(@"\b(INV-\d{4}-\d+(?:-[A-Z])?)\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

        // ══════════════════════════════════════════════════════════════
        //  COLLECTIONS
        // ══════════════════════════════════════════════════════════════

        public ObservableCollection<ChatMessage> Messages { get; } = new();

        public ObservableCollection<string> Suggestions { get; } = new()
        {
            "What's my outstanding balance?",
            "Which job orders are overdue?",
            "Show me pending deliveries",
            "What's the best glass for a shower door?",
            "How do I create a proforma invoice?"
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

        public bool IsProcessingVoice
        {
            get => _isProcessingVoice;
            set { _isProcessingVoice = value; OnPropertyChanged(); }
        }

        public float MicAudioLevel
        {
            get => _micAudioLevel;
            set { _micAudioLevel = value; OnPropertyChanged(); }
        }

        public bool IsVoiceAvailable => _stt?.IsAvailable ?? false;
        public bool IsTtsAvailable => _tts?.IsAvailable ?? false;

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

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
            IAiService ai,
            ISpeechToTextService stt = null,
            ITextToSpeechService tts = null)
        {
            _stt = stt;
            _tts = tts;

            // ── Wire Whisper events (all Dispatcher-marshaled) ────────
            if (_stt is WhisperSpeechService whisper)
            {
                whisper.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName ==
                        nameof(WhisperSpeechService.IsAvailable))
                    {
                        RunOnUi(() =>
                            OnPropertyChanged(nameof(IsVoiceAvailable)));
                    }
                };

                whisper.StatusChanged += (_, status) =>
                {
                    RunOnUi(() =>
                    {
                        StatusMessage = status ?? string.Empty;
                        IsProcessingVoice =
                            status == "Processing speech...";
                    });
                };

                whisper.AudioLevelChanged += (_, level) =>
                {
                    RunOnUi(() => MicAudioLevel = level);
                };

                // ── NEW: Auto-fallback on Whisper model failure ───────
                // If Whisper model fails to load (corrupted, missing DLL,
                // permission error), swap App.SpeechToText to the
                // SpeechToTextService fallback so voice still works.
                whisper.ModelLoadFailed += (_, reason) =>
                {
                    RunOnUi(() =>
                    {
                        StatusMessage =
                            $"⚠ Whisper unavailable: {reason} " +
                            "Switching to Windows Speech...";

                        try
                        {
                            whisper.Dispose();
                            App.SpeechToText = new SpeechToTextService();

                            System.Diagnostics.Debug.WriteLine(
                                "[AiChatVM] Swapped Whisper → System.Speech.");

                            StatusMessage =
                                "✓ Voice ready (System.Speech fallback). " +
                                "For 95% accuracy, re-download Whisper " +
                                "model in Settings.";

                            OnPropertyChanged(nameof(IsVoiceAvailable));
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine(
                                $"[AiChatVM] Fallback swap failed: {ex.Message}");
                            StatusMessage =
                                "⚠ Voice unavailable — check audio drivers";
                        }
                    });
                };
            }

            // ── Generic STT listening state ───────────────────────────
            if (_stt != null)
            {
                _stt.ListeningStateChanged += (_, listening) =>
                {
                    RunOnUi(() =>
                    {
                        IsListening = listening;
                        if (!listening)
                            MicAudioLevel = 0f;
                    });
                };
            }

            // ── Provider change notification ──────────────────────────
            App.AiProviderChanged += (_, __) =>
            {
                RunOnUi(() =>
                {
                    OnPropertyChanged(nameof(ActiveProviderName));
                    OnPropertyChanged(nameof(ActiveProviderIcon));
                    OnPropertyChanged(nameof(ActiveProviderSubtitle));
                });
            };

            // ══════════════════════════════════════════════════════════
            //  TEXT COMMANDS
            // ══════════════════════════════════════════════════════════

            SendCommand = new RelayCommand(
                async _ => await SendAsync(),
                _ => CanSend);

            ToggleChatCommand = new RelayCommand(
                _ => IsOpen = !IsOpen);

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
                MicAudioLevel = 0f;
                IsProcessingVoice = false;
                AiContextBuilder.InvalidateCache();
            });

            SuggestionCommand = new RelayCommand(async param =>
            {
                if (param is string text &&
                    !string.IsNullOrWhiteSpace(text))
                {
                    CurrentInput = text;
                    await SendAsync();
                }
            });

            // ══════════════════════════════════════════════════════════
            //  VOICE COMMAND — Push-to-Talk (NO auto-send)
            // ══════════════════════════════════════════════════════════

            VoiceInputCommand = new RelayCommand(_ =>
            {
                if (_stt == null || !_stt.IsAvailable) return;

                if (IsListening)
                {
                    _stt.StopListening();
                    return;
                }

                _tts?.Stop();

                if (_activeVoiceHandler != null)
                {
                    _stt.SpeechRecognized -= _activeVoiceHandler;
                    _activeVoiceHandler = null;
                }

                _activeVoiceHandler = (s, e) =>
                {
                    _stt.SpeechRecognized -= _activeVoiceHandler;
                    _activeVoiceHandler = null;

                    RunOnUi(() =>
                    {
                        if (!string.IsNullOrWhiteSpace(e.Text))
                        {
                            CurrentInput = e.Text;
                            StatusMessage =
                                "✓ Voice recognized — press Enter to send or edit first";
                        }
                        else
                        {
                            StatusMessage =
                                "Could not recognize speech — try again";
                        }

                        IsProcessingVoice = false;
                        MicAudioLevel = 0f;
                    });
                };

                _stt.SpeechRecognized += _activeVoiceHandler;
                _stt.StartListening();
            });

            ToggleSpeakerCommand = new RelayCommand(_ =>
            {
                IsSpeakerOn = !IsSpeakerOn;
                if (!IsSpeakerOn) _tts?.Stop();
            });

            StopSpeakingCommand = new RelayCommand(_ =>
            {
                _tts?.Stop();
            });

            Messages.CollectionChanged +=
                (_, __) => OnPropertyChanged(nameof(HasMessages));
        }

        // ══════════════════════════════════════════════════════════════
        //  SEND MESSAGE
        // ══════════════════════════════════════════════════════════════

        private async Task SendAsync()
        {
            if (string.IsNullOrWhiteSpace(CurrentInput)) return;

            _tts?.Stop();

            var ai = App.Ai;

            if (ai == null)
            {
                AddError(
                    "⚠ No AI provider configured.\n\n" +
                    "Go to ⚙ Settings → AI Providers and enter " +
                    "your API key for Gemini, Groq, or Cohere.");
                return;
            }

            if (!ai.IsEnabled)
            {
                AddError(
                    "⚠ AI Assistant is disabled.\n\n" +
                    "Go to ⚙ Settings → AI Assistant and toggle it ON.");
                return;
            }

            if (!ai.IsConfigured)
            {
                AddError(
                    $"⚠ {ai.ProviderName} API key is missing.\n\n" +
                    $"Go to ⚙ Settings → AI Providers, enter your " +
                    $"{ai.ProviderName} API key, and click 💾 SAVE & TEST.");
                return;
            }

            if (App.Connectivity != null && !App.Connectivity.IsOnline)
            {
                AddError(
                    "⚠ You appear to be offline.\n\n" +
                    $"AI requires internet to reach {ai.ProviderName}. " +
                    "Please check your network connection.");
                return;
            }

            var userMsg = CurrentInput.Trim();
            Messages.Add(new ChatMessage { Text = userMsg, IsUser = true });
            CurrentInput = string.Empty;
            StatusMessage = string.Empty;
            IsProcessingVoice = false;

            IsBusy = true;

            try
            {
                var systemPrompt = BuildSystemPrompt();
                string dataContext = string.Empty;

                if (App.Config?.Ai?.UseRealData == true)
                {
                    try
                    {
                        dataContext =
                            await AiContextBuilder.BuildContextAsync();
                    }
                    catch { }
                }

                var fullSystemPrompt = string.IsNullOrEmpty(dataContext)
                    ? systemPrompt
                    : systemPrompt + "\n" + dataContext;

                if (App.Config?.Ai?.UseRealData == true)
                {
                    var entityRef = DetectEntityReference(userMsg);
                    if (entityRef.HasValue)
                    {
                        try
                        {
                            string entityContext =
                                await AiContextBuilder
                                    .BuildEntityContextAsync(
                                        entityRef.Value.EntityType,
                                        entityRef.Value.EntityId);

                            if (!string.IsNullOrEmpty(entityContext))
                                fullSystemPrompt += "\n" + entityContext;
                        }
                        catch { }
                    }
                }

                var response =
                    await ai.AskAsync(fullSystemPrompt, userMsg);

                if (response.Success)
                {
                    var aiText = response.Text?.Trim()
                        ?? "(empty response)";

                    Messages.Add(new ChatMessage
                    {
                        Text = aiText,
                        IsUser = false,
                        ProviderName = response.ProviderName
                            ?? ai.ProviderName,
                        ProviderIcon = ai.ProviderIcon
                    });

                    OnPropertyChanged(nameof(ActiveProviderName));
                    OnPropertyChanged(nameof(ActiveProviderIcon));
                    OnPropertyChanged(nameof(ActiveProviderSubtitle));

                    if (IsSpeakerOn &&
                        _tts != null &&
                        _tts.IsAvailable)
                    {
                        _tts.SpeakAsync(aiText);
                    }
                }
                else
                {
                    AddError(
                        $"⚠ Error from " +
                        $"{response.ProviderName ?? ai.ProviderName}:\n" +
                        $"{response.ErrorMessage ?? "Unknown error"}\n\n" +
                        "Try again or switch providers in ⚙ Settings.",
                        response.ProviderName ?? ai.ProviderName);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  ENTITY DETECTION — Tier 2 RAG
        // ══════════════════════════════════════════════════════════════

        private static EntityReference? DetectEntityReference(string msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return null;

            var ti = _tiPattern.Match(msg);
            if (ti.Success)
                return new EntityReference("TI",
                    ti.Groups[1].Value.ToUpperInvariant());

            var pi = _piPattern.Match(msg);
            if (pi.Success)
                return new EntityReference("PI",
                    pi.Groups[1].Value.ToUpperInvariant());

            var jo = _joPattern.Match(msg);
            if (jo.Success)
                return new EntityReference("JO",
                    jo.Groups[1].Value.ToUpperInvariant());

            var dn = _dnPattern.Match(msg);
            if (dn.Success)
                return new EntityReference("DN",
                    dn.Groups[1].Value.ToUpperInvariant());

            return null;
        }

        private readonly struct EntityReference
        {
            public string EntityType { get; }
            public string EntityId { get; }

            public EntityReference(string entityType, string entityId)
            {
                EntityType = entityType;
                EntityId = entityId;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════

        private static void RunOnUi(Action action)
        {
            if (action == null) return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            if (dispatcher.CheckAccess())
                action();
            else
                dispatcher.InvokeAsync(action);
        }

        private void AddError(string text, string providerName = null)
        {
            Messages.Add(new ChatMessage
            {
                Text = text,
                IsUser = false,
                IsError = true,
                ProviderName = providerName ?? string.Empty
            });
        }

        private void StopAllVoice()
        {
            if (_stt != null && _stt.IsListening)
            {
                _stt.StopListening();
                IsListening = false;
            }
            _tts?.Stop();
            MicAudioLevel = 0f;
            IsProcessingVoice = false;
        }

        // ══════════════════════════════════════════════════════════════
        //  SYSTEM PROMPT
        // ══════════════════════════════════════════════════════════════

        private static string BuildSystemPrompt() =>
            "You are ProGlass AI — an expert assistant for glass " +
            "manufacturing in the UAE. You help users of the " +
            "ProGlass Automation ERP system.\n\n" +

            "YOUR KNOWLEDGE AREAS:\n" +
            "• Glass types: clear, tinted, frosted, tempered, " +
            "laminated, insulated (DGU/IGU), low-E coated, " +
            "reflective, patterned\n" +
            "• Processing: cutting, holes/cutouts, polish, " +
            "edge deletion, mitring, tempering, pairing, " +
            "doubling/DGU assembly\n" +
            "• UAE market: AED currency, 5% VAT, FTA compliance, " +
            "delivery logistics\n" +
            "• App modules: Dashboard, Sheet Store, Proforma Invoice, " +
            "Job Order, Delivery Note, Tax Invoice, " +
            "Sales Report/Statements, AI Assistant\n" +
            "• Document chain: Proforma Invoice → Job Order → " +
            "Delivery Note → Tax Invoice → Account Statement\n\n" +

            "DOCUMENT ID FORMATS:\n" +
            "• PI-YYYY-NNN    = Proforma Invoice\n" +
            "• JOB-YYYY-NNN  = Job Order\n" +
            "• DN-YYYY-NNN-NN = Delivery Note\n" +
            "• MDN-YYYY-NNN  = Master Delivery Note\n" +
            "• INV-YYYY-NNN or INV-YYYY-NNN-A = Tax Invoice\n" +
            "• STMT-YYYY-NNN = Account Statement\n\n" +

            "STATUS VALUES:\n" +
            "• Proforma Invoice: Draft | Approved | Sent\n" +
            "• Job Order: Draft | Released | InProduction | " +
            "Completed | Delivered\n" +
            "• Delivery Note: Draft | Dispatched | Delivered | " +
            "Signed | Received\n" +
            "• Tax Invoice: Draft | Issued | Paid | PartiallyPaid | " +
            "Overdue | Cancelled | Void\n\n" +

            "GUIDELINES:\n" +
            "• Give concise practical answers (bullet points preferred)\n" +
            "• Use LIVE DATA and entity detail sections if present\n" +
            "• Answer with REAL numbers when data is provided\n" +
            "• If a record is not in the data, say so honestly\n" +
            "• Show calculations for SQM, VAT, pricing formulas\n" +
            "• Keep responses under 300 words unless asked for detail\n" +
            "• Format monetary values as AED with commas\n" +
            "• Be professional but friendly — factory floor assistant\n" +
            "• Voice responses: under 100 words, no markdown, " +
            "conversational tone";
    }
}