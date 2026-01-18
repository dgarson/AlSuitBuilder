namespace AlSuitBuilder.Server.Persistence
{
    /// <summary>
    /// Types of events that can occur during a build for logging purposes.
    /// </summary>
    public enum BuildEventType
    {
        BuildStarted,
        BuildResumed,
        BuildCompleted,
        BuildCancelled,
        BuildCrashDetected,
        WorkItemAssigned,
        WorkItemCompleted,
        WorkItemFailed,
        WorkItemRetry,
        WorkItemSkippedOnDropoff,
        DuplicateItemDetected,
        ClientConnected,
        ClientDisconnected,
        CharacterSwitch,
        Error
    }
}
