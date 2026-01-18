namespace AlSuitBuilder.Server.Persistence
{
    /// <summary>
    /// Represents the status of an individual work item for persistence.
    /// </summary>
    public enum WorkItemStatus
    {
        /// <summary>
        /// Work item is waiting to be processed.
        /// </summary>
        Pending = 0,

        /// <summary>
        /// Work item was sent to client and is being processed.
        /// </summary>
        InProgress = 1,

        /// <summary>
        /// Work item was successfully completed.
        /// </summary>
        Completed = 2,

        /// <summary>
        /// Work item failed after max retries.
        /// </summary>
        Failed = 3
    }
}
