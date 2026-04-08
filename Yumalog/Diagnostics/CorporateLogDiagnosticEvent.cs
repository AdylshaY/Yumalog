namespace Yumalog.Diagnostics
{
    using System;

    /// <summary>
    /// Represents a diagnostic notification emitted by Yumalog runtime components.
    /// </summary>
    public sealed class CorporateLogDiagnosticEvent
    {
        /// <summary>
        /// Creates a new diagnostic event payload.
        /// </summary>
        /// <param name="eventType">The lifecycle event being reported.</param>
        /// <param name="applicationName">Application name associated with the logger instance.</param>
        /// <param name="logDirectory">Application-specific log directory.</param>
        /// <param name="message">Human-readable diagnostic message.</param>
        /// <param name="exception">Optional exception associated with the diagnostic event.</param>
        public CorporateLogDiagnosticEvent(
            CorporateLogDiagnosticEventType eventType,
            string applicationName,
            string logDirectory,
            string message,
            Exception exception = null,
            int? bufferSize = null,
            int? bufferCount = null,
            long? droppedMessagesCount = null)
        {
            EventType = eventType;
            ApplicationName = applicationName;
            LogDirectory = logDirectory;
            Message = message;
            Exception = exception;
            BufferSize = bufferSize;
            BufferCount = bufferCount;
            DroppedMessagesCount = droppedMessagesCount;
            TimestampUtc = DateTime.UtcNow;
        }

        /// <summary>
        /// Type of lifecycle event being reported.
        /// </summary>
        public CorporateLogDiagnosticEventType EventType { get; }

        /// <summary>
        /// Application name associated with the logger instance.
        /// </summary>
        public string ApplicationName { get; }

        /// <summary>
        /// Application-specific directory where log files are written.
        /// </summary>
        public string LogDirectory { get; }

        /// <summary>
        /// Human-readable description of the diagnostic event.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Optional exception associated with the diagnostic event.
        /// </summary>
        public Exception Exception { get; }

        /// <summary>
        /// Async sink buffer capacity when the diagnostic was emitted, if applicable.
        /// </summary>
        public int? BufferSize { get; }

        /// <summary>
        /// Async sink queue depth when the diagnostic was emitted, if applicable.
        /// </summary>
        public int? BufferCount { get; }

        /// <summary>
        /// Total number of dropped messages observed by the async sink, if applicable.
        /// </summary>
        public long? DroppedMessagesCount { get; }

        /// <summary>
        /// UTC timestamp for when the diagnostic event was created.
        /// </summary>
        public DateTime TimestampUtc { get; }
    }
}