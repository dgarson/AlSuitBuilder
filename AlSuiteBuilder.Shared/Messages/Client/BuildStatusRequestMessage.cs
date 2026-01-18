using System;

namespace AlSuitBuilder.Shared.Messages.Client
{
    /// <summary>
    /// Client request for current build status.
    /// </summary>
    [Serializable]
    public class BuildStatusRequestMessage : INetworkMessage
    {
        // Empty - just signals request for status
    }
}
