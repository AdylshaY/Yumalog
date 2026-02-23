namespace Yumalog.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Serilog.Core;
    using Serilog.Events;
    using Serilog.Parsing;
    using Yumalog.Abstractions;

    /// <summary>
    /// Serilog-based implementation of corporate logger.
    /// </summary>
    internal class SerilogCorporateLogger : ICorporateLogger
    {
        private readonly Logger _logger;

        public SerilogCorporateLogger(Logger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Synchronous Methods

        public void LogInformation(string message, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Information, message, null, properties);
        }

        public void LogWarning(string message, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Warning, message, null, properties);
        }

        public void LogError(string message, Exception exception = null, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Error, message, exception, properties);
        }

        public void LogDebug(string message, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Debug, message, null, properties);
        }

        public void LogFatal(string message, Exception exception = null, IDictionary<string, object> properties = null)
        {
            WriteLog(Serilog.Events.LogEventLevel.Fatal, message, exception, properties);
        }

        public void LogInformationObject(string message, object data)
        {
            _logger.Information("{Message} {@Data}", message, data);
        }

        #endregion

        #region Graceful Shutdown

        public void FlushAndShutdown()
        {
            _logger?.Dispose();
        }

        #endregion

        #region Private Helpers

        private void WriteLog(LogEventLevel level, string message, Exception exception, IDictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                _logger.Write(level, exception, message);
                return;
            }

            // Optimization: Create LogEvent directly instead of chaining ForContext calls
            // This avoids creating intermediate Logger instances for each property
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

        #endregion
    }
}
