namespace Yumalog.Abstractions
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Corporate logging interface for structured logging with key-value pairs.
    /// All methods are non-blocking - uses Serilog.Sinks.Async internally for background I/O.
    /// </summary>
    public interface ICorporateLogger
    {
        // Logging methods - all non-blocking, writes to async buffer
        void LogInformation(string message, IDictionary<string, object> properties = null);
        void LogWarning(string message, IDictionary<string, object> properties = null);
        void LogError(string message, Exception exception = null, IDictionary<string, object> properties = null);
        void LogDebug(string message, IDictionary<string, object> properties = null);
        void LogFatal(string message, Exception exception = null, IDictionary<string, object> properties = null);

        // Structured logging with object
        void LogInformationObject(string message, object data);

        // Graceful shutdown - flushes buffer and releases resources
        void FlushAndShutdown();
    }
}
