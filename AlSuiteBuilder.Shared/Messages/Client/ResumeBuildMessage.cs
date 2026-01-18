using System;

namespace AlSuitBuilder.Shared.Messages.Client
{
    /// <summary>
    /// Client request to resume a crashed/interrupted build.
    /// </summary>
    [Serializable]
    public class ResumeBuildMessage : INetworkMessage
    {
        // Empty - just signals intent to resume
    }
}
