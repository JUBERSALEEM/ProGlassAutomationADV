// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Models/LocalModelCatalog.cs
// PROJECT: ProGlassAutomation — Local LLM program (2026-09-04)
//
// The SINGLE list of models the one-click installer may offer.
//
// WHAT MAKES AN ENTRY ALLOWED ON THIS LIST:
//   1. FREELY DOWNLOADABLE without an account or token (a URL the user
//      could paste into a browser and get the file — no HF auth-gate).
//   2. PERMISSIVE LICENSE, commercial-friendly: Apache-2.0 or MIT only.
//   3. GGUF format (llama.cpp-compatible — that is what LLamaSharp runs).
//   4. Named summary of what it is good at.
//
// WHY NO LLAMA/GOOGLE/GEMMA ENTRIES HERE:
//   They require the user to accept model-specific terms on the host site
//   and/or use gated downloads. The app must not accept licences ON the
//   user's behalf, and gated URLs would break the installer. Those
//   models remain a manual decision for the user OUTSIDE this catalog.
//
// THE CHINESE OFFERING THE USER ASKED FOR:
//   Qwen2.5 is Alibaba Cloud's model family — the strongest free local
//   option for Chinese (and English, and the bilingual factory-floor
//   mix). Every entry is Apache-2.0; quantization by bartowski, the
//   community-standard GGUF maintainer on Hugging Face.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.LocalAi
{
    /// <summary>One installable local model entry.</summary>
    public sealed class LocalModelEntry
    {
        /// <summary>Stable id used in settings/state (never rename).</summary>
        public string Id { get; init; } = "";

        /// <summary>Short UI title.</summary>
        public string Title { get; init; } = "";

        /// <summary>One-line "what this is good at".</summary>
        public string Description { get; init; } = "";

        /// <summary>Per-entry license id (Apache-2.0 / MIT only in this catalog).</summary>
        public string LicenseName { get; init; } = "";

        /// <summary>License URL the user can open BEFORE installing.</summary>
        public string LicenseUrl { get; init; } = "";

        /// <summary>Direct GGUF download URL (https only, HF host only).</summary>
        public string DownloadUrl { get; init; } = "";

        /// <summary>Expected file size in MB (used for disk-space check).</summary>
        public long SizeMb { get; init; }

        /// <summary>Soft minimum RAM in GB where this runs comfortably.</summary>
        public int MinRamGb { get; init; }

        /// <summary>File name when stored locally: {Id}.gguf.</summary>
        public string FileName => Id + ".gguf";
    }

    /// <summary>Static, honest, permissive-only catalog.</summary>
    public static class LocalModelCatalog
    {
        public static readonly LocalModelEntry[] All =
        {
            new()
            {
                Id = "qwen2.5-0.5b",
                Title = "Qwen 2.5 · 0.5B (Alibaba · CN/EN)",
                Description =
                    "Smallest usable assistant. Runs on very low RAM (≥2 GB free). " +
                    "Good for quick Q&A and commands; weak on long reasoning.",
                LicenseName = "Apache-2.0",
                LicenseUrl  = "https://huggingface.co/Qwen/Qwen2.5-0.5B-Instruct/blob/main/LICENSE",
                DownloadUrl =
                    "https://huggingface.co/bartowski/Qwen2.5-0.5B-Instruct-GGUF/resolve/main/" +
                    "Qwen2.5-0.5B-Instruct-Q4_K_M.gguf",
                SizeMb = 510,
                MinRamGb = 2
            },
            new()
            {
                Id = "qwen2.5-1.5b",
                Title = "Qwen 2.5 · 1.5B (Alibaba · CN/EN)",
                Description =
                    "Best 'every-PC' pick: strong bilingual (中文/English) answers in " +
                    "~1 GB of memory. Recommended for 4 GB machines.",
                LicenseName = "Apache-2.0",
                LicenseUrl  = "https://huggingface.co/Qwen/Qwen2.5-1.5B-Instruct/blob/main/LICENSE",
                DownloadUrl =
                    "https://huggingface.co/bartowski/Qwen2.5-1.5B-Instruct-GGUF/resolve/main/" +
                    "Qwen2.5-1.5B-Instruct-Q4_K_M.gguf",
                SizeMb = 1040,
                MinRamGb = 4
            },
            new()
            {
                Id = "qwen2.5-3b",
                Title = "Qwen 2.5 · 3B (Alibaba · CN/EN)",
                Description =
                    "Noticeably sharper reasoning; still CPU-friendly. Recommended " +
                    "for 8 GB machines.",
                LicenseName = "Apache-2.0",
                LicenseUrl  = "https://huggingface.co/Qwen/Qwen2.5-3B-Instruct/blob/main/LICENSE",
                DownloadUrl =
                    "https://huggingface.co/bartowski/Qwen2.5-3B-Instruct-GGUF/resolve/main/" +
                    "Qwen2.5-3B-Instruct-Q4_K_M.gguf",
                SizeMb = 1980,
                MinRamGb = 8
            },
            new()
            {
                Id = "qwen2.5-7b",
                Title = "Qwen 2.5 · 7B (Alibaba · CN/EN)",
                Description =
                    "Top free tier: near-cloud quality offline. Needs ≥16 GB RAM " +
                    "and a patient CPU.",
                LicenseName = "Apache-2.0",
                LicenseUrl  = "https://huggingface.co/Qwen/Qwen2.5-7B-Instruct/blob/main/LICENSE",
                DownloadUrl =
                    "https://huggingface.co/bartowski/Qwen2.5-7B-Instruct-GGUF/resolve/main/" +
                    "Qwen2.5-7B-Instruct-Q4_K_M.gguf",
                SizeMb = 4680,
                MinRamGb = 16
            },
            new()
            {
                Id = "phi3.5-mini",
                Title = "Phi 3.5 mini · 3.8B (Microsoft · EN)",
                Description =
                    "Microsoft's small heavyweight — excellent English reasoning " +
                    "and instruction following. MIT licensed.",
                LicenseName = "MIT",
                LicenseUrl  = "https://huggingface.co/microsoft/Phi-3.5-mini-instruct/resolve/main/LICENSE",
                DownloadUrl =
                    "https://huggingface.co/bartowski/Phi-3.5-mini-instruct-GGUF/resolve/main/" +
                    "Phi-3.5-mini-instruct-Q4_K_M.gguf",
                SizeMb = 2390,
                MinRamGb = 8
            }
        };

        public static LocalModelEntry? Find(string? id) =>
            string.IsNullOrWhiteSpace(id)
                ? null
                : All.FirstOrDefault(e =>
                    string.Equals(e.Id, id, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The best entry for this machine's RAM. Never returns a model
        /// above the machine's comfortable tier — power users may pick a
        /// higher one MANUALLY (that is their call, stated in the UI).
        /// </summary>
        public static LocalModelEntry RecommendFor(double ramGb) =>
            All
                .Where(e => ramGb >= e.MinRamGb)
                .OrderByDescending(e => e.MinRamGb)
                .FirstOrDefault()
            ?? All[0]; // the 0.5B runs essentially anywhere
    }
}
