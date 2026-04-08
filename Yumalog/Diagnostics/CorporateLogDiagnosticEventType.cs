namespace Yumalog.Diagnostics
{
    /// <summary>
    /// Identifies the type of lifecycle diagnostic emitted by Yumalog.
    /// </summary>
    public enum CorporateLogDiagnosticEventType
    {
        /// <summary>
        /// Async sink buffer monitoring has started.
        /// </summary>
        AsyncBufferMonitoringStarted = 1,

        /// <summary>
        /// Async sink buffer monitoring has stopped.
        /// </summary>
        AsyncBufferMonitoringStopped = 2,

        /// <summary>
        /// Async sink buffer usage exceeded the configured warning threshold.
        /// </summary>
        AsyncBufferHighUsage = 3,

        /// <summary>
        /// Async sink buffer dropped one or more messages.
        /// </summary>
        AsyncBufferDroppedMessages = 4,

        /// <summary>
        /// Logger shutdown has started and buffered events are being flushed.
        /// </summary>
        ShutdownStarted = 5,

        /// <summary>
        /// Logger shutdown completed successfully.
        /// </summary>
        ShutdownCompleted = 6,

        /// <summary>
        /// Logger shutdown failed before the flush/dispose sequence completed.
        /// </summary>
        ShutdownFailed = 7,
    }
}