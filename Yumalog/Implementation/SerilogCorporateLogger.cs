namespace Yumalog.Implementation
{
    using System;
    using System.Collections.Generic;
    using Serilog.Core;
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
            _logger.Information(message + " {@Data}", data);
        }

        #endregion

        #region Graceful Shutdown

        public void FlushAndShutdown()
        {
            _logger?.Dispose();
        }

        #endregion

        #region Private Helpers

        private void WriteLog(Serilog.Events.LogEventLevel level, string message, Exception exception, IDictionary<string, object> properties)
        {
            if (properties == null || properties.Count == 0)
            {
                _logger.Write(level, exception, message);
                return;
            }

            var enrichedLogger = _logger;
            foreach (var prop in properties)
            {
                enrichedLogger = (Logger)enrichedLogger.ForContext(prop.Key, prop.Value);
            }

            enrichedLogger.Write(level, exception, message);
        }

        #endregion
    }
}
