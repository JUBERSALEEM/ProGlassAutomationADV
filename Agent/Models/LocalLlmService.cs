// ═══════════════════════════════════════════════════════════════════════
// FILE: Agent/Models/LocalLlmService.cs
// PROJECT: ProGlassAutomation — Local LLM program (2026-09-04)
//
// Wraps LLamaSharp (llama.cpp bindings, MIT): loads one GGUF file from
// the user-approved local store and generates answers 100% on-device.
//
// DESIGN RULES:
//   • LAZY: nothing loads at app start. First question pays the one-time
//     load (~5-15 s for 1-3B Q4); every later turn is inference only.
//     A Settings toggle can switch the service off; nothing is RAM-resident
//     while it is off.
//   • BOUNDED: context 2048 tokens, max 640 generated tokens per turn,
//     history kept to the last 6 user/assistant exchanges — factory PCs
//     stay responsive.
//   • HONEST: if the file is missing or a load fails, the provider layer
//     falls back to the knowledge base — the model NEVER pretends.
//   • OFFLINE: there is NO network code in this class.
// ═══════════════════════════════════════════════════════════════════════

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LLama;
using LLama.Common;
using LLama.Sampling;
using ProGlassAutomation.Services.Logging;

namespace ProGlassAutomation.Agent.Models
{
    /// <summary>
    /// One loaded model. Dispose to free native memory (up to several GB).
    /// Thread-safe at the ai-query level (a per-turn semaphore), so two
    /// clicks on Send never interleave generations.
    /// </summary>
    public sealed class LocalLlmService : IDisposable
    {
        /// <summary>How far back the slide of conversation goes.</summary>
        private const int MaxHistoryTurns = 6;

        private const int MaxGeneratedTokens = 640;
        private const int ContextTokens = 2048;

        private readonly string _modelPath;
        private readonly SemaphoreSlim _turn = new(1, 1);
        private readonly List<(string Role, string Text)> _history = new();

        private LLamaWeights? _weights;
        private InteractiveExecutor? _executor;
        private bool _loadFailed;
        private bool _disposed;

        public LocalLlmService(string modelPath)
        {
            _modelPath = modelPath ?? "";
        }

        /// <summary>
        /// True after a failed load — lets the caller flip back to the
        /// knowledge base instead of retrying the same broken file.
        /// </summary>
        public bool LoadFailed => _loadFailed;

        public string ModelPath => _modelPath;

        /// <summary>
        /// Ask the model. Never throws: a cancelled or failed run returns
        /// null so the provider layer can say so truthfully.
        /// </summary>
        public async Task<string?> GenerateAsync(
            string systemPrompt, string userMessage, CancellationToken ct)
        {
            if (_disposed || _loadFailed) return null;

            try
            {
                await _turn.WaitAsync(ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return null; }

            try
            {
                if (!EnsureLoaded()) return null;

                _history.Add(("user", userMessage));
                TrimHistory();

                string prompt = BuildPrompt(systemPrompt);

                var inf = new InferenceParams
                {
                    MaxTokens = MaxGeneratedTokens,
                    AntiPrompts = new[] { "<|im_end|>", "<|im_start|>", "\nuser", "\nUser" },
                    SamplingPipeline = new DefaultSamplingPipeline
                    {
                        Temperature = 0.4f,   // conservative: ERP answers, not poetry
                        RepeatPenalty = 1.1f
                    }
                };

                var sb = new StringBuilder();
                await foreach (var token in _executor!.InferAsync(prompt, inf, ct)
                                   .ConfigureAwait(false))
                {
                    sb.Append(token);
                }

                string answer = sb.ToString()
                    .Replace("<|im_end|>", "")
                    .Replace("<|im_start|>", "")
                    .Trim();

                _history.Add(("assistant", answer));
                TrimHistory();

                return string.IsNullOrWhiteSpace(answer) ? null : answer;
            }
            catch (OperationCanceledException)
            {
                return null;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[LocalLlm] generation failed: {ex.Message}");
                return null;
            }
            finally
            {
                _turn.Release();
            }
        }

        /// <summary>Drop conversation memory (NOT the loaded weights).</summary>
        public void ClearConversation() => _history.Clear();

        private bool EnsureLoaded()
        {
            if (_executor != null) return true;
            try
            {
                // Literal-only assignments on purpose: LLamaSharp property
                // numeric types have moved between int/uint across versions;
                // a literal constant converts to either. Threads/MainGpu are
                // NOT set — backend defaults are the safe choice (all cores,
                // CPU backend already selected by the package).
                var parameters = new ModelParams(_modelPath)
                {
                    ContextSize = 2048,   // literal
                    GpuLayerCount = 0     // CPU-only: no driver surprises on OEM boxes
                };

                _weights = LLamaWeights.LoadFromFile(parameters);
                var ctx = _weights.CreateContext(parameters);
                _executor = new InteractiveExecutor(ctx);
                return true;
            }
            catch (Exception ex)
            {
                AppLogService.Debug($"[LocalLlm] load failed: {ex.Message}");
                _loadFailed = true;
                return false;
            }
        }

        /// <summary>
        /// ChatML — the native template of Qwen 2.5 (our primary catalog
        /// entry) and a template Phi-3.5 also handles cleanly. Kept as
        /// plain string building so there is no version-sensitive
        /// dependency on LLamaSharp helpers.
        /// </summary>
        private string BuildPrompt(string systemPrompt)
        {
            var sb = new StringBuilder();
            sb.Append("<|im_start|>system\n")
              .Append(systemPrompt.Trim())
              .Append("<|im_end|>\n");

            foreach (var (role, text) in _history)
            {
                sb.Append("<|im_start|>").Append(role).Append('\n')
                  .Append(text.Trim())
                  .Append("<|im_end|>\n");
            }

            sb.Append("<|im_start|>assistant\n");
            return sb.ToString();
        }

        private void TrimHistory()
        {
            while (_history.Count > MaxHistoryTurns * 2)
                _history.RemoveAt(0);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            try { _executor?.Context.Dispose(); }
            catch (Exception ex) { AppLogService.Debug($"[LocalLlm] context dispose: {ex.Message}"); }
            try { _weights?.Dispose(); }
            catch (Exception ex) { AppLogService.Debug($"[LocalLlm] weights dispose: {ex.Message}"); }
            try { _turn.Dispose(); }
            catch (Exception ex) { AppLogService.Debug($"[LocalLlm] semaphore dispose: {ex.Message}"); }

            _executor = null;
            _weights = null;
        }
    }
}
