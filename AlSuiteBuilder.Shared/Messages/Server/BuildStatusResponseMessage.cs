using System;

namespace AlSuitBuilder.Shared.Messages.Server
{
    /// <summary>
    /// Server response with current build status.
    /// </summary>
    [Serializable]
    public class BuildStatusResponseMessage : INetworkMessage
    {
        /// <summary>
        /// Whether there is an active build.
        /// </summary>
        public bool HasActiveBuild;

        /// <summary>
        /// Whether there is a crashed build that can be resumed.
        /// </summary>
        public bool HasCrashedBuild;

        /// <summary>
        /// Name of the current/crashed suit.
        /// </summary>
        public string SuitName;

        /// <summary>
        /// Total items in the build.
        /// </summary>
        public int TotalItems;

        /// <summary>
        /// Number of completed items.
        /// </summary>
        public int CompletedItems;

        /// <summary>
        /// Number of pending items.
        /// </summary>
        public int PendingItems;

        /// <summary>
        /// Number of failed items.
        /// </summary>
        public int FailedItems;

        /// <summary>
        /// Progress percentage (0-100).
        /// </summary>
        public double ProgressPercentage;

        /// <summary>
        /// When the build started.
        /// </summary>
        public DateTime StartTime;

        /// <summary>
        /// Elapsed time since build started.
        /// </summary>
        public TimeSpan ElapsedTime;

        /// <summary>
        /// Status message.
        /// </summary>
        public string Message;
    }
}
