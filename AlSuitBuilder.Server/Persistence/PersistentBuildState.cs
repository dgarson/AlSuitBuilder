using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace AlSuitBuilder.Server.Persistence
{
    /// <summary>
    /// Serializable representation of the entire build state for persistence.
    /// This is saved to disk to enable crash recovery and build resume.
    /// </summary>
    [DataContract]
    public class PersistentBuildState
    {
        /// <summary>
        /// Schema version for forward compatibility.
        /// </summary>
        [DataMember(Order = 0)]
        public int Version { get; set; } = 1;

        /// <summary>
        /// Unique identifier for this build instance.
        /// </summary>
        [DataMember(Order = 1)]
        public string BuildId { get; set; }

        /// <summary>
        /// Original suit file name.
        /// </summary>
        [DataMember(Order = 2)]
        public string Name { get; set; }

        /// <summary>
        /// Character that initiated the build (items delivered here).
        /// </summary>
        [DataMember(Order = 3)]
        public string DropCharacter { get; set; }

        /// <summary>
        /// Relay character for items found on initiator's account.
        /// </summary>
        [DataMember(Order = 4)]
        public string RelayCharacter { get; set; }

        /// <summary>
        /// Client ID of the build initiator.
        /// </summary>
        [DataMember(Order = 5)]
        public int InitiatedId { get; set; }

        /// <summary>
        /// When the build was started.
        /// </summary>
        [DataMember(Order = 6)]
        public DateTime StartTime { get; set; }

        /// <summary>
        /// When the build ended (if completed/cancelled).
        /// </summary>
        [DataMember(Order = 7)]
        public DateTime? EndTime { get; set; }

        /// <summary>
        /// Last time the state was persisted.
        /// </summary>
        [DataMember(Order = 8)]
        public DateTime LastSaveTime { get; set; }

        /// <summary>
        /// Current build status.
        /// </summary>
        [DataMember(Order = 9)]
        public BuildStatus Status { get; set; }

        /// <summary>
        /// All work items (including completed ones for history).
        /// </summary>
        [DataMember(Order = 10)]
        public List<PersistentWorkItem> WorkItems { get; set; }

        /// <summary>
        /// IDs of completed work items.
        /// </summary>
        [DataMember(Order = 11)]
        public List<int> CompletedWorkItemIds { get; set; }

        /// <summary>
        /// Total items in the original build (for progress tracking).
        /// </summary>
        [DataMember(Order = 12)]
        public int TotalItemCount { get; set; }

        /// <summary>
        /// Path to the original .alb file.
        /// </summary>
        [DataMember(Order = 13)]
        public string OriginalFilePath { get; set; }

        public PersistentBuildState()
        {
            WorkItems = new List<PersistentWorkItem>();
            CompletedWorkItemIds = new List<int>();
        }
    }
}
