using System;
using System.Runtime.Serialization;

namespace AlSuitBuilder.Server.Persistence
{
    /// <summary>
    /// Serializable representation of a work item for persistence.
    /// Extends the base WorkItem with status tracking for recovery.
    /// </summary>
    [DataContract]
    public class PersistentWorkItem
    {
        [DataMember(Order = 0)]
        public int Id { get; set; }

        [DataMember(Order = 1)]
        public string Character { get; set; }

        [DataMember(Order = 2)]
        public string ItemName { get; set; }

        [DataMember(Order = 3)]
        public int[] Requirements { get; set; }

        [DataMember(Order = 4)]
        public int MaterialId { get; set; }

        [DataMember(Order = 5)]
        public int SetId { get; set; }

        [DataMember(Order = 6)]
        public int Burden { get; set; }

        [DataMember(Order = 7)]
        public int Value { get; set; }

        [DataMember(Order = 8)]
        public DateTime LastAttempt { get; set; }

        [DataMember(Order = 9)]
        public WorkItemStatus Status { get; set; }

        [DataMember(Order = 10)]
        public int AttemptCount { get; set; }

        [DataMember(Order = 11)]
        public string LastError { get; set; }
    }
}
