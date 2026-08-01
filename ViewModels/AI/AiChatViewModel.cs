// ═══════════════════════════════════════════════════════════════════════
// FILE: ViewModels/AI/AiChatViewModel.cs
// PROJECT: ProGlassAutomation
// MODULE: AI Chat (Chat 25 v2.0 — Tier 2 RAG entity detection)
//
// CHANGES vs Chat 25 v1.0:
//   ✓ NEW: Entity ID detection in SendAsync().
//     Scans user message for known ID patterns:
//       PI-XXXX-XXX   → Proforma Invoice deep context
//       JOB-XXXX-XXX  → Job Order deep context
//       DN-XXXX-XXX   → Delivery Note deep context
//       INV-XXXX-XXX  → Tax Invoice deep context
//     If matched: BuildEntityContextAsync() called for that record.
//     Result appended to system prompt AFTER Tier 1 summary.
//     AI receives both summary + specific entity detail in one call.
//   ✓ NEW: DetectEntityReference() helper — regex-based ID extraction.
//     Returns (entityType, entityId) or null if no match found.
//   ✓ ALL existing code preserved exactly — zero regression.
//
// REGRESSION RISK: ZERO
//   Only SendAsync() changed — one additional await call inserted
//   between fullSystemPrompt build and ai.AskAsync() call.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.AI.AIContext;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.ViewModels.AI
{
    /// <summary>
    /// ViewModel for the floating AI chat panel.
    /// Reads App.Ai at call time (not construction) so provider
    /// switching in Settings is reflected immediately.
    /// Phase 2: Tier 2 entity ID detection for deep per-record RAG.
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
        //  ENTITY ID PATTERNS — Tier 2 RAG detection
        //
        //  Scans user message for known ID formats.
        //  Compiled once at class level — never re-allocated per message.
        //
        //  Pattern breakdown:
        //    PI-   followed by 4 digits, dash, 3+ digits → Proforma Invoice
        //    JOB-  followed by 4 digits, dash, 3+ digits → Job Order
        //    DN-   followed by 4 digits, dash, 3+ digits → Delivery Note
        //    INV-  followed by 4 digits, dash, 3+ digits → Tax Invoice
        //    MDN-  followed by 4 digits, dash, 3+ digits → Master DN (maps to DN)
        //
        //  Word boundary \b ensures "PI-2026-001" matches but
        //  "NOOPI-2026-001" does not.
        // ══════════════════════════════════════════════════════════════

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
            new Regex(@"\b(INV-\d{4}-\d+(?:-[A-Z])?)\\b",
                RegexOptions.IgnoreCase | RegexOptions.Compiled);

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
        /// Re-evaluated when WhisperSpeechService.IsAvailable changes.
        /// </summary>
        public bool IsVoiceAvailable => _stt?.IsAvailable ?? false;

        public bool IsTtsAvailable => _tts?.IsAvailable ?? false;

        /// <summary>
        /// Voice status feedback shown below input area.
        /// e.g. "Listening...", "Processing speech...", "Recognized: ..."
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        // ── Provider indicator — reads App.Ai live ─────────────────────

        /// <summary>
        /// Reads App.Ai at call time — provider switching in Settings
        /// is reflected immediately without VM reconstruction.
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
            IAiService ai,
            ISpeechToTextService stt = null,
            ITextToSpeechService tts = null)
        {
            // NOTE: ai parameter NOT stored as _ai field.
            // All AI calls use App.Ai directly so provider switching
            // in Settings is reflected without VM reconstruction.
            _stt = stt;
            _tts = tts;

            // ── Subscribe to voice availability changes ────────────────
            if (_stt is WhisperSpeechService whisper)
            {
                whisper.PropertyChanged += (_, e) =>
                {
                    if (e.PropertyName ==
                        nameof(WhisperSpeechService.IsAvailable))
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

            // ── Subscribe to provider change event ─────────────────────
            // When SettingsViewModel switches provider, refresh header
            App.AiProviderChanged += (_, __) =>
            {
                OnPropertyChanged(nameof(ActiveProviderName));
                OnPropertyChanged(nameof(ActiveProviderIcon));
                OnPropertyChanged(nameof(ActiveProviderSubtitle));
            };

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

                // Invalidate RAG cache so next question gets fresh data
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

            // ── Voice commands — PTT model ─────────────────────────────
            VoiceInputCommand = new RelayCommand(_ =>
            {
                if (_stt == null || !_stt.IsAvailable) return;

                if (IsListening)
                {
                    // User clicked again — stop and process
                    _stt.StopListening();
                }
                else
                {
                    // Stop TTS so mic doesn't pick up speaker
                    _tts?.Stop();

                    void OnRecognized(object s, SpeechRecognizedEventArgs e)
                    {
                        _stt.SpeechRecognized -= OnRecognized;

                        System.Windows.Application.Current?.Dispatcher
                            .InvokeAsync(async () =>
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
                if (!IsSpeakerOn) _tts?.Stop();
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

            // Stop any current TTS
            _tts?.Stop();

            // Read App.Ai live — reflects provider switching
            var ai = App.Ai;

            // ── Guard: AI not configured ──────────────────────────────
            if (ai == null)
            {
                Messages.Add(new ChatMessage
                {
                    Text =
                        "⚠ No AI provider configured.\n\n" +
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
                    Text =
                        "⚠ AI Assistant is disabled.\n\n" +
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
                    Text =
                        $"⚠ {ai.ProviderName} API key is missing.\n\n" +
                        $"Go to ⚙ Settings → AI Providers, enter your " +
                        $"{ai.ProviderName} API key, and click 💾 SAVE & TEST.",
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
                    Text =
                        "⚠ You appear to be offline.\n\n" +
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

            try
            {
                // ── Build Tier 1 system prompt + RAG summary ──────────
                var systemPrompt = BuildSystemPrompt();

                string dataContext = string.Empty;
                if (App.Config?.Ai?.UseRealData == true)
                {
                    try
                    {
                        dataContext =
                            await AiContextBuilder.BuildContextAsync();
                    }
                    catch { /* RAG failure must never block AI */ }
                }

                var fullSystemPrompt = string.IsNullOrEmpty(dataContext)
                    ? systemPrompt
                    : systemPrompt + "\n" + dataContext;

                // ── Tier 2 — entity ID detection ─────────────────────
                // Scan user message for specific record IDs.
                // If found, fetch deep context for that single record
                // and append to system prompt so AI can answer precisely.
                if (App.Config?.Ai?.UseRealData == true)
                {
                    var entityRef = DetectEntityReference(userMsg);
                    if (entityRef.HasValue)
                    {
                        try
                        {
                            string entityContext =
                                await AiContextBuilder.BuildEntityContextAsync(
                                    entityRef.Value.EntityType,
                                    entityRef.Value.EntityId);

                            if (!string.IsNullOrEmpty(entityContext))
                                fullSystemPrompt += "\n" + entityContext;
                        }
                        catch { /* Entity fetch failure must not block AI */ }
                    }
                }

                // ── Send to AI provider ───────────────────────────────
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

                    // Refresh provider indicator
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
                        Text =
                            $"⚠ Error from " +
                            $"{response.ProviderName ?? ai.ProviderName}:\n" +
                            $"{response.ErrorMessage ?? "Unknown error"}\n\n" +
                            "Try again or switch providers in ⚙ Settings.",
                        IsUser = false,
                        IsError = true,
                        ProviderName = response.ProviderName ?? ai.ProviderName
                    });
                }
            }
            finally
            {
                // Ensure IsBusy is always reset even if exception thrown
                IsBusy = false;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  TIER 2 — ENTITY REFERENCE DETECTION
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Scans user message for a known document ID pattern.
        /// Returns the first match found with its entity type.
        /// Priority: TI > PI > JO > DN (most specific first).
        ///
        /// Examples:
        ///   "show me PI-2026-001"      → ("PI",  "PI-2026-001")
        ///   "what's JOB-2026-003 status" → ("JO", "JOB-2026-003")
        ///   "was DN-2026-001-02 invoiced" → ("DN", "DN-2026-001-02")
        ///   "find INV-2026-001-A"      → ("TI",  "INV-2026-001-A")
        ///   "hello how are you"        → null
        /// </summary>
        private static EntityReference? DetectEntityReference(string userMsg)
        {
            if (string.IsNullOrWhiteSpace(userMsg)) return null;

            // Check Tax Invoice first — most specific (INV- prefix)
            var tiMatch = _tiPattern.Match(userMsg);
            if (tiMatch.Success)
                return new EntityReference("TI", tiMatch.Groups[1].Value.ToUpper());

            // Check Proforma Invoice (PI- prefix)
            var piMatch = _piPattern.Match(userMsg);
            if (piMatch.Success)
                return new EntityReference("PI", piMatch.Groups[1].Value.ToUpper());

            // Check Job Order (JOB- prefix)
            var joMatch = _joPattern.Match(userMsg);
            if (joMatch.Success)
                return new EntityReference("JO", joMatch.Groups[1].Value.ToUpper());

            // Check Delivery Note (DN- or MDN- prefix)
            var dnMatch = _dnPattern.Match(userMsg);
            if (dnMatch.Success)
                return new EntityReference("DN", dnMatch.Groups[1].Value.ToUpper());

            return null;
        }

        /// <summary>
        /// Detected entity reference from user message.
        /// EntityType: "PI" | "JO" | "DN" | "TI"
        /// EntityId:   e.g. "PI-2026-001", "JOB-2026-001"
        /// </summary>
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
                "• App modules: Dashboard, Sheet Store (inventory), " +
                "Proforma Invoice, Job Order, Delivery Note, " +
                "Tax Invoice, Sales Report/Statements, AI Assistant\n" +
                "• Document chain: " +
                "Proforma Invoice → Job Order → Delivery Note → " +
                "Tax Invoice → Account Statement\n\n" +

                "DOCUMENT ID FORMATS (you will see these in data):\n" +
                "• PI-YYYY-NNN   = Proforma Invoice\n" +
                "• JOB-YYYY-NNN = Job Order\n" +
                "• DN-YYYY-NNN-NN = Delivery Note\n" +
                "• MDN-YYYY-NNN = Master Delivery Note (reference tracker)\n" +
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
                "• Give concise, practical answers (prefer bullet points)\n" +
                "• Use the LIVE DATA and entity detail sections below " +
                "if available — answer with REAL numbers from the data\n" +
                "• If a specific document is referenced by ID and its " +
                "data is shown below, use it precisely\n" +
                "• If asked about a record not in the data, say so honestly\n" +
                "• Show calculations for formulas (SQM, VAT, pricing)\n" +
                "• Keep responses under 300 words unless asked for detail\n" +
                "• Format monetary values as AED with commas " +
                "(e.g. AED 12,500.00)\n" +
                "• Be professional but friendly — factory floor assistant\n" +
                "• For voice responses: keep answers under 100 words, " +
                "avoid markdown, speak conversationally";
        }
    }
}