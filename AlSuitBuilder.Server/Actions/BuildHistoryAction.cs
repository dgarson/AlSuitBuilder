using AlSuitBuilder.Server.Persistence;
using AlSuitBuilder.Shared;
using AlSuitBuilder.Shared.Messages.Server;
using System;

namespace AlSuitBuilder.Server.Actions
{
    /// <summary>
    /// Action to get build history.
    /// </summary>
    internal class BuildHistoryAction : IServerAction
    {
        private readonly int _clientId;
        private readonly int _maxEntries;

        public BuildHistoryAction(int clientId, int maxEntries = 10)
        {
            _clientId = clientId;
            _maxEntries = maxEntries;
        }

        public void Execute()
        {
            var response = new BuildHistoryResponseMessage();

            try
            {
                if (Program.PersistenceManager == null)
                {
                    Program.SendMessageToClient(_clientId, response);
                    return;
                }

                var history = Program.PersistenceManager.GetRecentHistory(_maxEntries);

                foreach (var entry in history)
                {
                    response.Entries.Add(new BuildHistoryItem
                    {
                        SuitName = entry.SuitName,
                        DropCharacter = entry.DropCharacter,
                        StartTime = entry.StartTime,
                        EndTime = entry.EndTime,
                        Status = entry.FinalStatus.ToString(),
                        TotalItems = entry.TotalItems,
                        CompletedItems = entry.CompletedItems,
                        FailedItems = entry.FailedItems,
                        WasResumed = entry.WasResumed
                    });
                }

                Program.SendMessageToClient(_clientId, response);
            }
            catch (Exception ex)
            {
                Utils.LogException(ex);
                Program.SendMessageToClient(_clientId, response);
            }
        }
    }
}
