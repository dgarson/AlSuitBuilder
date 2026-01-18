using AlSuitBuilder.Server.Actions;
using AlSuitBuilder.Server.Data;
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
        private const int MaxConsecutiveErrors = 10;
        private const int MaxTotalErrors = 50;

        // name aka filename
        public string Name { get; set; }

        // initiator for now
        public string DropCharacter { get; set; }

        public List<WorkItem> WorkItems { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        public int TotalErrors { get; private set; } = 0;
        public bool HasFailed { get; private set; } = false;
        public string FailureReason { get; private set; }

        private List<int> CompletedIds = new List<int>();

        public int InitiatedId { get; set; }

        /// <summary>
        /// Records an error for a work item. Returns true if the build should be terminated.
        /// </summary>
        public bool RecordError(int workItemId, string errorMessage)
        {
            TotalErrors++;
            Console.WriteLine($"Build error #{TotalErrors}: {errorMessage}");

            var workItem = WorkItems.FirstOrDefault(w => w.Id == workItemId);
            if (workItem != null)
            {
                workItem.ConsecutiveErrors++;
                if (workItem.ConsecutiveErrors >= MaxConsecutiveErrors)
                {
                    MarkFailed($"Work item '{workItem.ItemName}' failed {MaxConsecutiveErrors} consecutive times");
                    return true;
                }
            }

            if (TotalErrors >= MaxTotalErrors)
            {
                MarkFailed($"Build exceeded maximum error threshold ({MaxTotalErrors} errors)");
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets error count for a work item on success.
        /// </summary>
        public void RecordSuccess(int workItemId)
        {
            var workItem = WorkItems.FirstOrDefault(w => w.Id == workItemId);
            if (workItem != null)
            {
                workItem.ConsecutiveErrors = 0;
            }
        }

        /// <summary>
        /// Marks the build as failed and notifies the initiator.
        /// </summary>
        public void MarkFailed(string reason)
        {
            if (HasFailed) return;

            HasFailed = true;
            FailureReason = reason;
            EndTime = DateTime.Now;

            Console.WriteLine($"Build FAILED: {reason}");
            Program.SendMessageToClient(InitiatedId, new InitiateBuildResponseMessage()
            {
                Accepted = false,
                Message = $"Build failed: {reason}. {WorkItems.Count} items remaining."
            });
        }

        /// <summary>
        /// Marks the build as successfully completed.
        /// </summary>
        private void MarkCompleted()
        {
            EndTime = DateTime.Now;
            var duration = EndTime - StartTime;
            Console.WriteLine($"Build completed successfully in {duration.TotalMinutes:F1} minutes");
            Program.SendMessageToClient(InitiatedId, new InitiateBuildResponseMessage()
            {
                Accepted = true,
                Message = $"Build completed successfully in {duration.TotalMinutes:F1} minutes"
            });
        }

        internal void Tick()
        {
            // Don't process if build has failed
            if (HasFailed)
            {
                Program.BuildInfo = null;
                return;
            }

            if (WorkItems.Count == 0)
            {
                MarkCompleted();
                Program.BuildInfo = null;
                return;
            }

            var clientIds = Program.GetClientIds().Except(CompletedIds).ToList();
            if (clientIds.Count == 0)
            {
                // No clients available but work remains - check if we should fail
                if (WorkItems.Any())
                {
                    Console.WriteLine($"Warning: No clients available, {WorkItems.Count} work items remaining");
                }
                return;
            }

            try
            {
                foreach (var clientId in clientIds)
                {
                    ClientInfo client;
                    try
                    {
                        client = Program.GetClientInfo(clientId);
                    }
                    catch (Exception)
                    {
                        // Client may have disconnected
                        continue;
                    }

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

                                clientWork.ForEach(o => o.LastAttempt = DateTime.Now);

                                var firstWork = clientWork.First();
                                Utils.WriteWorkItemToLog($"Sending switch from {client.CharacterName} to {firstWork.Character}", firstWork, true);
                                Program.SendMessageToClient(clientId, new SwitchCharacterMessage() { Character = firstWork.Character });
                                // queue up character change
                                continue;
                            }

                            // client has no work on any chars ..

                            if (!WorkItems.Where(o => o.Character == client.CharacterName || client.OtherCharacters.ToList().Contains(o.Character)).Any())
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
                TotalErrors++;
                Console.WriteLine($"Error during Tick: {ex.Message}");
                Utils.LogException(ex);

                if (TotalErrors >= MaxTotalErrors)
                {
                    MarkFailed($"Too many errors during build processing");
                }
            }
        }
    }
}
