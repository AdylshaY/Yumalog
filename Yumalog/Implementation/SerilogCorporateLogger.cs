namespace Yumalog.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Serilog.Events;
    using Serilog.Parsing;
    using Yumalog.Abstractions;

    /// <summary>
    /// Internal Yumalog logger implementation backed by Serilog.
    /// </summary>
    /// <remarks>
    /// This class translates the application-facing <see cref="ICorporateLogger"/> contract into Serilog
    /// write calls and guards against usage after shutdown. It is intentionally sealed because it is not
    /// designed as an inheritance-based extension point.
    /// </remarks>
    internal sealed class SerilogCorporateLogger : ICorporateLogger, IDisposable
    {
        private readonly CorporateLogRuntime _runtime;

        /// <summary>
        /// Creates a new Yumalog wrapper around a configured Serilog logger.
        /// </summary>
        /// <param name="runtime">Shared runtime that owns the configured Serilog pipeline.</param>
        public SerilogCorporateLogger(CorporateLogRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        /// <inheritdoc />
        public void LogInformation(string message, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Information, message, null, properties);
        }

        /// <inheritdoc />
        public void LogWarning(string message, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Warning, message, null, properties);
        }

        /// <inheritdoc />
        public void LogError(string message, Exception exception = null, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Error, message, exception, properties);
        }

        /// <inheritdoc />
        public void LogDebug(string message, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Debug, message, null, properties);
        }

        /// <inheritdoc />
        public void LogFatal(string message, Exception exception = null, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Fatal, message, exception, properties);
        }

        /// <inheritdoc />
        public void LogInformationObject(string message, object data)
        {
            ThrowIfDisposed();
            _runtime.BaseLogger.Information("{Message} {@Data}", message, data);
        }

        /// <summary>
        /// Flushes pending events and releases the underlying Serilog resources.
        /// </summary>
        public void Dispose()
        {
            _runtime.Dispose();
        }

        private void WriteLog(LogEventLevel level, string message, Exception exception, IDictionary<string, object> properties)
        {
            ThrowIfDisposed();

            var logger = _runtime.BaseLogger;

            if (properties == null || properties.Count == 0)
            {
                logger.Write(level, exception, message);
                return;
            }

            // Build the log event directly so the wrapper can emit arbitrary structured properties
            // without creating additional logger instances for each call.
            var messageTemplate = new MessageTemplate(message, Enumerable.Empty<MessageTemplateToken>());
            var logEventProperties = properties.Select(kvp =>
                new LogEventProperty(kvp.Key, new ScalarValue(kvp.Value)));

            var logEvent = new LogEvent(
                DateTimeOffset.Now,
                level,
                exception,
                messageTemplate,
                logEventProperties);

            logger.Write(logEvent);
        }

        private void ThrowIfDisposed()
        {
            _runtime.ThrowIfDisposed();
        }
    }
}
