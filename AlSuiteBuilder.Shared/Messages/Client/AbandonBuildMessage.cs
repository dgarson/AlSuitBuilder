using System;

namespace AlSuitBuilder.Shared.Messages.Client
{
    /// <summary>
    /// Client request to abandon a crashed build without resuming.
    /// </summary>
    [Serializable]
    public class AbandonBuildMessage : INetworkMessage
    {
        // Empty - just signals intent to abandon
    }
}
