using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared;
using AlSuitBuilder.Shared.Messages.Server;
using System;
using System.Linq;

namespace AlSuitBuilder.Server.Actions
{
    /// <summary>
    /// Action to abandon a crashed build without resuming.
    /// </summary>
    internal class AbandonBuildAction : IServerAction
    {
        private readonly int _clientId;

        public AbandonBuildAction(int clientId)
        {
            _clientId = clientId;
        }

        public void Execute()
        {
            var response = new AbandonBuildResponseMessage();

            try
            {
                // Check if there's an active build (can't abandon an active build)
                if (Program.BuildInfo != null)
                {
                    response.Success = false;
                    response.Message = "Cannot abandon an active build. Use /alb cancel to cancel first.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                if (Program.PersistenceManager == null)
                {
                    response.Success = false;
                    response.Message = "Persistence is not enabled.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                var state = Program.PersistenceManager.LoadActiveState();
                if (state == null)
                {
                    response.Success = false;
                    response.Message = "No crashed build to abandon.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                // Log the abandonment
                Program.PersistenceManager.LogEvent(new BuildEventLog
                {
                    Timestamp = DateTime.Now,
                    EventType = BuildEventType.BuildCancelled,
                    Message = "Build abandoned by user"
                });

                // Add to history
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

                response.Success = true;
                response.Message = $"Abandoned build '{state.Name}' ({completedCount}/{state.TotalItemCount} items were completed)";

                Console.WriteLine($"[ABANDON] Build '{state.Name}' abandoned");
                Program.SendMessageToClient(_clientId, response);
            }
            catch (Exception ex)
            {
                Utils.LogException(ex);
                response.Success = false;
                response.Message = $"Error abandoning build: {ex.Message}";
                Program.SendMessageToClient(_clientId, response);
            }
        }
    }
}
