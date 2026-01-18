using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared;
using AlSuitBuilder.Shared.Messages.Server;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AlSuitBuilder.Server.Actions
{
    /// <summary>
    /// Action to resume a crashed or interrupted build.
    /// </summary>
    internal class ResumeBuildAction : IServerAction
    {
        private readonly int _clientId;

        public ResumeBuildAction(int clientId)
        {
            _clientId = clientId;
        }

        public void Execute()
        {
            var response = new ResumeBuildResponseMessage();

            try
            {
                // Check if there's already an active build
                if (Program.BuildInfo != null)
                {
                    response.CanResume = false;
                    response.Message = "A build is already in progress. Cancel it first with /alb cancel.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                // Check if persistence manager is available
                if (Program.PersistenceManager == null)
                {
                    response.CanResume = false;
                    response.Message = "Persistence is not enabled.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                // Load crashed/interrupted state
                var state = Program.PersistenceManager.LoadActiveState();
                if (state == null)
                {
                    response.CanResume = false;
                    response.Message = "No build to resume.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                if (state.Status != BuildStatus.Crashed && state.Status != BuildStatus.Active)
                {
                    response.CanResume = false;
                    response.Message = $"Build status is {state.Status}, cannot resume.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                // Get client info
                var clientInfo = Program.GetClientInfo(_clientId);
                if (clientInfo == null || string.IsNullOrEmpty(clientInfo.CharacterName))
                {
                    response.CanResume = false;
                    response.Message = "Client not properly connected. Please wait for full initialization.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                // Collect all connected characters
                var connectedCharacters = new List<string>();
                foreach (var cid in Program.GetClientIds())
                {
                    var client = Program.GetClientInfo(cid);
                    if (client != null && !string.IsNullOrEmpty(client.CharacterName))
                    {
                        connectedCharacters.Add(client.CharacterName);
                        if (client.OtherCharacters != null)
                            connectedCharacters.AddRange(client.OtherCharacters);
                    }
                }

                // Find pending items
                var pendingItems = state.WorkItems
                    .Where(w => w.Status != WorkItemStatus.Completed)
                    .ToList();

                if (!pendingItems.Any())
                {
                    // Build was actually complete
                    Program.PersistenceManager.ClearActiveState();
                    response.CanResume = false;
                    response.Message = "Build was already complete. Cleared stale state.";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                // Check if all required characters are connected
                var requiredCharacters = pendingItems
                    .Select(w => w.Character)
                    .Distinct()
                    .ToList();

                var missingCharacters = requiredCharacters
                    .Except(connectedCharacters, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                if (missingCharacters.Any())
                {
                    response.CanResume = false;
                    response.Message = $"Missing clients for characters: {string.Join(", ", missingCharacters)}";
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                // Reset in-flight items to pending (they may have failed during crash)
                foreach (var item in state.WorkItems.Where(w => w.Status == WorkItemStatus.InProgress))
                {
                    item.Status = WorkItemStatus.Pending;
                    item.LastAttempt = DateTime.MinValue;
                    item.AttemptCount++;
                }

                // Update initiator to current client
                state.InitiatedId = _clientId;
                state.DropCharacter = clientInfo.CharacterName;

                // Convert to BuildInfo and activate
                Program.BuildInfo = BuildPersistenceManager.ToBuildInfo(state);

                // Mark as resuming/active and save
                state.Status = BuildStatus.Active;
                Program.PersistenceManager.SaveActiveState(state);

                // Start/resume logging
                Program.PersistenceManager.StartBuildLog(state.BuildId);
                Program.PersistenceManager.LogEvent(new BuildEventLog
                {
                    Timestamp = DateTime.Now,
                    EventType = BuildEventType.BuildResumed,
                    Message = $"Build resumed with {pendingItems.Count} remaining items",
                    CharacterName = clientInfo.CharacterName
                });

                // Send success response
                response.CanResume = true;
                response.Message = $"Resuming build '{state.Name}'";
                response.SuitName = state.Name;
                response.RemainingItems = pendingItems.Count;
                response.TotalItems = state.TotalItemCount;

                Console.WriteLine($"[RESUME] Build '{state.Name}' resumed with {pendingItems.Count} items remaining");
                Program.SendMessageToClient(_clientId, response);
            }
            catch (Exception ex)
            {
                Utils.LogException(ex);
                response.CanResume = false;
                response.Message = $"Error resuming build: {ex.Message}";
                Program.SendMessageToClient(_clientId, response);
            }
        }
    }
}
