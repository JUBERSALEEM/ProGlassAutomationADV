// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Context/AnswerProvenance.cs
// PROJECT: ProGlassAutomation — ProGlass Agent
//
// MASTER PROMPT §28 (DATA PROVENANCE) + §29 (ANSWER CONFIDENCE)
//                + §30 (NO FABRICATION)
//
// WHAT PROBLEM THIS SOLVES
//   Before this, every agent answer looked equally authoritative. A figure
//   read straight out of the database and a figure the agent worked out by
//   subtraction were presented in exactly the same voice, and a guess about
//   WHICH document the user meant was indistinguishable from a fact.
//
//   That is the failure mode §30 is really about. Fabrication does not
//   usually look like an invented number; it looks like a real number
//   presented with more certainty than it deserves.
//
// THE FOUR LEVELS, AS SPECIFIED
//   Known       — read directly from ERP data.
//   Calculated  — derived from ERP data by a stated formula.
//   Inferred    — depends on an assumption (usually "you meant the document
//                 currently open"). The assumption is always named.
//   Unknown     — the ERP does not contain enough to answer.
//
// DESIGN NOTE
//   Provenance is recorded INTERNALLY on every answer. §28 says not to dump
//   implementation detail on normal users, so the plain-language qualifier
//   is deliberately short, and empty for Known — a fact needs no hedge.
//   The detailed source/formula is available for audit, not for chat.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Agent.Context
{
    /// <summary>How much the agent actually knows about an answer.</summary>
    public enum AnswerConfidence
    {
        /// <summary>The ERP cannot answer this. Say so plainly.</summary>
        Unknown = 0,

        /// <summary>Rests on a named assumption.</summary>
        Inferred = 1,

        /// <summary>Derived from ERP data by a stated formula.</summary>
        Calculated = 2,

        /// <summary>Read directly from ERP data.</summary>
        Known = 3
    }

    /// <summary>
    /// Where one answer came from. Immutable: provenance that can be edited
    /// after the fact is not provenance.
    /// </summary>
    public sealed class AnswerProvenance
    {
        public AnswerConfidence Confidence { get; init; } = AnswerConfidence.Unknown;

        /// <summary>Service or repository consulted, e.g. "JobOrderRepository".</summary>
        public string Source { get; init; } = "";

        /// <summary>Documents consulted, e.g. "PI-1052", "JO-2201".</summary>
        public IReadOnlyList<string> Documents { get; init; } = Array.Empty<string>();

        /// <summary>Formula, when Calculated. Empty otherwise.</summary>
        public string Calculation { get; init; } = "";

        /// <summary>The assumption, when Inferred. Empty otherwise.</summary>
        public string Assumption { get; init; } = "";

        /// <summary>When the underlying data was read.</summary>
        public DateTime AtUtc { get; init; } = DateTime.UtcNow;

        public static AnswerProvenance Known(
            string source, params string[] documents) =>
            new()
            {
                Confidence = AnswerConfidence.Known,
                Source = source ?? "",
                Documents = documents ?? Array.Empty<string>()
            };

        public static AnswerProvenance Calculated(
            string source, string calculation, params string[] documents) =>
            new()
            {
                Confidence = AnswerConfidence.Calculated,
                Source = source ?? "",
                Calculation = calculation ?? "",
                Documents = documents ?? Array.Empty<string>()
            };

        public static AnswerProvenance Inferred(
            string source, string assumption, params string[] documents) =>
            new()
            {
                Confidence = AnswerConfidence.Inferred,
                Source = source ?? "",
                Assumption = assumption ?? "",
                Documents = documents ?? Array.Empty<string>()
            };

        public static AnswerProvenance Unknown(string reason) =>
            new()
            {
                Confidence = AnswerConfidence.Unknown,
                Source = reason ?? ""
            };

        /// <summary>
        /// Short plain-language hedge for the user. Empty for Known: adding
        /// "this is a fact" to a fact just makes answers noisier.
        /// </summary>
        public string UserQualifier() => Confidence switch
        {
            AnswerConfidence.Known => "",

            AnswerConfidence.Calculated =>
                string.IsNullOrWhiteSpace(Calculation)
                    ? "This figure is calculated from ERP data."
                    : $"Calculated as {Calculation}.",

            AnswerConfidence.Inferred =>
                string.IsNullOrWhiteSpace(Assumption)
                    ? "This assumes which record you meant — tell me if it is wrong."
                    : $"I assumed {Assumption}. Tell me if that is wrong.",

            _ => "The ERP does not hold enough information to answer that."
        };

        /// <summary>Audit-side detail. Never shown in normal chat.</summary>
        public string ToAuditString()
        {
            var docs = Documents.Count > 0
                ? string.Join(", ", Documents)
                : "none";

            var extra =
                !string.IsNullOrWhiteSpace(Calculation) ? $" calc={Calculation}" :
                !string.IsNullOrWhiteSpace(Assumption) ? $" assumed={Assumption}" : "";

            return $"{Confidence} src={Source} docs=[{docs}]{extra} at={AtUtc:O}";
        }
    }
}
