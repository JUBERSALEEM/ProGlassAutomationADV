using System.Collections.ObjectModel;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProGlassAutomation.Agent.AI
{
    /// <summary>Task/project memory tier for agent execution history.</summary>
    public sealed class AgentTaskMemory
    {
        private readonly List<TaskMemoryItem> _items = new();
        private readonly object _lock = new();

        /// <summary>Adds a task memory item and optionally syncs with AgentMemoryService.</summary>
        public async Task AddTaskMemoryAsync(string taskId, string result, bool completed)
        {
            lock (_lock)
            {
                _items.Add(new TaskMemoryItem
                {
                    TaskId = taskId,
                    Result = result,
                    Completed = completed,
                    Timestamp = DateTime.UtcNow
                });
                while (_items.Count > 100)
                    _items.RemoveAt(0);
            }

            // Enhanced: sync approved facts to persistent AgentMemoryService if available.
            try
            {
                if (ProGlassAutomation.Agent.Core.AgentHost.IsBuilt &&
                    ProGlassAutomation.Agent.Core.AgentHost.Memory != null)
                {
                    await ProGlassAutomation.Agent.Core.AgentHost.Memory.AddApprovedAsync(
                        "task_memory", $"Task: {taskId}, Completed: {completed}, Result: {result}").ConfigureAwait(false);
                }
            }
            catch (System.Exception ex)
            {
                // Memory sync must not break task execution.
                ProGlassAutomation.Services.Logging.AppLogService.Debug("[AgentTaskMemory] Task-memory sync skipped: " + ex.Message);
            }
        }

        public IReadOnlyList<TaskMemoryItem> RecallTasks(string taskId) =>
            _items.Where(i => i.TaskId.Equals(taskId ?? "", StringComparison.OrdinalIgnoreCase)).ToList().AsReadOnly();
    }

    public sealed class TaskMemoryItem
    {
        public string TaskId { get; init; } = "";
        public string Result { get; init; } = "";
        public bool Completed { get; init; }
        public DateTime Timestamp { get; init; }
    }
}
