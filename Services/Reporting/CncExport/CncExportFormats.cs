// ═══════════════════════════════════════════════════════════════════════
// FILE: Services/Reporting/CncExport/CncExportFormats.cs
// PROJECT: ProGlassAutomation
// MODULE: Optimization — machine-file export (PHASE 4)
//
// WHY THIS FILE
//   The REPORT tab has always shown four CNC format pills (Bottero .OPT,
//   Lisec .DAT, Intermac .ISO, Generic DXF) that were never wired to
//   anything — decoration standing in for a feature. Phase 0 made the cut
//   sequence exist; this is what turns it into something a table can read.
//
// THE HONEST PART
//   Three of those four formats are proprietary and undocumented. Public
//   sources disagree even on the extensions: the format lists published by
//   optimiser vendors give Lisec as OPT101.CNC and Bottero as *.CUT, not
//   the .DAT and .OPT this UI claims. Writing a "Lisec .DAT" from
//   guesswork would produce a file that LOOKS right and feeds a machine
//   wrong numbers — the one outcome worse than not shipping it at all.
//
//   So: formats that are documented are implemented, and formats that are
//   not are declared, named, and say exactly what finishes them (a sample
//   file from the machine). IsReady is the switch, and it is false for
//   every format that cannot be proven correct. Nothing here writes a
//   machine file it cannot stand behind.
// ═══════════════════════════════════════════════════════════════════════

using ProGlassAutomation.Services.Optimization;
using System;
using System.Collections.Generic;

namespace ProGlassAutomation.Services.Reporting.CncExport
{
    /// <summary>Everything a machine-file writer needs to know about a plan.</summary>
    public sealed class CncExportPlan
    {
        /// <summary>The plan to export. Sheets carry both pieces and cuts.</summary>
        public OptimizationResult Result { get; init; }

        /// <summary>Free-text provenance stamped into the file header.</summary>
        public string Source { get; init; } = string.Empty;

        /// <summary>Cut pattern the plan was built under, stamped for traceability.</summary>
        public string CutPattern { get; init; } = string.Empty;

        /// <summary>Blade gap, so a reader knows what the cut lines consumed.</summary>
        public int KerfMm { get; init; }

        /// <summary>Local timestamp stamped into the file header.</summary>
        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;
    }

    /// <summary>
    /// One export target. <see cref="IsReady"/> is the contract: a format
    /// with a null writer can be listed, explained and requested, but never
    /// silently written.
    /// </summary>
    public sealed class CncExportFormat
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
        public string Extension { get; init; } = string.Empty;

        /// <summary>True only when the writer produces output this codebase
        /// can prove correct against a published specification.</summary>
        public bool IsReady => Writer != null;

        /// <summary>What is true about this format, or — when it is not ready —
        /// exactly what would finish it. Shown to the operator either way.</summary>
        public string Note { get; init; } = string.Empty;

        /// <summary>Produces the file text. Null when the format is not ready.</summary>
        public Func<CncExportPlan, string>? Writer { get; init; }
    }

    public static class CncExportFormats
    {
        public static readonly CncExportFormat GenericDxf = new()
        {
            Id = "dxf",
            DisplayName = "Generic DXF",
            Extension = ".dxf",
            Note = "DXF R12 (AC1009) — sheet outline, every piece, and the cut "
                 + "sequence as lines in execution order. Documented format, "
                 + "readable by CAM and by the drawing packages the floor already has.",
            Writer = CncDxfWriter.Write
        };

        public static readonly CncExportFormat CutListCsv = new()
        {
            Id = "cutlist",
            DisplayName = "Cut list CSV",
            Extension = ".csv",
            Note = "One row per cut in execution order: sheet, sequence, axis, "
                 + "position, the rectangle the cut crosses, stage, and whether it "
                 + "is a trim. Opens in Excel; feeds any CAM that accepts a table.",
            Writer = CncCutListWriter.Write
        };

        public static readonly CncExportFormat LisecDat = new()
        {
            Id = "lisec",
            DisplayName = "Lisec .DAT",
            Extension = ".dat",
            Note = "Not implemented. Lisec does not publish this format; vendor "
                 + "format lists name Lisec output as OPT101.CNC. Send one sample "
                 + "file exported by your Lisec table and this can be written "
                 + "against it — until then a file that merely looks right would be "
                 + "dangerous to put in front of a machine."
        };

        public static readonly CncExportFormat BotteroOpt = new()
        {
            Id = "bottero",
            DisplayName = "Bottero .OPT",
            Extension = ".opt",
            Note = "Not implemented. Bottero does not publish this format and "
                 + "vendor format lists name Bottero output as *.CUT. Send one sample "
                 + "file exported by your Bottero table and this can be written "
                 + "against it."
        };

        public static readonly CncExportFormat IntermacIso = new()
        {
            Id = "intermac",
            DisplayName = "Intermac .ISO",
            Extension = ".iso",
            Note = "Not implemented. Intermac (Biesse) does not publish this "
                 + "format. Send one sample file exported by your Intermac table "
                 + "and this can be written against it."
        };

        public static IReadOnlyList<CncExportFormat> All { get; } = new[]
        {
            GenericDxf,
            CutListCsv,
            LisecDat,
            BotteroOpt,
            IntermacIso
        };

        /// <summary>Looks a format up by id. Unknown ids return null rather than
        /// defaulting, so a typo can never export to the wrong machine.</summary>
        public static CncExportFormat? ById(string? id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            for (int i = 0; i < All.Count; i++)
                if (string.Equals(All[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return All[i];
            return null;
        }
    }
}
