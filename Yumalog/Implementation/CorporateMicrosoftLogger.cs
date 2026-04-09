namespace Yumalog.Implementation
{
    using System;
    using System.Collections.Generic;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// ASP.NET Core logger implementation that forwards to the shared Yumalog runtime.
    /// </summary>
    internal sealed class CorporateMicrosoftLogger : ILogger
    {
        private const string OriginalFormatPropertyName = "{OriginalFormat}";
        private readonly string _categoryName;
        private readonly CorporateLogRuntime _runtime;
        private readonly CorporateScopeProvider _scopeProvider;

        /// <summary>
        /// Initializes a new category-specific logger instance.
        /// </summary>
        public CorporateMicrosoftLogger(
            string categoryName,
            CorporateLogRuntime runtime,
            CorporateScopeProvider scopeProvider)
        {
            _categoryName = categoryName;
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
            _scopeProvider = scopeProvider ?? throw new ArgumentNullException(nameof(scopeProvider));
        }

        /// <inheritdoc />
        public IDisposable BeginScope<TState>(TState state)
        {
            return _scopeProvider.Push(state);
        }

        /// <inheritdoc />
        public bool IsEnabled(LogLevel logLevel)
        {
            return _runtime.IsEnabled(_categoryName, logLevel);
        }

        /// <inheritdoc />
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception exception,
            Func<TState, Exception, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }

            var logger = _runtime.CreateCategoryLogger(_categoryName);
            logger = EnrichWithEventId(logger, eventId);
            logger = EnrichWithState(logger, state);
            logger = EnrichWithScopes(logger);

            var message = formatter(state, exception) ?? string.Empty;
            logger.Write(MapLogLevel(logLevel), exception, "{Message:l}", message);
        }

        private static Serilog.ILogger EnrichWithEventId(Serilog.ILogger logger, EventId eventId)
        {
            if (eventId.Id != 0)
            {
                logger = logger.ForContext("EventId", eventId.Id);
            }

            if (!string.IsNullOrWhiteSpace(eventId.Name))
            {
                logger = logger.ForContext("EventName", eventId.Name);
            }

            return logger;
        }

        private static Serilog.ILogger EnrichWithState<TState>(Serilog.ILogger logger, TState state)
        {
            if (!(state is IEnumerable<KeyValuePair<string, object>> structuredState))
            {
                return logger;
            }

            foreach (var property in structuredState)
            {
                var propertyName = property.Key == OriginalFormatPropertyName
                    ? "OriginalFormat"
                    : property.Key;

                logger = logger.ForContext(propertyName, property.Value, destructureObjects: true);
            }

            return logger;
        }

        private Serilog.ILogger EnrichWithScopes(Serilog.ILogger logger)
        {
            var scopes = _scopeProvider.CaptureScopes();
            if (scopes.Count == 0)
            {
                return logger;
            }

            return logger.ForContext("Scopes", scopes, destructureObjects: true);
        }

        private static Serilog.Events.LogEventLevel MapLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Trace:
                case LogLevel.Debug:
                    return Serilog.Events.LogEventLevel.Debug;
                case LogLevel.Information:
                    return Serilog.Events.LogEventLevel.Information;
                case LogLevel.Warning:
                    return Serilog.Events.LogEventLevel.Warning;
                case LogLevel.Error:
                    return Serilog.Events.LogEventLevel.Error;
                case LogLevel.Critical:
                    return Serilog.Events.LogEventLevel.Fatal;
                default:
                    throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel,
                        "Unsupported Microsoft.Extensions.Logging log level.");
            }
        }

    }
}