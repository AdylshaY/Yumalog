namespace Yumalog.Implementation
{
    using System;
    using System.IO;
    using Serilog;
    using Serilog.Core;
    using Serilog.Formatting.Json;
    using Yumalog.Abstractions;
    using Yumalog.Configuration;

    /// <summary>
    /// Factory class for creating configured Serilog loggers.
    /// </summary>
    internal static class LoggerFactory
    {
        /// <summary>
        /// Creates a Serilog logger with corporate standards.
        /// </summary>
        public static Logger CreateLogger(CorporateLogConfiguration configuration)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            // Ensure log directory exists
            if (!Directory.Exists(configuration.LogDirectory))
            {
                Directory.CreateDirectory(configuration.LogDirectory);
            }

            var logFilePath = Path.Combine(configuration.LogDirectory, "log-.json");

            // Configure Serilog with async sink for performance and zero-data-loss
            // bufferSize: 50000 - Handles burst traffic up to 50k messages
            // blockWhenFull: true - Prevents log loss in extreme scenarios (may slow down app if buffer fills)
            var logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
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
                ), bufferSize: configuration.BufferSize, blockWhenFull: configuration.BlockWhenFull)
                .CreateLogger();

            return logger;
        }

        /// <summary>
        /// Creates a corporate logger wrapper around Serilog.
        /// </summary>
        public static ICorporateLogger CreateCorporateLogger(CorporateLogConfiguration configuration)
        {
            var serilogLogger = CreateLogger(configuration);
            return new SerilogCorporateLogger(serilogLogger);
        }
    }
}
