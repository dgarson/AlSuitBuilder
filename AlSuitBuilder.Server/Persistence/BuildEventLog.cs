using System;
using System.Runtime.Serialization;

namespace AlSuitBuilder.Server.Persistence
{
    /// <summary>
    /// Individual event log entry for build activity tracking.
    /// </summary>
    [DataContract]
    public class BuildEventLog
    {
        [DataMember(Order = 0)]
        public DateTime Timestamp { get; set; }

        [DataMember(Order = 1)]
        public BuildEventType EventType { get; set; }

        [DataMember(Order = 2)]
        public string Message { get; set; }

        [DataMember(Order = 3)]
        public int? WorkItemId { get; set; }

        [DataMember(Order = 4)]
        public string CharacterName { get; set; }

        [DataMember(Order = 5)]
        public string Details { get; set; }

        public BuildEventLog()
        {
            Timestamp = DateTime.Now;
        }

        public BuildEventLog(BuildEventType eventType, string message)
            : this()
        {
            EventType = eventType;
            Message = message;
        }
    }
}
