namespace AlSuitBuilder.Server.Persistence
{
    /// <summary>
    /// Represents the current status of a build for persistence purposes.
    /// </summary>
    public enum BuildStatus
    {
        /// <summary>
        /// Build is actively running.
        /// </summary>
        Active = 0,

        /// <summary>
        /// Build completed successfully.
        /// </summary>
        Completed = 1,

        /// <summary>
        /// Build was cancelled by user.
        /// </summary>
        Cancelled = 2,

        /// <summary>
        /// Build was interrupted by server crash (detected on recovery).
        /// </summary>
        Crashed = 3,

        /// <summary>
        /// Build is being resumed after a crash.
        /// </summary>
        Resuming = 4
    }
}
