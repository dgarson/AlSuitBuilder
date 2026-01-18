using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared;
using System;
using System.Linq;

namespace AlSuitBuilder.Server.Actions
{
    internal class TerminateSuitAction : UnclearableAction
    {
        public override void Execute()
        {
            // Handle persistence before cancelling
            if (Program.BuildInfo != null && Program.PersistenceManager != null)
            {
                try
                {
                    var state = Program.PersistenceManager.LoadActiveState();
                    if (state != null)
                    {
                        // Log cancellation
                        Program.PersistenceManager.LogEvent(new BuildEventLog
                        {
                            Timestamp = DateTime.Now,
                            EventType = BuildEventType.BuildCancelled,
                            Message = "Build cancelled by user"
                        });

                        // Add history entry
                        var completedCount = state.WorkItems.Count(w => w.Status == WorkItemStatus.Completed);
                        var failedCount = state.WorkItems.Count(w => w.Status == WorkItemStatus.Failed);

                        var historyEntry = new BuildHistoryEntry
                        {
                            BuildId = state.BuildId,
                            SuitName = state.Name,
                            DropCharacter = state.DropCharacter,
                            StartTime = state.StartTime,
                            EndTime = DateTime.Now,
                            FinalStatus = BuildStatus.Cancelled,
                            TotalItems = state.TotalItemCount,
                            CompletedItems = completedCount,
                            FailedItems = failedCount,
                            WasResumed = false,
                            LogFilePath = Program.PersistenceManager.GetCurrentLogPath()
                        };

                        Program.PersistenceManager.AddHistoryEntry(historyEntry);
                        Program.PersistenceManager.ClearActiveState();
                        Program.PersistenceManager.CloseCurrentLog();
                    }
                }
                catch (Exception ex)
                {
                    Utils.LogException(ex);
                }
            }

            Program.CancelBuild();
        }
    }
}
