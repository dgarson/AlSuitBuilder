using System;

namespace AlSuitBuilder.Shared.Messages.Server
{
    /// <summary>
    /// Server response to an abandon build request.
    /// </summary>
    [Serializable]
    public class AbandonBuildResponseMessage : INetworkMessage
    {
        /// <summary>
        /// Whether the build was successfully abandoned.
        /// </summary>
        public bool Success;

        /// <summary>
        /// Status message.
        /// </summary>
        public string Message;
    }
}
