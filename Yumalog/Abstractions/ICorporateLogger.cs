namespace Yumalog.Abstractions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Application-facing logging contract used by Yumalog consumers.
    /// </summary>
    /// <remarks>
    /// The implementation writes through an asynchronous Serilog pipeline under normal operation.
    /// When <c>BlockWhenFull</c> is enabled in the active configuration, a caller can still block
    /// temporarily if the in-memory queue is saturated. In Dependency Injection scenarios, the
    /// container owns logger disposal and shutdown flushing.
    /// </summary>
    public interface ICorporateLogger
    {
        /// <summary>
        /// Writes an informational event.
        /// </summary>
        /// <param name="message">Human-readable message text.</param>
        /// <param name="properties">Optional structured properties that will be emitted with the event.</param>
        void LogInformation(string message, IDictionary<string, object> properties = null);

        /// <summary>
        /// Writes a warning event.
        /// </summary>
        /// <param name="message">Human-readable message text.</param>
        /// <param name="properties">Optional structured properties that will be emitted with the event.</param>
        void LogWarning(string message, IDictionary<string, object> properties = null);

        /// <summary>
        /// Writes an error event.
        /// </summary>
        /// <param name="message">Human-readable message text.</param>
        /// <param name="exception">Optional exception associated with the failure.</param>
        /// <param name="properties">Optional structured properties that will be emitted with the event.</param>
        void LogError(string message, Exception exception = null, IDictionary<string, object> properties = null);

        /// <summary>
        /// Writes a debug event.
        /// </summary>
        /// <param name="message">Human-readable message text.</param>
        /// <param name="properties">Optional structured properties that will be emitted with the event.</param>
        void LogDebug(string message, IDictionary<string, object> properties = null);

        /// <summary>
        /// Writes a fatal event for unrecoverable failures.
        /// </summary>
        /// <param name="message">Human-readable message text.</param>
        /// <param name="exception">Optional exception associated with the failure.</param>
        /// <param name="properties">Optional structured properties that will be emitted with the event.</param>
        void LogFatal(string message, Exception exception = null, IDictionary<string, object> properties = null);

        /// <summary>
        /// Writes an informational event and destructures the supplied object into the payload.
        /// </summary>
        /// <param name="message">Human-readable message text.</param>
        /// <param name="data">Object to serialize into the structured event payload.</param>
        void LogInformationObject(string message, object data);
    }
}
