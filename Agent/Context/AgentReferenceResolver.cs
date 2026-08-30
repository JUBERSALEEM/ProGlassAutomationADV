// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Context/AgentReferenceResolver.cs
// PROJECT: ProGlassAutomation — ProGlass Agent
//
// MASTER PROMPT §14 (context resolver), §29 (INFERRED confidence),
//                §30 (no fabrication), §31 (ambiguity handling)
//
// ─────────────────────────────────────────────────────────────────────
//  THE PROBLEM THIS SOLVES
// ─────────────────────────────────────────────────────────────────────
//  IntentDetector.ExtractQuery pulls the tool argument out of the text
//  after the matched keyword. Ask "how much of PI-1052 is delivered?" and
//  it yields "PI-1052". Ask "how much is still pending?" while looking at
//  that very invoice and it yields "" — the tool then refuses, or worse,
//  runs unscoped.
//
//  §14's whole point is that "it" should resolve to the document on screen
//  WHEN THAT IS UNAMBIGUOUS.
//
// ─────────────────────────────────────────────────────────────────────
//  THE RULES, AND WHY EACH ONE EXISTS
// ─────────────────────────────────────────────────────────────────────
//  1. Only fills an EMPTY argument. If the user named a document, that
//     always wins — context must never override an explicit instruction.
//
//  2. Only for a whitelist of document tools. Filling a blank argument on
//     a search tool would silently narrow a deliberately broad query
//     ("show me the deliveries" must not become "deliveries for PI-1052").
//
//  3. NEVER for Write or Destructive tools. Inferring the target of a
//     change is exactly the mistake that edits the wrong record. A write
//     with no stated subject must be refused, not guessed. This is
//     enforced by the caller passing the tool's risk, and asserted in
//     tests.
//
//  4. Only when a document IDENTIFIER can be recovered. "Sheet Store" is
//     a module, not a document; there is nothing to resolve, so it
//     returns nothing rather than inventing a subject.
//
//  5. The result is always marked INFERRED and the assumption is stated
//     back to the user, so a wrong guess is visible and correctable
//     rather than silent (§29/§31).
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Agent.Interfaces;
using ProGlassAutomation.Services.Logging;
using System;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ProGlassAutomation.Agent.Context
{
    /// <summary>Outcome of trying to resolve an omitted subject.</summary>
    public sealed class ResolvedReference
    {
        /// <summary>True when context supplied a subject.</summary>
        public bool Resolved { get; init; }

        /// <summary>The document identifier, e.g. "PI-2026-014".</summary>
        public string Value { get; init; } = "";

        /// <summary>Plain-language assumption, shown to the user.</summary>
        public string Assumption { get; init; } = "";

        public static ResolvedReference None() => new();

        public static ResolvedReference From(string value, string assumption) =>
            new()
            {
                Resolved = true,
                Value = value ?? "",
                Assumption = assumption ?? ""
            };
    }

    /// <summary>Resolves "it" / "this one" against the open document.</summary>
    public static class AgentReferenceResolver
    {
        /// <summary>
        /// Tools that may take the on-screen document as an implied subject.
        /// Deliberately a SHORT ALLOW-LIST rather than a deny-list: a new
        /// tool gets context only when someone consciously adds it here.
        /// </summary>
        private static readonly string[] ContextualTools =
        {
            "get_document_chain",
            "get_profitability"
        };

        /// <summary>
        /// Pulls a document identifier out of a context label such as
        /// "editing PI-2026-014".
        ///
        /// THE HYPHEN AND THE DIGIT ARE BOTH MANDATORY, AND THAT IS
        /// LOAD-BEARING. An earlier version allowed an optional separator
        /// and any trailing characters, so "Pipeline" matched as PI +
        /// "peline" and "Job Order" matched as "Job". A module publishing
        /// ordinary prose would then have produced a fabricated document
        /// id and the agent would have answered about a record that does
        /// not exist. Requiring "<PREFIX>-<digit>..." makes English words
        /// unmatchable.
        /// </summary>
        private static readonly Regex DocumentId = new(
            @"\b((?:PI|JO|DN|TI|MDN)-\d[A-Za-z0-9\-]*)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Pulls a PI/JO/DN/TI/MDN identifier out of free text.
        /// Returns false when nothing hyphen-plus-digit shaped is present,
        /// so English words like "Pipeline" are never minted as ids.
        /// </summary>
        public static bool TryExtractDocumentId(string? text, out string id)
        {
            id = "";
            if (string.IsNullOrWhiteSpace(text)) return false;

            var match = DocumentId.Match(text);
            if (!match.Success) return false;

            id = match.Groups[1].Value.Trim();
            return id.Length > 0;
        }

        /// <summary>
        /// Tries to supply a missing tool argument from screen context.
        /// Returns None for every case where guessing would be unsafe.
        /// </summary>
        /// <param name="toolName">Tool about to run.</param>
        /// <param name="risk">Its risk tier — Write/Destructive never resolve.</param>
        /// <param name="currentArgument">What the user actually said.</param>
        public static ResolvedReference TryResolve(
            string toolName, ToolRisk risk, string currentArgument)
        {
            try
            {
                // Rule 1 — an explicit subject always wins.
                if (!string.IsNullOrWhiteSpace(currentArgument))
                    return ResolvedReference.None();

                // Rule 3 — never infer the target of a change.
                if (risk is ToolRisk.Write or ToolRisk.Destructive)
                    return ResolvedReference.None();

                // Rule 2 — allow-list only.
                string name = (toolName ?? "").Trim();
                bool allowed = false;

                foreach (var t in ContextualTools)
                {
                    if (string.Equals(t, name, StringComparison.OrdinalIgnoreCase))
                    {
                        allowed = true;
                        break;
                    }
                }

                if (!allowed) return ResolvedReference.None();

                // Rule 4 — need a real document identifier.
                string detail = AgentScreenContext.CurrentDetail;
                if (string.IsNullOrWhiteSpace(detail))
                    return ResolvedReference.None();

                if (!TryExtractDocumentId(detail, out string id))
                    return ResolvedReference.None();

                // Rule 5 — state the assumption.
                return ResolvedReference.From(
                    id,
                    $"you meant {id}, which is open on screen");
            }
            catch (Exception ex)
            {
                // A resolver that throws must resolve nothing, not crash a turn.
                AppLogService.Debug($"[AgentReferenceResolver] {ex.Message}");
                return ResolvedReference.None();
            }
        }
    }
}
