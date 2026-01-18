using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared;
using AlSuitBuilder.Shared.Messages.Server;
using System;
using System.Linq;

namespace AlSuitBuilder.Server.Actions
{
    /// <summary>
    /// Action to get current build status.
    /// </summary>
    internal class BuildStatusAction : IServerAction
    {
        private readonly int _clientId;

        public BuildStatusAction(int clientId)
        {
            _clientId = clientId;
        }

        public void Execute()
        {
            var response = new BuildStatusResponseMessage();

            try
            {
                // Check for active build first
                if (Program.BuildInfo != null)
                {
                    response.HasActiveBuild = true;
                    response.HasCrashedBuild = false;
                    response.SuitName = Program.BuildInfo.Name;
                    response.StartTime = Program.BuildInfo.StartTime;
                    response.ElapsedTime = DateTime.Now - Program.BuildInfo.StartTime;

                    // Get counts from persistence if available, otherwise from BuildInfo
                    if (Program.PersistenceManager != null)
                    {
                        var stats = Program.PersistenceManager.GetCurrentStatistics();
                        if (stats != null)
                        {
                            response.TotalItems = stats.TotalItems;
                            response.CompletedItems = stats.CompletedItems;
                            response.PendingItems = stats.PendingItems;
                            response.FailedItems = stats.FailedItems;
                            response.ProgressPercentage = stats.ProgressPercentage;
                        }
                        else
                        {
                            response.TotalItems = Program.BuildInfo.WorkItems.Count;
                            response.PendingItems = Program.BuildInfo.WorkItems.Count;
                        }
                    }
                    else
                    {
                        response.PendingItems = Program.BuildInfo.WorkItems.Count;
                        response.TotalItems = Program.BuildInfo.WorkItems.Count;
                    }

                    response.Message = $"Build '{response.SuitName}' in progress: {response.CompletedItems}/{response.TotalItems} items ({response.ProgressPercentage:F1}%)";
                }
                // Check for crashed build
                else if (Program.PersistenceManager?.HasActiveState() == true)
                {
                    var state = Program.PersistenceManager.LoadActiveState();
                    if (state != null)
                    {
                        response.HasActiveBuild = false;
                        response.HasCrashedBuild = true;
                        response.SuitName = state.Name;
                        response.TotalItems = state.TotalItemCount;
                        response.CompletedItems = state.WorkItems.Count(w => w.Status == WorkItemStatus.Completed);
                        response.PendingItems = state.WorkItems.Count(w => w.Status == WorkItemStatus.Pending || w.Status == WorkItemStatus.InProgress);
                        response.FailedItems = state.WorkItems.Count(w => w.Status == WorkItemStatus.Failed);
                        response.StartTime = state.StartTime;
                        response.ElapsedTime = (state.LastSaveTime > DateTime.MinValue ? state.LastSaveTime : DateTime.Now) - state.StartTime;
                        response.ProgressPercentage = response.TotalItems > 0 ? (response.CompletedItems * 100.0 / response.TotalItems) : 0;

                        response.Message = $"Crashed build '{state.Name}' found: {response.CompletedItems}/{response.TotalItems} items completed. Use /alb resume to continue or /alb abandon to discard.";
                    }
                    else
                    {
                        response.HasActiveBuild = false;
                        response.HasCrashedBuild = false;
                        response.Message = "No active build.";
                    }
                }
                else
                {
                    response.HasActiveBuild = false;
                    response.HasCrashedBuild = false;
                    response.Message = "No active build.";
                }

                Program.SendMessageToClient(_clientId, response);
            }
            catch (Exception ex)
            {
                Utils.LogException(ex);
                response.Message = $"Error getting status: {ex.Message}";
                Program.SendMessageToClient(_clientId, response);
            }
        }
    }
}
