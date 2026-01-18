using System;

namespace AlSuitBuilder.Shared.Messages.Client
{
    /// <summary>
    /// Client request for build history.
    /// </summary>
    [Serializable]
    public class BuildHistoryRequestMessage : INetworkMessage
    {
        /// <summary>
        /// Maximum number of history entries to return.
        /// </summary>
        public int MaxEntries = 10;
    }
}
