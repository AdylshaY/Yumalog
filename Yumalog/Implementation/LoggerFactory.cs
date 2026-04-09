namespace Yumalog.Implementation
{
    using System;
    using System.IO;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;
    using Serilog.Formatting.Json;
    using Serilog.Sinks.Async;
    using Yumalog.Configuration;

    /// <summary>
    /// Builds the internal Serilog pipeline used by Yumalog.
    /// </summary>
    internal static class LoggerFactory
    {
        private const string WriteProbeFileName = ".yumalog-write-test";

        /// <summary>
        /// Creates and configures the underlying Serilog logger instance.
        /// </summary>
        /// <param name="configuration">Validated runtime settings for file output and buffering.</param>
        /// <returns>A configured Serilog logger that writes JSON files to the application log directory.</returns>
        public static Logger CreateLogger(CorporateLogConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            EnsureLogDirectoryReady(configuration.LogDirectory);

            var logFilePath = Path.Combine(configuration.LogDirectory, "log-.json");
            var monitor = CreateAsyncBufferDiagnosticMonitor(configuration);

            // The async wrapper keeps normal log writes off the file I/O path.
            // Rolling is fixed to daily; validation rejects unsupported RollingIntervalDays values.
            // When BlockWhenFull is true, callers will wait instead of losing events during bursts.
            var logger = new LoggerConfiguration()
                .MinimumLevel.Is(MapLogLevel(GetSinkMinimumLogLevel(configuration)))
                .Enrich.WithProperty("Application", configuration.ApplicationName)
                .Enrich.WithProperty("Environment", configuration.Environment)
                .Enrich.WithProperty("MachineName", Environment.MachineName)
                .Enrich.WithProperty("ProcessId", System.Diagnostics.Process.GetCurrentProcess().Id)
                .WriteTo.Async(a => a.File(
                    formatter: new JsonFormatter(renderMessage: true),
                    path: logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: configuration.RetainedFileCountLimit,
                    fileSizeLimitBytes: configuration.FileSizeLimitBytes,
                    shared: false, // Prevent file locking issues
                    rollOnFileSizeLimit: true
                ), bufferSize: configuration.BufferSize, blockWhenFull: configuration.BlockWhenFull, monitor: monitor)
                .CreateLogger();

            return logger;
        }

        /// <summary>
        /// Creates the shared runtime object used by both the legacy Yumalog API and the
        /// Microsoft.Extensions.Logging provider integration.
        /// </summary>
        /// <param name="configuration">Validated runtime settings for file output and buffering.</param>
        /// <returns>A disposable runtime wrapper around the configured Serilog logger.</returns>
        public static CorporateLogRuntime CreateRuntime(CorporateLogConfiguration configuration)
        {
            var serilogLogger = CreateLogger(configuration);
            return new CorporateLogRuntime(
                serilogLogger,
                configuration.ApplicationName,
                configuration.LogDirectory,
                configuration.MinimumLogLevel,
                configuration.CategoryMinimumLogLevels,
                configuration.DiagnosticListener);
        }

        /// <summary>
        /// Creates the application-facing Yumalog wrapper around the configured Serilog instance.
        /// </summary>
        /// <param name="configuration">Validated runtime settings for file output and buffering.</param>
        /// <returns>A disposable Yumalog logger instance.</returns>
        public static SerilogCorporateLogger CreateCorporateLogger(CorporateLogConfiguration configuration)
        {
            return new SerilogCorporateLogger(CreateRuntime(configuration));
        }

        /// <summary>
        /// Ensures the target log directory exists and is writable before the service starts logging.
        /// </summary>
        /// <param name="logDirectory">Application-specific directory used by the file sink.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the directory cannot be created or written to.
        /// </exception>
        private static void EnsureLogDirectoryReady(string logDirectory)
        {
            try
            {
                Directory.CreateDirectory(logDirectory);

                var probePath = Path.Combine(logDirectory, WriteProbeFileName);
                File.WriteAllText(probePath, string.Empty);
                File.Delete(probePath);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is NotSupportedException)
            {
                throw new InvalidOperationException(
                    $"The configured log directory '{logDirectory}' could not be created or written to.",
                    ex);
            }
        }

        private static IAsyncLogEventSinkMonitor CreateAsyncBufferDiagnosticMonitor(CorporateLogConfiguration configuration)
        {
            if (configuration.DiagnosticListener == null)
            {
                return null;
            }

            return new AsyncBufferDiagnosticMonitor(
                configuration.ApplicationName,
                configuration.LogDirectory,
                configuration.DiagnosticListener,
                configuration.AsyncBufferMonitorInterval,
                configuration.AsyncBufferWarningUsageThresholdPercentage);
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

        private static LogLevel GetSinkMinimumLogLevel(CorporateLogConfiguration configuration)
        {
            var minimumLogLevel = configuration.MinimumLogLevel;

            foreach (var rule in configuration.CategoryMinimumLogLevels)
            {
                if (rule.Value < minimumLogLevel)
                {
                    minimumLogLevel = rule.Value;
                }
            }

            return minimumLogLevel;
        }
    }
}
