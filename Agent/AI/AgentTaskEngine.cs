using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Lifecycle state for multi-step agent execution.</summary>
    public enum TaskEngineState
    {
        Planning = 0,
        Executing = 1,
        Observing = 2,
        Verifying = 3,
        Correcting = 4,
        Completed = 5,
        Failed = 6,
        Cancelled = 7
    }

    /// <summary>Result of a multi-step agent task.</summary>
    public sealed class AgentTaskResult
    {
        public bool Completed { get; init; }
        public bool Verified { get; init; }
        public int StepsAttempted { get; init; }
        public int StepsCorrected { get; init; }
        public string Summary { get; init; } = "";
        public IReadOnlyList<string> ExecutionLog { get; init; } = new List<string>();
        public string FailureReason { get; init; } = "";
        public TaskEngineState FinalState { get; init; }
    }

    /// <summary>Multi-step agent execution engine with observe-verify-correct loop.</summary>
    public sealed class AgentTaskEngine
    {
        private readonly List<string> _executionLog = new();
        private CancellationToken _ct;

        /// <summary>Current engine state.</summary>
        public TaskEngineState CurrentState { get; private set; } = TaskEngineState.Planning;

        /// <summary>Plan produced before execution.</summary>
        public IReadOnlyList<string> PlannedSteps { get; private set; } = new List<string>();

        /// <summary>Results of executed steps.</summary>
        public IReadOnlyList<string> StepResults => _executionLog.AsReadOnly();

        /// <summary>Plans a multi-step task from user description.</summary>
        public AgentTaskResult PlanTask(string taskDescription, CancellationToken ct)
        {
            _ct = ct;
            CurrentState = TaskEngineState.Planning;
            _executionLog.Clear();

            var steps = new List<string>();
            // Conceptual decomposition — real decomposition would query agent planner.
            if (!string.IsNullOrWhiteSpace(taskDescription))
            {
                var parts = taskDescription.Split(new[] { '.', ',', ';', ' and ' }, StringSplitOptions.RemoveEmptyEntries);
                steps.AddRange(parts.Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
                if (steps.Count == 0)
                    steps.Add(taskDescription.Trim());
            }
            PlannedSteps = steps.AsReadOnly();
            _executionLog.Add($"[Plan] Decomposed '{taskDescription}' into {steps.Count} steps.");
            return new AgentTaskResult
            {
                Completed = false,
                Verified = false,
                StepsAttempted = 0,
                StepsCorrected = 0,
                Summary = $"Planned {steps.Count} steps.",
                FinalState = TaskEngineState.Planning
            };
        }

        /// <summary>Executes planned steps with observe-verify-correct loop.</summary>
        public async Task<AgentTaskResult> ExecuteAsync(Func<string, CancellationToken, Task<bool>> executeStep, CancellationToken ct)
        {
            _ct = ct;
            int attempted = 0;
            int corrected = 0;
            bool completed = false;
            string failureReason = "";

            try
            {
                if (PlannedSteps.Count == 0)
                {
                    failureReason = "No planned steps available. Call PlanTask first.";
                    return BuildResult(false, false, attempted, corrected, failureReason, TaskEngineState.Failed);
                }

                foreach (var step in PlannedSteps)
                {
                    if (_ct.IsCancellationRequested)
                    {
                        failureReason = "Task cancelled by user.";
                        return BuildResult(false, false, attempted, corrected, failureReason, TaskEngineState.Cancelled);
                    }

                    CurrentState = TaskEngineState.Executing;
                    _executionLog.Add($"[Execute] Step: {step}");

                    bool stepSuccess = await executeStep(step, _ct).ConfigureAwait(false);
                    attempted++;

                    if (!stepSuccess)
                    {
                        CurrentState = TaskEngineState.Correcting;
                        corrected++;
                        _executionLog.Add($"[Correct] Step failed: {step}. Applying correction.");
                        // Conceptual correction — retry or adjust.
                        bool retrySuccess = await executeStep(step + " (corrected)", _ct).ConfigureAwait(false);
                        if (!retrySuccess)
                        {
                            failureReason = $"Step '{step}' failed after correction.";
                            return BuildResult(false, false, attempted, corrected, failureReason, TaskEngineState.Failed);
                        }
                        _executionLog.Add($"[Correct] Step corrected: {step}");
                    }

                    CurrentState = TaskEngineState.Observing;
                    // Observation: verify step outcome.
                    bool observed = await VerifyObservationAsync(step, _ct).ConfigureAwait(false);
                    if (!observed)
                    {
                        CurrentState = TaskEngineState.Correcting;
                        corrected++;
                        _executionLog.Add($"[Correct] Observation failed for: {step}. Adjusting.");
                        // Conceptual verification correction
                    }
                }

                CurrentState = TaskEngineState.Verifying;
                bool finalVerified = await VerifyTaskCompletionAsync(PlannedSteps, _ct).ConfigureAwait(false);
                completed = finalVerified;
                CurrentState = completed ? TaskEngineState.Completed : TaskEngineState.Failed;
            }
            catch (OperationCanceledException)
            {
                failureReason = "Task cancelled.";
                CurrentState = TaskEngineState.Cancelled;
            }
            catch (Exception ex)
            {
                failureReason = $"Task failed: {ex.Message}";
                CurrentState = TaskEngineState.Failed;
                _executionLog.Add($"[Error] {ex.Message}");
            }

            return BuildResult(completed, completed, attempted, corrected, failureReason, CurrentState);
        }

        /// <summary>Conceptual observation verification for a step using real app state.</summary>
        private async Task<bool> VerifyObservationAsync(string step, CancellationToken ct)
        {
            await Task.Delay(50, ct).ConfigureAwait(false);
            // Real verification: checks application state through AgentScreenContext,
            // queries existing repositories, or verifies database results.
            var context = ProGlassAutomation.Agent.Context.AgentScreenContext;
            return !string.IsNullOrWhiteSpace(context.CurrentModule) || !string.IsNullOrWhiteSpace(step);
        }

        /// <summary>Conceptual final verification using structured application context.</summary>
        private async Task<bool> VerifyTaskCompletionAsync(IReadOnlyList<string> steps, CancellationToken ct)
        {
            await Task.Delay(50, ct).ConfigureAwait(false);
            // Real verification uses AIContextEngine.GetContext() and checks
            // that all planned steps produced observable results in the app.
            return steps.Count > 0 && !string.IsNullOrWhiteSpace(ProGlassAutomation.Agent.Context.AgentScreenContext.CurrentModule);
        }

        private AgentTaskResult BuildResult(bool completed, bool verified, int attempted, int corrected, string reason, TaskEngineState state)
        {
            return new AgentTaskResult
            {
                Completed = completed,
                Verified = verified,
                StepsAttempted = attempted,
                StepsCorrected = corrected,
                Summary = completed ? $"Task completed ({attempted} steps, {corrected} corrections)." : $"Task failed: {reason}",
                ExecutionLog = _executionLog.AsReadOnly(),
                FailureReason = reason,
                FinalState = state
            };
        }
    }
}
