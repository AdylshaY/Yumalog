namespace Yumalog.Implementation
{
    using System;
    using System.IO;
    using Serilog;
    using Serilog.Core;
    using Serilog.Formatting.Json;
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

            // The async wrapper keeps normal log writes off the file I/O path.
            // When BlockWhenFull is true, callers will wait instead of losing events during bursts.
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
        /// Creates the application-facing Yumalog wrapper around the configured Serilog instance.
        /// </summary>
        /// <param name="configuration">Validated runtime settings for file output and buffering.</param>
        /// <returns>A disposable Yumalog logger instance.</returns>
        public static SerilogCorporateLogger CreateCorporateLogger(CorporateLogConfiguration configuration)
        {
            var serilogLogger = CreateLogger(configuration);
            return new SerilogCorporateLogger(serilogLogger);
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
    }
}
