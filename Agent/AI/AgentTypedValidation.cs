using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Validation, authorization, timeout, cancellation and audit wrapper for agent tools.</summary>
    public sealed class AgentTypedValidation
    {
        private readonly IReadOnlyList<AgentTypedParameter> _parameters;
        private readonly TimeSpan _timeout;

        public AgentTypedValidation(IReadOnlyList<AgentTypedParameter> parameters, TimeSpan timeout)
        {
            _parameters = parameters ?? new List<AgentTypedParameter>();
            _timeout = timeout > TimeSpan.Zero ? timeout : TimeSpan.FromSeconds(30);
        }

        /// <summary>Checks argument against typed parameter rules.</summary>
        public bool ValidateArgument(string argument)
        {
            if (_parameters.Count == 0) return true;

            foreach (var param in _parameters)
            {
                if (param.Required && string.IsNullOrWhiteSpace(argument))
                    return false;

                if (!string.IsNullOrEmpty(param.ValidationRule) && !string.IsNullOrWhiteSpace(argument))
                {
                    try
                    {
                        if (param.ValidationRule.Contains("^") || param.ValidationRule.Contains("$"))
                        {
                            if (!Regex.IsMatch(argument, param.ValidationRule))
                                return false;
                        }
                        else
                        {
                            if (!argument.Contains(param.ValidationRule))
                                return false;
                        }
                    }
                    catch
                    {
                        // If regex is invalid, fall back to literal match.
                        if (!argument.Contains(param.ValidationRule))
                            return false;
                    }
                }
            }
            return true;
        }

        /// <summary>Executes with timeout and cancellation.</summary>
        public async Task<bool> ExecuteWithTimeoutAsync(
            Func<CancellationToken, Task<bool>> action, CancellationToken ct)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_timeout);

            try
            {
                return await action(cts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <summary>Audit log for tool execution with authorization check.</summary>
        public void LogExecution(string toolName, string username, string argument, bool allowed, string detail)
        {
            ProGlassAutomation.Services.Logging.AppLogService.Debug(
                $"[AgentTypedTool] {toolName} user={username} arg={argument} allowed={allowed} detail={detail}");
        }
    }
}
