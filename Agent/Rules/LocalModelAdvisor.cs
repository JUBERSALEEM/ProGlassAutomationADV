// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Rules/LocalModelAdvisor.cs
// PROJECT: ProGlassAutomation — ProGlass Agent (PHASE F)
//
// Looks at the machine and RECOMMENDS a local model size. That is all.
//
// IT NEVER DOWNLOADS ANYTHING. No URL is opened, no file is fetched, no
// background job is scheduled. Pulling multi-gigabyte weights is a
// decision for the user to make deliberately, not something a settings
// page should start on its own.
//
// Only permissively licensed models are named (MIT / Apache-2.0) so the
// recommendation never pushes the user toward a licence they cannot use
// commercially.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Diagnostics;

namespace ProGlassAutomation.Agent.Rules
{
    /// <summary>What the local machine looks like.</summary>
    public sealed class HardwareSnapshot
    {
        public int LogicalCores { get; init; }
        public double TotalMemoryGb { get; init; }

        public string Summary =>
            $"{LogicalCores} logical core(s), {TotalMemoryGb:0.#} GB RAM";
    }

    /// <summary>Reads basic hardware facts. Never throws.</summary>
    public static class HardwareProbe
    {
        /// <summary>CPU count and total available memory.</summary>
        public static HardwareSnapshot Read()
        {
            int cores = 1;
            double memoryGb = 0;

            try
            {
                cores = Environment.ProcessorCount;

                long bytes = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                memoryGb = bytes / 1024d / 1024d / 1024d;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[HardwareProbe] read failed: {ex.Message}");
            }

            return new HardwareSnapshot
            {
                LogicalCores = cores,
                TotalMemoryGb = memoryGb
            };
        }
    }

    /// <summary>A model recommendation — advice only, never an action.</summary>
    public sealed class ModelRecommendation
    {
        /// <summary>True when this machine can reasonably run something.</summary>
        public bool Recommended { get; init; }

        /// <summary>Size tier, e.g. "7B Q4".</summary>
        public string Tier { get; init; } = "";

        /// <summary>Named candidates with permissive licences.</summary>
        public string Candidates { get; init; } = "";

        /// <summary>Plain-English explanation for the Settings card.</summary>
        public string Advice { get; init; } = "";
    }

    /// <summary>Maps hardware to a local-model recommendation.</summary>
    public static class LocalModelAdvisor
    {
        /// <summary>
        /// Recommends a tier from RAM. Thresholds are deliberately
        /// conservative: the ERP itself needs headroom, so the advice
        /// assumes the model is NOT the only thing running.
        /// </summary>
        public static ModelRecommendation Advise(HardwareSnapshot? hardware = null)
        {
            var hw = hardware ?? HardwareProbe.Read();

            if (hw.TotalMemoryGb >= 16)
            {
                return new ModelRecommendation
                {
                    Recommended = true,
                    Tier = "7B Q4",
                    Candidates = "Phi-3.5-mini (MIT) or Qwen2.5-7B-Instruct (Apache-2.0)",
                    Advice =
                        $"This machine ({hw.Summary}) could run a 7B Q4 quantized " +
                        "model locally — for example Phi-3.5-mini (MIT) or " +
                        "Qwen2.5-7B-Instruct (Apache-2.0). Nothing is downloaded " +
                        "automatically; this is a recommendation only."
                };
            }

            if (hw.TotalMemoryGb >= 8)
            {
                return new ModelRecommendation
                {
                    Recommended = true,
                    Tier = "3B Q4",
                    Candidates = "Phi-3.5-mini (MIT) at 3B class, Qwen2.5-3B (Apache-2.0)",
                    Advice =
                        $"This machine ({hw.Summary}) suits a smaller 3B Q4 model " +
                        "such as Qwen2.5-3B (Apache-2.0). Nothing is downloaded " +
                        "automatically; this is a recommendation only."
                };
            }

            return new ModelRecommendation
            {
                Recommended = false,
                Tier = "none",
                Candidates = "",
                Advice =
                    $"This machine ({hw.Summary}) does not have enough memory to " +
                    "run a local language model comfortably. The agent will keep " +
                    "using its built-in knowledge base, which needs no model."
            };
        }
    }
}
