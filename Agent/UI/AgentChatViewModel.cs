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
                _ => FireAndForget(SendAsync),
                _ => CanSend);

            ClearCommand = new RelayCommand(_ => ClearTranscript());

            SpeakCommand = new RelayCommand(
                _ => FireAndForget(ListenAsync),
                _ => !IsBusy);

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

        private string _status = "";
        /// <summary>Single truthful status line under the composer.</summary>
        public string StatusText
        {
            get => IsBusy ? "Thinking…" : _status;
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

        // ── Send ──────────────────────────────────────────────────────────

        private async Task SendAsync()
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
                    FromVoice = false,
                    Username = AuditService.CurrentUsername ?? ""
                });

                if (response == null)
                {
                    AddFailure("I did not get a reply from the agent.");
                    return;
                }

                if (response.Success) Add("agent", response.Message);
                else AddFailure(response.Message);

                StatusText = string.IsNullOrWhiteSpace(response.NavigateTarget)
                    ? ""
                    : $"Opened {response.NavigateTarget}.";
            }
            catch (Exception ex)
            {
                // Fail closed and stay truthful — never surface a stack trace.
                Debug.WriteLine($"[AgentChatViewModel] send failed: {ex.Message}");
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
            if (IsBusy) return;

            IsBusy = true;
            try
            {
                AgentHost.Ensure();

                var voice = AgentHost.Voice;
                if (voice == null)
                {
                    AddFailure("Voice is not available on this machine.");
                    return;
                }

                StatusText = "Listening…";

                var response = await voice.ListenAndHandleAsync(
                    AuditService.CurrentUsername ?? "");

                if (response == null)
                {
                    AddFailure("I did not catch that.");
                    return;
                }

                if (response.Success) Add("agent", response.Message);
                else AddFailure(response.Message);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AgentChatViewModel] voice failed: {ex.Message}");
                AddFailure("Voice input failed. Nothing was changed.");
            }
            finally
            {
                StatusText = "";
                IsBusy = false;
            }
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
                Debug.WriteLine($"[AgentChatViewModel] unhandled: {ex.Message}");
            }
        }
    }
}
