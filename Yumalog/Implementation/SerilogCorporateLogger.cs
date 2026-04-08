namespace Yumalog.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Serilog.Core;
    using Serilog.Events;
    using Serilog.Parsing;
    using Yumalog.Abstractions;
    using Yumalog.Diagnostics;

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
        private readonly Logger _logger;
        private readonly string _applicationName;
        private readonly string _logDirectory;
        private readonly Action<CorporateLogDiagnosticEvent> _diagnosticListener;
        private bool _disposed;

        /// <summary>
        /// Creates a new Yumalog wrapper around a configured Serilog logger.
        /// </summary>
        /// <param name="logger">Underlying Serilog logger instance.</param>
        /// <param name="applicationName">Application name associated with the logger instance.</param>
        /// <param name="logDirectory">Application-specific directory where logs are written.</param>
        /// <param name="diagnosticListener">Optional lifecycle diagnostic callback.</param>
        public SerilogCorporateLogger(
            Logger logger,
            string applicationName,
            string logDirectory,
            Action<CorporateLogDiagnosticEvent> diagnosticListener = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            _diagnosticListener = diagnosticListener;
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
            _logger.Information("{Message} {@Data}", message, data);
        }

        /// <summary>
        /// Flushes pending events and releases the underlying Serilog resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            EmitDiagnostic(CorporateLogDiagnosticEventType.ShutdownStarted,
                "Logger shutdown started. Buffered events are being flushed.");

            try
            {
                _logger.Dispose();
                _disposed = true;

                EmitDiagnostic(CorporateLogDiagnosticEventType.ShutdownCompleted,
                    "Logger shutdown completed successfully.");
            }
            catch (Exception ex)
            {
                EmitDiagnostic(CorporateLogDiagnosticEventType.ShutdownFailed,
                    "Logger shutdown failed before all buffered events could be flushed.",
                    ex);
                throw;
            }
        }

        private void WriteLog(LogEventLevel level, string message, Exception exception, IDictionary<string, object> properties)
        {
            ThrowIfDisposed();

            if (properties == null || properties.Count == 0)
            {
                _logger.Write(level, exception, message);
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

            _logger.Write(logEvent);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(SerilogCorporateLogger));
            }
        }

        private void EmitDiagnostic(
            CorporateLogDiagnosticEventType eventType,
            string message,
            Exception exception = null)
        {
            var listener = _diagnosticListener;
            if (listener == null)
            {
                return;
            }

            listener(new CorporateLogDiagnosticEvent(
                eventType,
                _applicationName,
                _logDirectory,
                message,
                exception));
        }
    }
}
