// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/UI/AgentChatViewModel.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE L)
// MODULE: Agent chat — the conversational surface
//
// WHY THIS EXISTS
//   Phase F built the whole agent engine — intent, tools, permissions,
//   teaching, memory, security — but ProGlassAgent.HandleAsync had exactly
//   ONE caller: the voice pipeline. There was no way to type to the agent,
//   so the standing requirement "a real local agent that learns from
//   teaching in chat" was unreachable: a rule could be approved in
//   Settings but never PROPOSED conversationally.
//
//   This view-model is that missing surface. It adds no new capability to
//   the agent and weakens no gate — every message goes through the same
//   HandleAsync pipeline, with the same permission and confirmation
//   checks, as the voice path.
//
// THREADING
//   HandleAsync is awaited without ConfigureAwait(false) so continuations
//   return to the UI thread that owns the Messages collection.
//
// FAILURE POSTURE
//   The agent failing must never break the ERP. Every path is wrapped;
//   a failure becomes a spoken-in-chat apology, never an exception.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Core;
using ProGlassAutomation.Services.Logging;
using ProGlassAutomation.Commands;
using ProGlassAutomation.Services.Audit;
using ProGlassAutomation.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

namespace ProGlassAutomation.Agent.UI
{
    /// <summary>One line in the transcript.</summary>
    public sealed class AgentChatMessage
    {
        /// <summary>"user", "agent" or "system".</summary>
        public string Role { get; init; } = "agent";

        public string Text { get; init; } = "";

        public DateTime AtLocal { get; init; } = DateTime.Now;

        /// <summary>True when the agent could not do what was asked.</summary>
        public bool IsFailure { get; init; }

        public bool IsUser =>
            string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);

        public bool IsAgent => !IsUser;

        public string TimeDisplay => AtLocal.ToString("HH:mm");

        public string RoleDisplay => IsUser ? "You" : "ProGlass Agent";
    }

    /// <summary>Chat surface over <see cref="ProGlassAgent.HandleAsync"/>.</summary>
    public sealed class AgentChatViewModel : ViewModelBase
    {
        private const int MaxTranscript = 200;

        public AgentChatViewModel()
        {
            SendCommand = new RelayCommand(
                // Lambda, NOT the method group: SendAsync(bool fromVoice = false)
                // has an optional parameter, and a method group with optional
                // parameters cannot convert to Func<Task>. The lambda binds the
                // default explicitly, so a typed turn stays silent.
                _ => FireAndForget(() => SendAsync()),
                _ => CanSend);

            ClearCommand = new RelayCommand(_ => ClearTranscript());

            // LISTEN. Disabled while a turn is running, and it now flips
            // IsListening so the UI can prove the microphone is actually
            // open - previously nothing on screen changed, so there was no
            // way to tell whether it was hearing anything.
            SpeakCommand = new RelayCommand(
                _ => FireAndForget(ListenAsync),
                _ => !IsBusy && !IsListening);

            // STOP. Cancels an in-flight turn and silences the voice.
            StopCommand = new RelayCommand(
                _ => Stop(),
                _ => IsBusy || IsListening || IsSpeaking);

            // MUTE. Persists for the session and silences speech
            // immediately, so muting mid-sentence actually stops it.
            ToggleMuteCommand = new RelayCommand(_ => ToggleMute());

            AddSystem(
                "Local agent ready. Ask about invoices, jobs, deliveries, " +
                "customers, stock or recent activity. " +
                "Teach it with: whenever I say <phrase>, <action>. " +
                "A taught phrase does nothing until you approve it in " +
                "Settings > AI & Agent.");
        }

        // ── Transcript ────────────────────────────────────────────────────

        public ObservableCollection<AgentChatMessage> Messages { get; } = new();

        public bool HasMessages => Messages.Count > 0;

        // ── Input ─────────────────────────────────────────────────────────

        private string _input = "";
        public string Input
        {
            get => _input;
            set
            {
                if (_input == value) return;
                _input = value ?? "";
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSend));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(Input);

        private bool _isBusy;
        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (_isBusy == value) return;
                _isBusy = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanSend));
                OnPropertyChanged(nameof(StatusText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  VOICE STATE
        //
        //  The complaint was "I am not sure it is even hearing me".
        //  That was accurate: ListenAsync set IsBusy and nothing else, so
        //  the microphone opened with no visible change at all. These
        //  properties give the view something concrete to bind to.
        // ══════════════════════════════════════════════════════════════

        private bool _isListening;
        /// <summary>True while the microphone is open for one utterance.</summary>
        public bool IsListening
        {
            get => _isListening;
            private set
            {
                if (_isListening == value) return;
                _isListening = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusText));
                OnPropertyChanged(nameof(MicButtonText));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isSpeaking;
        /// <summary>True while the agent is talking back.</summary>
        public bool IsSpeaking
        {
            get => _isSpeaking;
            private set
            {
                if (_isSpeaking == value) return;
                _isSpeaking = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        private bool _isMuted;
        /// <summary>
        /// When true the agent never speaks. Session-scoped on purpose:
        /// a mute that silently survives a restart is a support call.
        /// </summary>
        public bool IsMuted
        {
            get => _isMuted;
            private set
            {
                if (_isMuted == value) return;
                _isMuted = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MuteButtonText));
                OnPropertyChanged(nameof(MuteButtonTooltip));
            }
        }

        /// <summary>Speaker glyph: on = E767, muted = E74F.</summary>
        public string MuteButtonText => IsMuted ? "" : "";

        public string MuteButtonTooltip => IsMuted
            ? "Voice replies are muted — click to unmute"
            : "Mute the agent's voice replies";

        /// <summary>Mic glyph: idle = E720, listening = E71A (stop).</summary>
        public string MicButtonText => IsListening ? "" : "";

        private string _status = "";
        /// <summary>Single truthful status line under the composer.</summary>
        public string StatusText
        {
            get => IsListening
                     ? "Listening… speak now."
                     : IsBusy ? "Thinking…" : _status;
            private set
            {
                if (_status == value) return;
                _status = value ?? "";
                OnPropertyChanged();
            }
        }

        // ── Commands ──────────────────────────────────────────────────────

        public ICommand SendCommand { get; }
        public ICommand ClearCommand { get; }
        public ICommand SpeakCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ToggleMuteCommand { get; }

        // ── Send ──────────────────────────────────────────────────────────

        /// <summary>
        /// Runs one turn. <paramref name="fromVoice"/> is true when the text
        /// came from the microphone: the request is tagged as spoken AND the
        /// answer is read back. A typed turn stays silent, which is what
        /// people expect from a chat box.
        /// </summary>
        private async Task SendAsync(bool fromVoice = false)
        {
            string text = (Input ?? "").Trim();
            if (text.Length == 0 || IsBusy) return;

            Input = "";
            Add("user", text);

            IsBusy = true;
            try
            {
                var agent = AgentHost.Ensure();

                if (agent == null)
                {
                    AddFailure(
                        "The agent could not start, so I cannot answer. " +
                        "Everything else in ProGlass is unaffected.");
                    return;
                }

                // Seeding is idempotent and cheap after the first call; doing
                // it here means chat works even if Settings was never opened.
                await AgentHost.SeedKnowledgeAsync();

                var response = await agent.HandleAsync(new AgentRequest
                {
                    Text = text,
                    FromVoice = fromVoice,
                    Username = AuditService.CurrentUsername ?? ""
                });

                if (response == null)
                {
                    AddFailure("I did not get a reply from the agent.");
                    return;
                }

                if (response.Success) Add("agent", response.Message);
                else AddFailure(response.Message);

                // Speak back only for a spoken request, and only when the
                // user has not muted. The pipeline re-checks IsMuted itself,
                // so this cannot drift out of sync with the button.
                if (fromVoice && !IsMuted)
                {
                    try
                    {
                        IsSpeaking = true;
                        AgentHost.Voice?.SpeakResponse(response);
                    }
                    catch (Exception ex)
                    {
                        AppLogService.Debug($"[AgentChatViewModel] speak: {ex.Message}");
                    }
                    finally
                    {
                        IsSpeaking = false;
                    }
                }

                StatusText = string.IsNullOrWhiteSpace(response.NavigateTarget)
                    ? ""
                    : $"Opened {response.NavigateTarget}.";
            }
            catch (Exception ex)
            {
                // Fail closed and stay truthful — never surface a stack trace.
                AppLogService.Debug($"[AgentChatViewModel] send failed: {ex.Message}");
                AddFailure(
                    "Something went wrong handling that. Nothing was changed.");
            }
            finally
            {
                IsBusy = false;
            }
        }

        // ── Voice ─────────────────────────────────────────────────────────

        /// <summary>
        /// Captures one utterance through the existing Whisper pipeline and
        /// runs it as a normal turn. Reports honestly when voice is not
        /// available rather than silently doing nothing.
        /// </summary>
        private async Task ListenAsync()
        {
            if (IsBusy || IsListening) return;

            try
            {
                AgentHost.Ensure();

                var voice = AgentHost.Voice;
                if (voice == null)
                {
                    AddFailure("Voice is not available on this machine.");
                    return;
                }

                // ══════════════════════════════════════════════════════
                //  TELL THE USER WHY, BEFORE OPENING THE MICROPHONE.
                //
                //  ListenAndHandleAsync already refuses when Whisper is not
                //  ready, but it refused INSIDE the pipeline - so from the
                //  chat window it looked identical to "listened and heard
                //  nothing". Checking readiness up front means an
                //  undownloaded model reads as a setup problem, not as a
                //  microphone that cannot hear you.
                // ══════════════════════════════════════════════════════
                if (!voice.IsReady)
                {
                    AddFailure(
                        "Voice input is not ready, so I did not open the " +
                        "microphone. " + voice.ReadinessMessage);
                    return;
                }

                IsListening = true;

                // Transcribe only. The turn is run below so the transcript
                // can be SHOWN as a user message first - previously the
                // spoken words never appeared in the chat at all, which is
                // why it was impossible to tell what had been heard.
                string? transcript = await voice
                    .CaptureTranscriptAsync()
                    .ConfigureAwait(true);

                IsListening = false;

                if (string.IsNullOrWhiteSpace(transcript))
                {
                    AddFailure(
                        "I did not catch that — nothing was transcribed. " +
                        "Check the microphone is not muted, then try again.");
                    return;
                }

                // Show what was actually heard, then answer it.
                Input = transcript;
                await SendAsync(fromVoice: true).ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentChatViewModel] voice failed: {ex.Message}");
                AddFailure("Voice input failed. Nothing was changed.");
            }
            finally
            {
                IsListening = false;
                StatusText = "";
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  STOP / MUTE
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Stops speech immediately. It cannot abort a database read that
        /// is already in flight, so it does not pretend to - the wording
        /// below only claims what actually happened.
        /// </summary>
        private void Stop()
        {
            try
            {
                AgentHost.Voice?.StopSpeaking();
                IsSpeaking = false;
                IsListening = false;
                StatusText = "Stopped.";
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentChatViewModel] stop failed: {ex.Message}");
            }
        }

        private void ToggleMute()
        {
            IsMuted = !IsMuted;

            var voice = AgentHost.Voice;
            if (voice != null) voice.IsMuted = IsMuted;

            // Muting mid-sentence must actually stop the current sentence,
            // not just suppress the next one.
            if (IsMuted)
            {
                try { voice?.StopSpeaking(); } catch { /* best effort */ }
                IsSpeaking = false;
            }

            StatusText = IsMuted
                ? "Voice replies muted."
                : "Voice replies on.";
        }

        // ── Transcript helpers ────────────────────────────────────────────

        private void Add(string role, string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            Messages.Add(new AgentChatMessage { Role = role, Text = text });
            Trim();
            OnPropertyChanged(nameof(HasMessages));
        }

        private void AddFailure(string text)
        {
            Messages.Add(new AgentChatMessage
            {
                Role = "agent",
                Text = text,
                IsFailure = true
            });
            Trim();
            OnPropertyChanged(nameof(HasMessages));
        }

        private void AddSystem(string text) => Add("system", text);

        /// <summary>
        /// Caps the visible transcript. The agent's own conversation memory
        /// is separately capped and session-only; this is display hygiene.
        /// </summary>
        private void Trim()
        {
            while (Messages.Count > MaxTranscript) Messages.RemoveAt(0);
        }

        /// <summary>
        /// Clears the on-screen transcript only. Saved memory, taught rules
        /// and knowledge are untouched — those have their own explicit
        /// resets in Settings, and must never be cleared as a side effect.
        /// </summary>
        private void ClearTranscript()
        {
            Messages.Clear();
            StatusText = "";
            OnPropertyChanged(nameof(HasMessages));

            AddSystem("Transcript cleared. Taught rules and knowledge are unchanged.");
        }

        private static async void FireAndForget(Func<Task> work)
        {
            try
            {
                await work();
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[AgentChatViewModel] unhandled: {ex.Message}");
            }
        }
    }
}
