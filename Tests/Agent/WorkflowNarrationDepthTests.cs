// -----------------------------------------------------------------------------
// WorkflowNarrationDepthTests
// -----------------------------------------------------------------------------
// Guards against regression of the Aug-2026 narration re-sync pass. The user
// complained the Demo Hub was producing terse, robotic voice-overs that did
// NOT explain the software like a human trainer would. Every remaining
// workflow (Dashboard, Job Profitability, User Center, Company Center,
// Delivery Note, Job Order, Tax Invoice, Sheet Store) was re-written to
// speak in full-sentence, per-field, human explanations.
//
// This test is source-file only — it does NOT boot the app, touch a DB,
// or run any workflow. It reads each workflow .cs file, extracts the
// narration argument of every PresentAsync(...) call (which is the third
// string literal / interpolated string), and asserts a minimum character
// length. A future edit that shortens a beat below the threshold will
// break this test and force a code review.
//
// Deliberately not enforced on ProformaInvoiceWorkflow (already had
// deep narration from the 10/10 push) or StatementWorkflow (already deep),
// or the shared DemoWalk helper. Also not enforced on Ffmpeg/Composer or
// tour workflows.
//
// Hermetic: pure file I/O + regex; no test doubles, no fixtures.
// -----------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace ProGlassAutomation.Tests.Agent
{
    public class WorkflowNarrationDepthTests
    {
        // Files whose narration was rewritten this pass. If a future edit
        // deletes narration, this list still forces the file to be present.
        private static readonly string[] EnrichedWorkflows =
        {
            "DashboardWorkflow.cs",
            "JobProfitabilityWorkflow.cs",
            "UserCenterWorkflow.cs",
            "CompanyCenterWorkflow.cs",
            "DeliveryNoteWorkflow.cs",
            "JobOrderWorkflow.cs",
            "TaxInvoiceWorkflow.cs",
            "SheetStoreWorkflow.cs",
        };

        // Minimum narration character count per beat. 200 characters is
        // roughly 2 full sentences — anything shorter is the terse
        // technical style the user rejected. Chosen empirically from
        // the shortest surviving beat after the rewrite (~350 chars).
        private const int MinCharsPerBeat = 200;

        [Fact]
        public void Every_present_beat_has_human_length_narration()
        {
            var root = LocateWorkflowsRoot();
            var failures = new List<string>();

            foreach (var name in EnrichedWorkflows)
            {
                var path = Path.Combine(root, name);
                Assert.True(File.Exists(path), $"Workflow file missing: {path}");

                var source = File.ReadAllText(path);
                var narrations = ExtractNarrations(source);
                Assert.True(narrations.Count > 0,
                    $"{name}: no PresentAsync narration extracted — parser broken or file empty.");

                foreach (var n in narrations)
                {
                    if (n.Text.Length < MinCharsPerBeat)
                    {
                        failures.Add(
                            $"{name} beat #{n.Index}: {n.Text.Length} chars < {MinCharsPerBeat}. " +
                            $"First 60 chars: \"{n.Text[..Math.Min(60, n.Text.Length)]}...\"");
                    }
                }
            }

            Assert.True(failures.Count == 0,
                "Narration regression:" + Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        [Fact]
        public void Human_narration_avoids_developer_jargon()
        {
            // These tokens are code identifiers or dev shorthand that must
            // NEVER appear in the spoken narration — they were the main
            // symptom of the pre-rewrite text ("CreateUserAsync", "SelectCommand",
            // "ViewModel", "cmd", "vm"). If a future edit re-introduces them
            // this test flags it before the demo agent reads them aloud.
            var jargon = new[]
            {
                "ViewModel", "CommandAsync", "InitNewAsync", "LoadAsync",
                "SwitchDetailTabCommand", "SelectCommand", "SelectUserCommand",
                "SaveCompanyCommand", "CreateUserAsync", "NextStepCommand",
                "PreviewCommand", "RefreshCommand", "ClearFiltersCommand",
            };

            var root = LocateWorkflowsRoot();
            var failures = new List<string>();

            foreach (var name in EnrichedWorkflows)
            {
                var path = Path.Combine(root, name);
                var source = File.ReadAllText(path);
                foreach (var n in ExtractNarrations(source))
                {
                    foreach (var j in jargon)
                    {
                        // '.Command' etc. inside a $"{x.Command}" IS in the
                        // narration string but is an interpolation — we only
                        // fail on unbraced literal jargon by requiring the
                        // token to be surrounded by whitespace/punctuation.
                        var pattern = @"\b" + Regex.Escape(j) + @"\b";
                        if (Regex.IsMatch(n.Text, pattern))
                        {
                            failures.Add($"{name} beat #{n.Index} contains dev jargon '{j}'.");
                        }
                    }
                }
            }

            Assert.True(failures.Count == 0,
                "Narration contains developer jargon:" + Environment.NewLine +
                string.Join(Environment.NewLine, failures));
        }

        // -- helpers ----------------------------------------------------------

        private sealed record NarrationBeat(int Index, string Text);

        private static List<NarrationBeat> ExtractNarrations(string source)
        {
            var result = new List<NarrationBeat>();
            // Walk the source manually. Regex is not safe here because
            // interpolated strings can contain nested quoted expressions
            // such as $"…{string.Join(", ", visited)}…" — a naive
            // "[^"]*" pattern terminates on the inner quote and reports
            // truncated narration lengths.
            //
            // For every occurrence of "PresentAsync(" we skip whitespace
            // and commas until we have consumed exactly three C# string
            // arguments (events + "id" + "title") and then capture the
            // FOURTH argument as the narration — parsing it with brace/
            // quote awareness so interpolations are treated as opaque.
            int idx = 0;
            int beat = 0;
            while ((idx = source.IndexOf("PresentAsync(", idx, StringComparison.Ordinal)) >= 0)
            {
                idx += "PresentAsync(".Length;
                // Skip 'events' identifier argument.
                if (!SkipArgument(source, ref idx)) break;
                // Skip 'id' string argument.
                if (!SkipArgument(source, ref idx)) break;
                // Skip 'title' string argument.
                if (!SkipArgument(source, ref idx)) break;
                // Capture 'narration' string argument.
                string? narr = ReadStringArgument(source, ref idx);
                if (narr == null) continue;
                // Replace interpolation expressions {…} with a placeholder
                // so we measure only human-readable narration length.
                narr = Regex.Replace(narr, @"\{[^{}]*\}", "###");
                narr = narr.Replace("\\\"", "\"").Replace("\\n", "\n").Replace("\\\\", "\\");
                result.Add(new NarrationBeat(++beat, narr));
            }
            return result;
        }

        /// <summary>Skip whitespace, then one C# argument, then the trailing comma.</summary>
        private static bool SkipArgument(string src, ref int i)
        {
            SkipWs(src, ref i);
            if (i >= src.Length) return false;
            if (src[i] == '$' || src[i] == '@' || src[i] == '"')
            {
                ReadStringArgument(src, ref i);
            }
            else
            {
                // identifier / expression up to the next top-level comma.
                int depth = 0;
                while (i < src.Length)
                {
                    char c = src[i];
                    if (c == '(' || c == '{' || c == '[') depth++;
                    else if (c == ')' || c == '}' || c == ']')
                    {
                        if (depth == 0) return false;
                        depth--;
                    }
                    else if (c == ',' && depth == 0) break;
                    i++;
                }
            }
            SkipWs(src, ref i);
            if (i < src.Length && src[i] == ',') { i++; return true; }
            return false;
        }

        /// <summary>Read a C# string literal ("…" or $"…" or @"…") and return its raw
        /// content (without the surrounding quotes / $ / @). Advances i past the closer.</summary>
        private static string? ReadStringArgument(string src, ref int i)
        {
            SkipWs(src, ref i);
            bool interpolated = false, verbatim = false;
            if (i < src.Length && src[i] == '$') { interpolated = true; i++; }
            if (i < src.Length && src[i] == '@') { verbatim = true; i++; }
            if (i >= src.Length || src[i] != '"') return null;
            i++; // opening "
            int start = i;
            int braceDepth = 0;
            while (i < src.Length)
            {
                char c = src[i];
                if (interpolated && c == '{' && i + 1 < src.Length && src[i + 1] == '{') { i += 2; continue; }
                if (interpolated && c == '}' && i + 1 < src.Length && src[i + 1] == '}') { i += 2; continue; }
                if (interpolated && c == '{') { braceDepth++; i++; continue; }
                if (interpolated && c == '}') { braceDepth--; i++; continue; }
                if (braceDepth > 0)
                {
                    // Inside an interpolation expression — skip nested strings/braces.
                    if (c == '"')
                    {
                        i++;
                        while (i < src.Length && src[i] != '"')
                        {
                            if (!verbatim && src[i] == '\\' && i + 1 < src.Length) i++;
                            i++;
                        }
                        if (i < src.Length) i++;
                        continue;
                    }
                    i++;
                    continue;
                }
                if (!verbatim && c == '\\' && i + 1 < src.Length) { i += 2; continue; }
                if (c == '"') { break; }
                i++;
            }
            if (i >= src.Length) return null;
            string content = src.Substring(start, i - start);
            i++; // closing "
            return content;
        }

        private static void SkipWs(string src, ref int i)
        {
            while (i < src.Length && (char.IsWhiteSpace(src[i]) || src[i] == '\r' || src[i] == '\n'))
                i++;
        }

        private static string LocateWorkflowsRoot()
        {
            // Walk up from the test binary until we find a folder that has
            // Agent/Demo/Workflows underneath. Keeps the test hermetic — no
            // hard-coded repo path.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "Agent", "Demo", "Workflows");
                if (Directory.Exists(candidate) &&
                    File.Exists(Path.Combine(candidate, "DashboardWorkflow.cs")))
                {
                    return candidate;
                }
                dir = dir.Parent;
            }
            throw new DirectoryNotFoundException(
                "Could not locate Agent/Demo/Workflows above " + AppContext.BaseDirectory);
        }
    }
}
