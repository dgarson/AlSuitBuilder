using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared;
using System;
using System.Linq;

namespace AlSuitBuilder.Server.Actions
{
    /// <summary>
    /// Action to save the current build state to disk.
    /// Called after work item completion or other state changes.
    /// </summary>
    internal class SaveBuildStateAction : IServerAction
    {
        private readonly int? _completedWorkItemId;
        private readonly bool _success;
        private readonly string _errorMessage;

        /// <summary>
        /// Creates a save action for a work item completion.
        /// </summary>
        public SaveBuildStateAction(int completedWorkItemId, bool success, string errorMessage = null)
        {
            _completedWorkItemId = completedWorkItemId;
            _success = success;
            _errorMessage = errorMessage;
        }

        /// <summary>
        /// Creates a save action for a general state update.
        /// </summary>
        public SaveBuildStateAction()
        {
            _completedWorkItemId = null;
            _success = true;
        }

        public void Execute()
        {
            if (Program.PersistenceManager == null)
                return;

            try
            {
                var state = Program.PersistenceManager.LoadActiveState();
                if (state == null)
                    return;

                // If this is a work item completion, update the work item status
                if (_completedWorkItemId.HasValue)
                {
                    var workItem = state.WorkItems.FirstOrDefault(w => w.Id == _completedWorkItemId.Value);
                    if (workItem != null)
                    {
                        workItem.Status = _success ? WorkItemStatus.Completed : WorkItemStatus.Failed;
                        if (!_success)
                        {
                            workItem.AttemptCount++;
                            workItem.LastError = _errorMessage ?? "Delivery failed";
                        }

                        if (_success && !state.CompletedWorkItemIds.Contains(_completedWorkItemId.Value))
                        {
                            state.CompletedWorkItemIds.Add(_completedWorkItemId.Value);
                        }
                    }

                    // Log the event
                    Program.PersistenceManager.LogEvent(new BuildEventLog
                    {
                        Timestamp = DateTime.Now,
                        EventType = _success ? BuildEventType.WorkItemCompleted : BuildEventType.WorkItemFailed,
                        Message = $"Work item {_completedWorkItemId.Value} {(_success ? "completed" : "failed")}",
                        WorkItemId = _completedWorkItemId.Value,
                        Details = _errorMessage
                    });
                }

                // Sync with current BuildInfo if available
                if (Program.BuildInfo != null)
                {
                    // Mark items that are no longer in the active list as completed
                    var activeIds = Program.BuildInfo.WorkItems.Select(w => w.Id).ToList();
                    foreach (var pItem in state.WorkItems)
                    {
                        if (!activeIds.Contains(pItem.Id) && pItem.Status != WorkItemStatus.Completed)
                        {
                            pItem.Status = WorkItemStatus.Completed;
                            if (!state.CompletedWorkItemIds.Contains(pItem.Id))
                                state.CompletedWorkItemIds.Add(pItem.Id);
                        }
                    }
                }

                Program.PersistenceManager.SaveActiveState(state);
            }
            catch (Exception ex)
            {
                Utils.LogException(ex);
            }
        }
    }
}
