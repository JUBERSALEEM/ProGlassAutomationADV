// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Core/ProGlassAgent.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// The orchestrator. One public method, one strict pipeline:
//
//   sanitize → detect intent → plan → execute → truthful response
//
// EXECUTION GATES for a tool step, in this order and no other:
//   1. Is the tool registered?          (unknown → refuse)
//   2. Permission check                 (identity + role)
//   3. Confirmation                     (Write/Destructive always)
//   4. Execute
//
// FAIL-CLOSED EVERYWHERE: every catch produces a refusal, never a
// best-effort action. An agent that "tries something" after an internal
// error is exactly the failure mode this design exists to prevent.
//
// TRUTHFULNESS: the agent never claims success it did not achieve. Tool
// failures are reported verbatim, denials explain why, and unknown
// requests say so rather than improvising.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Intent;
using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Agent.Tools.Registry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.Core
{
    /// <summary>The ProGlass Agent. Deterministic, gated and auditable.</summary>
    public sealed class ProGlassAgent
    {
        private readonly AgentContext _context;

        /// <summary>Current lifecycle state (for the UI).</summary>
        public AgentState State { get; private set; } = AgentState.Idle;

        /// <summary>Raised whenever <see cref="State"/> changes.</summary>
        public event EventHandler<AgentState>? StateChanged;

        public ProGlassAgent(AgentContext context)
        {
            _context = context ?? new AgentContext();
        }

        private void SetState(AgentState state)
        {
            if (State == state) return;
            State = state;

            try { StateChanged?.Invoke(this, state); }
            catch { /* a UI handler must never break the agent */ }
        }

        // ══════════════════════════════════════════════════════════════
        //  ENTRY POINT
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// Handles one request end to end. Never throws: every failure
        /// path returns an AgentResponse explaining what did not happen.
        /// </summary>
        public async Task<AgentResponse> HandleAsync(AgentRequest request)
        {
            if (request == null)
                return AgentResponse.Fail("I did not receive a request.");

            string user = (request.Username ?? "").Trim();

            try
            {
                SetState(AgentState.Thinking);

                // ── 1. SANITIZE ───────────────────────────────────────
                // Voice transcripts and typed text are both untrusted.
                string text = _context.Security?.SanitizeUntrusted(request.Text)
                              ?? (request.Text ?? "").Trim();

                if (string.IsNullOrWhiteSpace(text))
                {
                    SetState(AgentState.Idle);
                    return AgentResponse.Fail("I did not catch anything to act on.");
                }

                _context.Audit?.Request(user, text, request.FromVoice);
                _context.Memory?.AddTurn("user", text);

                // ── 2. DETECT INTENT ──────────────────────────────────
                IReadOnlyList<TeachingRuleView> activeRules =
                    Array.Empty<TeachingRuleView>();

                if (_context.Teaching != null)
                {
                    try
                    {
                        activeRules = await _context.Teaching.ActiveRulesAsync()
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        // Teaching being unavailable must not block the agent;
                        // it just means no user rules apply this turn.
                        Debug.WriteLine($"[Agent] teaching rules unavailable: {ex.Message}");
                    }
                }

                string intent = IntentDetector.Detect(text, activeRules);
                _context.Audit?.Intent(user, intent);

                // ── 3. PLAN ───────────────────────────────────────────
                var steps = _context.Planner?.Plan(intent, text)
                            ?? new List<AgentPlanStep>
                            {
                                new() { Kind = "answer", Argument = text }
                            };

                if (steps.Count == 0)
                {
                    SetState(AgentState.Idle);
                    return AgentResponse.Fail("I could not work out what to do.");
                }

                // ── 4. EXECUTE (first step; the DSL is single-step) ───
                var step = steps[0];

                AgentResponse response = step.Kind switch
                {
                    "help" => Help(),
                    "teach" => await TeachAsync(user, step).ConfigureAwait(false),
                    "approve" => await ApproveAsync(user, step).ConfigureAwait(false),
                    "navigate" => await NavigateAsync(user, step).ConfigureAwait(false),
                    "tool" => await RunToolAsync(user, step).ConfigureAwait(false),
                    _ => await AnswerAsync(step.Argument).ConfigureAwait(false)
                };

                _context.Memory?.AddTurn("agent", response.Message);
                SetState(AgentState.Idle);
                return response;
            }
            catch (Exception ex)
            {
                // Nothing escapes. Fail closed, tell the truth, audit it.
                Debug.WriteLine($"[Agent] unhandled: {ex.Message}");
                _context.Audit?.AgentError(user, ex.Message);
                SetState(AgentState.Error);

                return AgentResponse.Fail(
                    "Something went wrong inside me, so I stopped without doing " +
                    "anything.");
            }
        }

        // ══════════════════════════════════════════════════════════════
        //  FLOWS
        // ══════════════════════════════════════════════════════════════

        private AgentResponse Help()
        {
            var sb = new StringBuilder();
            sb.AppendLine("Here is what I can do:");
            sb.AppendLine();
            sb.AppendLine("Look things up (read-only):");
            sb.AppendLine(AgentToolRegistry.Describe(_context.Tools));
            sb.AppendLine();
            sb.AppendLine("Open screens: say \"open sheet store\", \"go to job orders\".");
            sb.AppendLine();
            sb.AppendLine("Teach me a phrase:");
            sb.AppendLine("  \"whenever I say stock check, show me the inventory\"");
            sb.AppendLine("  then \"approve rule 1\" — taught phrases do nothing " +
                          "until you approve them.");
            sb.AppendLine();
            sb.AppendLine("I cannot create, edit or delete any record.");

            return AgentResponse.Ok(
                sb.ToString().TrimEnd(),
                "I can look up documents and stock, open screens, and learn " +
                "phrases you approve. I cannot change any record.");
        }

        /// <summary>Stores a proposed rule. It stays inert until approved.</summary>
        private async Task<AgentResponse> TeachAsync(string user, AgentPlanStep step)
        {
            if (_context.Teaching == null)
                return AgentResponse.Fail("I cannot save taught phrases right now.");

            string trigger = step.Target ?? "";
            string action = step.Argument ?? "";

            if (trigger.Length == 0 || action.Length == 0)
            {
                return AgentResponse.Fail(
                    "I could not tell what to learn. Try: \"whenever I say stock " +
                    "check, show me the inventory\".");
            }

            int id = await _context.Teaching.ProposeAsync(trigger, action)
                .ConfigureAwait(false);

            if (id <= 0)
            {
                return AgentResponse.Fail(
                    "I could not turn that into something I am able to do, so I " +
                    "did not save it. I can only be taught to open a screen or " +
                    "run one of my lookups.");
            }

            string message =
                $"Saved as rule {id}: when you say \"{trigger}\" I will {Describe(action)}.\n" +
                $"It will not do anything until you say \"approve rule {id}\".";

            _context.Audit?.Teaching(user, "Rule proposed via agent",
                $"#{id} '{trigger}' → {action}");

            return AgentResponse.Ok(message,
                $"Saved as rule {id}. Say approve rule {id} to switch it on.");
        }

        private async Task<AgentResponse> ApproveAsync(string user, AgentPlanStep step)
        {
            if (_context.Teaching == null)
                return AgentResponse.Fail("I cannot change taught phrases right now.");

            if (!int.TryParse(step.Target, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int id) || id <= 0)
                return AgentResponse.Fail("Tell me which rule number to approve.");

            bool ok = await _context.Teaching.ApproveAsync(id, user)
                .ConfigureAwait(false);

            return ok
                ? AgentResponse.Ok($"Rule {id} is approved and active now.")
                : AgentResponse.Fail($"I could not find rule {id}.");
        }

        /// <summary>Navigation runs through the tool gate like everything else.</summary>
        private Task<AgentResponse> NavigateAsync(string user, AgentPlanStep step) =>
            RunToolAsync(user, new AgentPlanStep
            {
                Kind = "tool",
                Target = "navigate",
                Argument = step.Target
            });

        /// <summary>
        /// The gated tool path: registered → permitted → confirmed → run.
        /// </summary>
        private async Task<AgentResponse> RunToolAsync(string user, AgentPlanStep step)
        {
            string name = (step.Target ?? "").Trim();

            // 1 ── Registered?
            if (!_context.Tools.TryGetValue(name, out var tool) || tool == null)
            {
                _context.Audit?.Tool(user, name, false, "unknown tool");
                return AgentResponse.Fail(
                    $"I do not have a '{name}' capability, so I did nothing.");
            }

            var definition = tool.Definition;

            // ══════════════════════════════════════════════════════════
            //  1b ── CONTEXT RESOLUTION (§14)
            //
            //  "How much is still pending?" carries no document, so
            //  ExtractQuery returns "" and the tool would refuse. If a
            //  document is open on screen, use it as the implied subject.
            //
            //  Done BEFORE the confirmation prompt on purpose: a prompt
            //  must state the REAL target, never a blank that the tool
            //  fills in afterwards.
            //
            //  The resolver refuses to guess for Write/Destructive tools,
            //  refuses when the user named a subject, and refuses unless a
            //  real document id can be recovered. See its header.
            // ══════════════════════════════════════════════════════════
            string argument = step.Argument ?? "";
            string inferenceNote = "";

            var resolved = ProGlassAutomation.Agent.Context.AgentReferenceResolver.TryResolve(
                name, definition.Risk, argument);

            if (resolved.Resolved)
            {
                argument = resolved.Value;

                // Stated back to the user so a wrong guess is visible and
                // correctable, never silent (§29/§31).
                inferenceNote = $"I assumed {resolved.Assumption}.";

                // NOTE: this is recorded as its own line rather than folded
                // into the execution entry, because an inferred subject is a
                // decision the agent made on the user's behalf and must be
                // auditable EVEN IF the permission check then denies the
                // tool. Logged via Teaching (a neutral "what the agent did"
                // channel) rather than Tool, because Tool(allowed: true)
                // would read as a successful tool RUN in the activity log
                // before the gate has even been consulted.
                _context.Audit?.Teaching(user, "Agent inferred subject",
                    $"{name}: assumed '{argument}' from the document on screen " +
                    "(INFERRED, not stated by the user)");
            }

            // 2 ── Permission (identity + role)
            if (_context.Permissions == null)
            {
                _context.Audit?.Tool(user, name, false, "no permission service");
                return AgentResponse.Fail(
                    "I cannot verify your permissions right now, so I stopped.");
            }

            var allowed = _context.Permissions.Check(definition, user);
            if (!allowed.Allowed)
            {
                _context.Audit?.Tool(user, name, false, allowed.Reason);
                return AgentResponse.Fail(allowed.Reason);
            }

            // 3 ── Confirmation (always for Write/Destructive)
            if (definition.Risk is ToolRisk.Write or ToolRisk.Destructive ||
                definition.RequiresConfirmation)
            {
                SetState(AgentState.AwaitingConfirmation);

                var confirmed = await _context.Permissions
                    .ConfirmAsync(definition, argument)
                    .ConfigureAwait(false);

                if (!confirmed.Allowed)
                {
                    _context.Audit?.Tool(user, name, false, confirmed.Reason);
                    return AgentResponse.Fail(confirmed.Reason);
                }
            }

            // 4 ── Execute
            SetState(AgentState.Acting);

            try
            {
                var result = await tool.ExecuteAsync(argument)
                    .ConfigureAwait(false);

                // Provenance in the audit line: an answer built on an
                // inferred subject must be identifiable months later, when
                // nobody remembers what was on screen at the time.
                string provenance = inferenceNote.Length > 0
                    ? "INFERRED subject"
                    : "explicit subject";

                _context.Audit?.Tool(user, name, result.Success,
                    $"arg='{argument}' ({provenance}) → " +
                    $"{(result.Success ? "ok" : "failed")}");

                if (!result.Success) return AgentResponse.Fail(result.Message);

                // Prepend the assumption so the user can correct a wrong
                // inference immediately, rather than acting on the answer
                // and discovering later it was about another document.
                string message = inferenceNote.Length > 0
                    ? $"{inferenceNote}\n\n{result.Message}"
                    : result.Message;

                return AgentResponse.Ok(message, "", result.NavigateTarget);
            }
            catch (Exception ex)
            {
                // A throwing tool is a bug; report it, never pretend success.
                Debug.WriteLine($"[Agent] tool '{name}' threw: {ex.Message}");
                _context.Audit?.Tool(user, name, false, $"threw: {ex.Message}");

                return AgentResponse.Fail(
                    $"The '{name}' step failed, so I stopped without finishing.");
            }
        }

        private async Task<AgentResponse> AnswerAsync(string question)
        {
            if (_context.Reasoning == null)
            {
                return AgentResponse.Fail(
                    "I do not have anything to answer that with. Ask me about " +
                    "stock, a document, a customer, or recent activity.");
            }

            string answer = await _context.Reasoning.ReasonAsync(question)
                .ConfigureAwait(false);

            return string.IsNullOrWhiteSpace(answer)
                ? AgentResponse.Fail("I could not find an answer to that.")
                : AgentResponse.Ok(answer);
        }

        /// <summary>Renders an action DSL string as plain English.</summary>
        private static string Describe(string action)
        {
            if (action.StartsWith("navigate:", StringComparison.OrdinalIgnoreCase))
                return $"open {action["navigate:".Length..]}";

            if (action.StartsWith("tool:", StringComparison.OrdinalIgnoreCase))
            {
                string body = action["tool:".Length..];
                int split = body.IndexOf(':');
                return split > 0
                    ? $"run {body[..split]} for \"{body[(split + 1)..]}\""
                    : $"run {body}";
            }

            return action;
        }
    }
}
