using System;
using System.Collections.Generic;

namespace AlSuitBuilder.Shared.Messages.Server
{
    /// <summary>
    /// Server response with build history.
    /// </summary>
    [Serializable]
    public class BuildHistoryResponseMessage : INetworkMessage
    {
        /// <summary>
        /// List of history entries.
        /// </summary>
        public List<BuildHistoryItem> Entries;

        public BuildHistoryResponseMessage()
        {
            Entries = new List<BuildHistoryItem>();
        }
    }

    /// <summary>
    /// Simplified history entry for client display.
    /// </summary>
    [Serializable]
    public class BuildHistoryItem
    {
        public string SuitName;
        public string DropCharacter;
        public DateTime StartTime;
        public DateTime? EndTime;
        public string Status;
        public int TotalItems;
        public int CompletedItems;
        public int FailedItems;
        public bool WasResumed;
    }
}
