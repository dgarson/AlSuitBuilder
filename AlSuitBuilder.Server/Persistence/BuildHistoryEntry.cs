using System;
using System.Runtime.Serialization;

namespace AlSuitBuilder.Server.Persistence
{
    /// <summary>
    /// Historical record of a completed/cancelled build.
    /// </summary>
    [DataContract]
    public class BuildHistoryEntry
    {
        [DataMember(Order = 0)]
        public string BuildId { get; set; }

        [DataMember(Order = 1)]
        public string SuitName { get; set; }

        [DataMember(Order = 2)]
        public string DropCharacter { get; set; }

        [DataMember(Order = 3)]
        public DateTime StartTime { get; set; }

        [DataMember(Order = 4)]
        public DateTime? EndTime { get; set; }

        [DataMember(Order = 5)]
        public BuildStatus FinalStatus { get; set; }

        [DataMember(Order = 6)]
        public int TotalItems { get; set; }

        [DataMember(Order = 7)]
        public int CompletedItems { get; set; }

        [DataMember(Order = 8)]
        public int FailedItems { get; set; }

        [DataMember(Order = 9)]
        public bool WasResumed { get; set; }

        [DataMember(Order = 10)]
        public string LogFilePath { get; set; }
    }
}
