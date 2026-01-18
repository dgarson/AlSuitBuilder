using AlSuitBuilder.Server.Actions;
using AlSuitBuilder.Server.Data;
using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared;
using AlSuitBuilder.Shared.Messages.Client;
using AlSuitBuilder.Shared.Messages.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace AlSuitBuilder.Server
{
    internal class BuildInfo
    {
        /// <summary>
        /// Unique identifier for this build instance.
        /// </summary>
        public string BuildId { get; set; }

        // name aka filename
        public string Name { get; set; }

        // initiator for now
        public string DropCharacter { get; set; }

        // the first account done with their items will be designated as the relay character for anything found on the initiators account at the end of the build.
        public string RelayCharacter { get; set; }

        public List<WorkItem> WorkItems { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        private List<int> CompletedIds = new List<int>();

        public int InitiatedId { get; set; }



        internal void Tick()
        {


            if (WorkItems.Count == 0)
            {
                Console.WriteLine("Build complete");

                // Finalize persistence
                if (Program.PersistenceManager != null)
                {
                    try
                    {
                        var state = Program.PersistenceManager.LoadActiveState();
                        if (state != null)
                        {
                            // Log completion
                            Program.PersistenceManager.LogEvent(new BuildEventLog
                            {
                                Timestamp = DateTime.Now,
                                EventType = BuildEventType.BuildCompleted,
                                Message = "Build completed successfully"
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
                                FinalStatus = BuildStatus.Completed,
                                TotalItems = state.TotalItemCount,
                                CompletedItems = completedCount,
                                FailedItems = failedCount,
                                WasResumed = state.Status == BuildStatus.Resuming,
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

                Program.SendMessageToClient(InitiatedId, new InitiateBuildResponseMessage() { Accepted = true, Message = "Build completed" });
                Program.BuildInfo = null;
                return;
            }

            var clientIds = Program.GetClientIds().Except(CompletedIds).ToList();
            if (clientIds.Count == 0)
                return;


            try
            {
                foreach (var clientId in clientIds)
                {
                    var client = Program.GetClientInfo(clientId);
                    if (client == null || string.IsNullOrEmpty(client.CharacterName))
                        continue;

                    var clientWork = WorkItems.Where(o => o.Character == client.CharacterName && o.LastAttempt < DateTime.Now.AddSeconds(-30)).ToList();
                    if (!clientWork.Any())
                    {
                        if (client.OtherCharacters != null && client.OtherCharacters.Any())
                        {
                            clientWork = WorkItems.Where(o => o.LastAttempt < DateTime.Now.AddSeconds(-30) && client.OtherCharacters.ToList().Contains(o.Character)).ToList();
                            if (clientWork.Any())
                            {

                                // make sure we are not just waiting on acceptable requests to the current character before we go further.
                                if (WorkItems.Any(o => o.Character == client.CharacterName))
                                    continue;

                                clientWork.ForEach(o=> o.LastAttempt = DateTime.Now);

                                var firstWork = clientWork.First();
                                Utils.WriteWorkItemToLog($"Sending switch from {client.CharacterName} to {firstWork.Character}", firstWork, true);
                                Program.SendMessageToClient(clientId, new SwitchCharacterMessage() { Character = firstWork.Character });
                                // queue up character change
                                continue;
                            }

                            // client has no work on any chars ..

                            if (!WorkItems.Where(o => o.Character == client.CharacterName ||  client.OtherCharacters.ToList().Contains(o.Character)).Any())
                            {
                                Console.WriteLine("Completed Account " + client.CharacterName);
                                CompletedIds.Add(clientId);
                            }
                            continue;
                        }
                    }

                    var workItem = clientWork.FirstOrDefault();
                    if (workItem != null)
                    {


                        // set related work items to not attempt yet.
                        clientWork.ForEach(o => o.LastAttempt = DateTime.Now);

                        Utils.WriteWorkItemToLog("Sending work to client", workItem, true);
                        Program.SendMessageToClient(clientId, new GiveItemMessage()
                        {
                            WorkId = workItem.Id,
                            ItemName = workItem.ItemName,
                            MaterialId = workItem.MaterialId,
                            SetId = workItem.SetId,
                            RequiredSpells = workItem.Requirements,
                            DeliverTo = Program.BuildInfo.DropCharacter
                        });
                    }


                }
            }
            catch (Exception ex)
            {

                Utils.LogException(ex);
            }

        }
    }
}
