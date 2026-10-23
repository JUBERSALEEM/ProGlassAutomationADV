// ═══════════════════════════════════════════════════════════════════════
// FILE: AiChatViewModel.Part1.cs (Phase 3.8 split — member census verified)
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Services.AI.AIContext;
using ProGlassAutomation.Services.AI.AIService;
using ProGlassAutomation.Services.AI.AIVoice;
using System;
using ProGlassAutomation.Utilities;
using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using ProGlassAutomation.Services.AI.AIConfig;
using ProGlassAutomation.Services.AI.AIConnectivity;

namespace ProGlassAutomation.ViewModels.AI
{
        public partial class AiChatViewModel : ViewModelBase
    {
            // VOICE-FIX: was readonly. When Whisper failed mid-session the
        // fallback service could never replace it, so IsVoiceAvailable
        // kept reading the disposed Whisper → the mic button and voice
        // assistant UI silently disappeared ("voice assistant hidden").
        // Now mutable + swapped safely in the ModelLoadFailed handler.
        private ISpeechToTextService _stt;
        private readonly ITextToSpeechService _tts;
        private readonly Services.AI.IAiProviderEvents _providerEvents;
        private readonly IAiService _ai = null!;

        private string _currentInput;
        private bool _isBusy;
        private bool _isOpen;
        private bool _isListening;
        private bool _isSpeakerOn;
        private bool _isProcessingVoice;
        private float _micAudioLevel;

        // 2026-08-25 senior debug: AudioLevelChanged fires per audio
        // buffer (~30-50x/second while listening). Forwarding every tick
        // to the UI dispatcher flooded the UI thread and added visible
        // jank. Only forward changes of at least 0.03, at most ~15/s.
        private float _lastMicLevelApplied;
        private DateTime _lastMicLevelUiUtc = DateTime.MinValue;
        private string _statusMessage;
        private StreamingChatMessage _activeStream;
        private CancellationTokenSource _cts;

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

        // ── v3 streaming: live token-by-token answer bubble ───────────

        /// <summary>Live streaming bubble (null when not streaming).</summary>
        public StreamingChatMessage ActiveStream
        {
            get => _activeStream;
            private set
            {
                _activeStream = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(HasActiveStream));
            }
        }

        public bool HasActiveStream => _activeStream != null;

        public string ActiveProviderName => _ai?.ProviderName ?? "AI";
        public string ActiveProviderIcon => _ai?.ProviderIcon ?? "🤖";
        public string ActiveProviderSubtitle => _ai != null
            ? $"Powered by {_ai.ProviderName}"
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

        /// <summary>v3: cancels an in-flight (streaming) generation.</summary>
        public ICommand StopGenerationCommand { get; }

        // ══════════════════════════════════════════════════════════════
        //  CONSTRUCTOR
        // ══════════════════════════════════════════════════════════════

                private readonly AppConfig _config = null!;
        private readonly IConnectivityService _connectivity = null!;

public AiChatViewModel(
            AppConfig? config,
            IConnectivityService? connectivity,
            IAiService ai,
            ISpeechToTextService stt = null,
            ITextToSpeechService tts = null,
            Services.AI.IAiProviderEvents? providerEvents = null)
        {
            _config = config!;
            _connectivity = connectivity!;
            _stt = stt;
            _tts = tts;
            _providerEvents = providerEvents ?? new Services.AI.AiProviderEvents();
            _ai = ai!;

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
                    var now = DateTime.UtcNow;
                    if (Math.Abs(level - _lastMicLevelApplied) < 0.03f &&
                        (now - _lastMicLevelUiUtc).TotalMilliseconds < 66)
                        return;
                    _lastMicLevelApplied = level;
                    _lastMicLevelUiUtc = now;
                    RunOnUi(() => MicAudioLevel = level);
                };

                // ── Auto-fallback on Whisper model failure ────────────
                // If Whisper model fails to load (corrupted, missing DLL,
                // permission error), swap BOTH stt and this
                // VM's own _stt reference to the SpeechToTextService
                // fallback so voice keeps working.
                //
                // VOICE-FIX: previous versions only swapped stt;
                // this VM kept using the disposed Whisper through _stt, so
                // IsVoiceAvailable flipped to false and the mic button +
                // voice assistant UI vanished.
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

                            var fallback = new SpeechToTextService();

                            // Re-point this VM AND the global service.
                            _stt = fallback;
                            stt = fallback;

                            // Re-wire listening-state updates from the
                            // new service (old handler died with Whisper).
                            _stt.ListeningStateChanged += (_, listening) =>
                            {
                                RunOnUi(() =>
                                {
                                    IsListening = listening;
                                    if (!listening)
                                        MicAudioLevel = 0f;
                                });
                            };

                            AppLogService.Debug(
                                "[AiChatVM] Swapped Whisper → System.Speech.");

                            StatusMessage =
                                "✓ Voice ready (System.Speech fallback). " +
                                "For 95% accuracy, re-download Whisper " +
                                "model in Settings.";

                            // Show the mic again — availability changed.
                            OnPropertyChanged(nameof(IsVoiceAvailable));
                        }
                        catch (Exception ex)
                        {
                            AppLogService.Debug(
                                $"[AiChatVM] Fallback swap failed: {ExceptionMessages.Deep(ex)}");
                            StatusMessage =
                                "⚠ Voice unavailable — check audio drivers";
                            OnPropertyChanged(nameof(IsVoiceAvailable));
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
            // 2026-08-25: named handler instead of an anonymous lambda —
            // anonymous subscriptions to static events are invisible in
            // audits and impossible to detach.
            _providerEvents.Changed += OnAiProviderChanged;

            // ══════════════════════════════════════════════════════════
            //  TEXT COMMANDS
            // ══════════════════════════════════════════════════════════

            SendCommand = new RelayCommand(
                async _ => await SendAsync(),
                _ => CanSend);

            ToggleChatCommand = new RelayCommand(_ =>
            {
                IsOpen = !IsOpen;

                // v3 JARVIS: greet once when the panel first opens so
                // the user is never staring at an empty box.
                // 2026-08-25: guarded — the greeting builds screen-context
                // text and touches provider metadata; if ANY of that throws
                // the toggle must still open the panel, never crash.
                if (IsOpen && Messages.Count == 0)
                {
                    try { AddGreeting(); }
                    catch (Exception ex)
                    {
                        AppLogService.Warning(
                            "[AiChatVM] Greeting failed: " + ExceptionMessages.Deep(ex));
                    }
                }
            });

            CloseChatCommand = new RelayCommand(_ =>
            {
                IsOpen = false;
                StopAllVoice();

                // v3: closing the panel aborts any in-flight generation
                // so nothing keeps running (and billing) invisibly.
                try { _cts?.Cancel(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }
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

                            // v3 JARVIS: voice = instant. Recognized
                            // speech is sent immediately unless the
                            // user disabled AutoSendVoice in settings
                            // (some prefer to edit the text first).
                            if (_config?.Ai?.AutoSendVoice != false)
                            {
                                StatusMessage = "🎤 Voice captured — sending…";
                                _ = SendAsync();
                            }
                            else
                            {
                                StatusMessage =
                                    "✓ Voice recognized — press Enter to send or edit first";
                            }
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

            StopGenerationCommand = new RelayCommand(_ =>
            {
                try { _cts?.Cancel(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }
                StatusMessage = "⏹ Stopping generation…";
            });

            Messages.CollectionChanged +=
                (_, __) => OnPropertyChanged(nameof(HasMessages));
        }

        // ══════════════════════════════════════════════════════════════
        //  SEND MESSAGE
        // ══════════════════════════════════════════════════════════════

        private async Task SendAsync()
        {
            // R-2: re-entry guard at the METHOD, not the command.
            // RelayCommand takes Action<object>, so "async _ => await
            // SendAsync()" is an async void lambda: Execute() returns at
            // the first await and CanExecute cannot observe in-flight
            // work. SendCommand's CanSend covers only call site #1;
            // SuggestionCommand and the voice path (_ = SendAsync())
            // bypass it entirely. Guarding here covers all three and
            // stops a second turn being appended mid-request.
            if (IsBusy) return;

            if (string.IsNullOrWhiteSpace(CurrentInput)) return;

            _tts?.Stop();

            var currentAi = _ai;

            // ══════════════════════════════════════════════════════════
            //  2026-09-04 — NEVER DEAD: the LOCAL-BRAIN FALLBACK.
            //
            //  The assistant used to be cloud-only: no key, no provider
            //  or no internet meant four dead-end error strings and a
            //  chat that did nothing — the "offline agent is completely
            //  dead" complaint. But this machine ALWAYS has a working,
            //  offline, zero-download brain on board: the ProGlass Agent
            //  (knowledge base + live-data lookups + navigation + memory
            //  + teaching — Agent/Core). Configuration gaps and network
            //  absence are now FALLBACK TRIGGERS, not failures:
            //
            //    no provider / no key / offline
            //      → the SAME question goes through AgentHost.HandleAsync
            //      → answer is shown with an honest "🏠 Local (offline)"
            //        provenance tag instead of a shrug.
            //
            //  "AI Assistant disabled by admin" stays a hard stop: an
            //  explicit off switch is a statement of intent, not a
            //  missing capability.
            // ══════════════════════════════════════════════════════════
            // An explicit OFF switch is a statement of intent, not a
            // missing capability — it stops the turn BEFORE fallback is
            // even considered, regardless of provider state.
            if (currentAi is { IsEnabled: false })
            {
                AddError(
                    "⚠ AI Assistant is disabled.\n\n" +
                    "Go to ⚙ Settings → AI Assistant and toggle it ON.");
                return;
            }

            bool cloudUnavailable =
                currentAi == null ||
                !currentAi.IsConfigured ||
                (_connectivity != null && !_connectivity.IsOnline);

            if (cloudUnavailable)
            {
                string localProbe = CurrentInput.Trim();
                string reason = currentAi == null
                    ? "No cloud AI provider is configured"
                    : !currentAi.IsConfigured
                        ? $"The {currentAi.ProviderName} API key is missing"
                        : "This computer is offline";

                await AnswerWithLocalBrainAsync(localProbe, reason);
                return;
            }

            var userMsg = CurrentInput.Trim();
            Messages.Add(new ChatMessage { Text = userMsg, IsUser = true });
            CurrentInput = string.Empty;
            StatusMessage = string.Empty;
            IsProcessingVoice = false;

            IsBusy = true;

            // v3: one cancellation source per request — the Stop button
            // can abort an in-flight generation at any time.
            try { _cts?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }
            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            try
            {
                // ── Phase 1: context (visible progress, never silent) ──
                StatusMessage = "🧠 Preparing permitted context…";

                var systemPrompt = BuildSystemPrompt();
                string dataContext = string.Empty;

                // This is an egress boundary: with real business data OFF,
                // BuildContextAsync returns before cache or database access.
                // Persisted AI memory and audit events are not appended. The
                // user's current message is still sent to the selected cloud
                // provider as part of ordinary chat.
                try
                {
                    dataContext = await AiContextBuilder.BuildContextAsync(
                        _config?.Ai?.UseRealData == true);
                }
                catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }

                var fullSystemPrompt = systemPrompt;
                if (!string.IsNullOrEmpty(dataContext))
                    fullSystemPrompt += "\n" + dataContext;

                // Detected entity reference is passed to the local memory
                // store after the response; it is not recalled into cloud prompts.
                EntityReference? entityRef = null;

                if (_config?.Ai?.UseRealData == true)
                {
                    entityRef = DetectEntityReference(userMsg);
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
                        catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }
                    }
                }

                // ── Phase 2: generation ────────────────────────────────
                StatusMessage = $"🤔 {currentAi.ProviderName} is thinking…";

                bool handled = false;

                // v3 STREAMING: providers implementing IAiStreamingService
                // (Gemini) deliver the answer token-by-token — the user
                // sees words within ~1-2s instead of a silent 10-30s wait.
                if (_config?.Ai?.StreamingEnabled != false &&
                    currentAi is IAiStreamingService streaming)
                {
                    var sm = new StreamingChatMessage
                    {
                        ProviderName = currentAi.ProviderName,
                        ProviderIcon = currentAi.ProviderIcon
                    };
                    ActiveStream = sm;
                    bool firstTokenSeen = false;

                    try
                    {
                        var res = await streaming.AskStreamingAsync(
                            fullSystemPrompt, userMsg,
                            delta => RunOnUi(() =>
                            {
                                if (!firstTokenSeen)
                                {
                                    firstTokenSeen = true;
                                    StatusMessage = "📝 Responding…";
                                }
                                sm.Text = delta;
                            }),
                            token);

                        handled = true;
                        FinishResponse(res.Success, res.Text,
                            res.ErrorMessage,
                            res.ProviderName ?? currentAi.ProviderName,
                            res.WasCancelled, userMsg, entityRef);
                    }
                    catch (Exception ex)
                    {
                        handled = true;
                        AddError($"⚠ {currentAi.ProviderName} error: {ExceptionMessages.Deep(ex)}");
                    }
                }

                // ── Fallback: one-shot request (Groq/Cohere/stream-off) ──
                if (!handled)
                {
                    var response =
                        await currentAi.AskAsync(fullSystemPrompt, userMsg);

                    FinishResponse(response.Success, response.Text,
                        response.ErrorMessage,
                        response.ProviderName ?? currentAi.ProviderName,
                        false, userMsg, entityRef);
                }
            }
            finally
            {
                ActiveStream = null;
                IsBusy = false;
                StatusMessage = string.Empty;
                try { _cts?.Dispose(); } catch (Exception ex) { AppLogService.Warning($"Suppressed exception: {ExceptionMessages.Deep(ex)}"); }
                _cts = null;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  LOCAL-BRAIN FALLBACK (2026-09-04)
        //
        //  The offline/local answer path. Requirements it must satisfy:
        //   1. Works on ANY PC — no model download, no network, no key.
        //   2. Never lies: the reply is tagged as coming from the local
        //      brain, and the reason cloud was skipped is stated.
        //   3. Never breaks the chat: every failure degrades to the old
        //      honest guidance, not to silence or a crash.
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Answers via the always-on-board ProGlass Agent
        /// (knowledge base + live-data lookups + navigation + memory).
        /// Consumes the input, posts the exchange, and reports provenance.
        /// Falls back to the legacy guidance only if even the local brain
        /// cannot start.
        /// </summary>
        /// <summary>Entry point for the pre-send guards: consumes the input.</summary>
        private Task AnswerWithLocalBrainAsync(string userMsg, string reason)
        {
            Messages.Add(new ChatMessage { Text = userMsg, IsUser = true });
            CurrentInput = string.Empty;
            return AnswerWithLocalBrainCoreAsync(userMsg, reason);
        }

        private async Task AnswerWithLocalBrainCoreAsync(string userMsg, string reason)
        {
            IsBusy = true;
            StatusMessage = "🏠 Local brain is thinking… (offline mode)";
            try
            {
                var agent = ProGlassAutomation.Agent.Core.AgentHost.Ensure();

                if (agent == null)
                {
                    // Even the local brain could not start on this box:
                    // the chat still must answer with guidance, not death.
                    AddError(
                        $"⚠ {reason}, and the local brain failed to start.\n\n" +
                        "Nothing was changed. Everything else in ProGlass " +
                        "is unaffected.");
                    return;
                }

                try
                {
                    await ProGlassAutomation.Agent.Core.AgentHost
                        .SeedKnowledgeAsync()
                        .ConfigureAwait(true);
                }
                catch (Exception ex)
                {
                    AppLogService.Debug($"[AiChat] local knowledge seed skipped: {ex.Message}");
                }

                var response = await agent.HandleAsync(
                    new ProGlassAutomation.Agent.Core.AgentRequest
                    {
                        Text = userMsg,
                        FromVoice = false,
                        Username = ProGlassAutomation.Services.Audit
                            .AuditService.CurrentUsername ?? ""
                    });

                string message = string.IsNullOrWhiteSpace(response?.Message)
                    ? "I could not find a local answer for that."
                    : response!.Message;

                string provenance =
                    $"🏠 LOCAL BRAIN — {reason}. Answered fully offline " +
                    "from the on-board knowledge base and live data. ";

                Messages.Add(new ChatMessage
                {
                    Text = provenance + "\n\n" + message,
                    IsUser = false,
                    ProviderName = "ProGlass Local Brain",
                    ProviderIcon = "🏠"
                });

                RememberExchange(userMsg, message, null);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Ai,
                    "Question answered offline (local brain)",
                    userMsg.Length > 120 ? userMsg[..120] + "…" : userMsg);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AiChat] local-brain fallback failed: {ex.Message}");
                AddError(
                    "⚠ Local answering failed too — nothing was changed. " +
                    "Everything else in ProGlass is unaffected.");
            }
            finally
            {
                IsBusy = false;
                StatusMessage = string.Empty;
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  SHARED RESPONSE FINISH (streaming + one-shot paths)
        // ══════════════════════════════════════════════════════════════

        private void FinishResponse(
            bool success, string text, string errorMessage,
            string providerName, bool wasCancelled,
            string userMsg, EntityReference? entityRef)
        {
            ActiveStream = null;

            if (wasCancelled)
            {
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Messages.Add(new ChatMessage
                    {
                        Text = text.Trim() + "\n\n⏹ Generation stopped.",
                        IsUser = false,
                        ProviderName = providerName,
                        ProviderIcon = _ai?.ProviderIcon ?? "🤖"
                    });
                    RememberExchange(userMsg, text, entityRef);
                }
                else
                {
                    StatusMessage = "⏹ Stopped before any text arrived.";
                }
                return;
            }

            if (success)
            {
                var aiText = text?.Trim() ?? "(empty response)";

                Messages.Add(new ChatMessage
                {
                    Text = aiText,
                    IsUser = false,
                    ProviderName = providerName,
                    ProviderIcon = _ai?.ProviderIcon ?? "🤖"
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

                RememberExchange(userMsg, aiText, entityRef);

                ProGlassAutomation.Services.Audit.AuditService.Log(
                    ProGlassAutomation.Services.Audit.AuditSections.Ai,
                    "Question answered",
                    userMsg.Length > 120 ? userMsg[..120] + "…" : userMsg);
            }
            else
            {
                // 2026-09-04 — cloud errors are no longer dead ends.
                // Report the failure truthfully, then try the local brain:
                // on a factory PC (4 GB, no quota, spotty wifi) a failed
                // cloud call must still produce an answer wherever the
                // knowledge base or live data can give one.
                AddError(
                    $"⚠ Error from {providerName}:\n" +
                    $"{errorMessage ?? "Unknown error"}\n\n" +
                    "Trying the local brain…",
                    providerName);

                ProGlassAutomation.Utilities.AsyncUtility.SafeFireAndForget(
                    () => AnswerWithLocalBrainCoreAsync(
                        userMsg,
                        $"{providerName} failed ({errorMessage ?? "error"})"),
                    "AiChat local fallback after cloud error");
            }
        }

        /// <summary>
        /// v2 learning — remember the exchange so the assistant builds
        /// continuity across sessions. Fire-and-forget with a logged
        /// failure path (a memory write must never break the chat).
        /// </summary>
    }
}
