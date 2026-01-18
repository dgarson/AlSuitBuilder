using System;

namespace AlSuitBuilder.Shared.Messages.Server
{
    /// <summary>
    /// Server response to a resume build request.
    /// </summary>
    [Serializable]
    public class ResumeBuildResponseMessage : INetworkMessage
    {
        /// <summary>
        /// Whether the build can be resumed.
        /// </summary>
        public bool CanResume;

        /// <summary>
        /// Status message explaining the result.
        /// </summary>
        public string Message;

        /// <summary>
        /// Name of the suit being resumed.
        /// </summary>
        public string SuitName;

        /// <summary>
        /// Number of remaining work items.
        /// </summary>
        public int RemainingItems;

        /// <summary>
        /// Total items in the original build.
        /// </summary>
        public int TotalItems;
    }
}
