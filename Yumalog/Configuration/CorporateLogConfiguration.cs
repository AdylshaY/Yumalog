namespace Yumalog.Configuration
{
    using System;

    /// <summary>
    /// Configuration settings for corporate logging infrastructure.
    /// </summary>
    public class CorporateLogConfiguration
    {
        /// <summary>
        /// Application name - used for directory creation and labeling. Required.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Environment name (e.g., Development, Staging, Production). Auto-detected if not provided.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// Base directory for log files. Fixed to corporate standard.
        /// </summary>
        public string BaseLogDirectory => @"C:\CorporateLogs";

        /// <summary>
        /// Full log directory path including application name.
        /// </summary>
        public string LogDirectory => System.IO.Path.Combine(BaseLogDirectory, ApplicationName);

        /// <summary>
        /// Rolling interval in days. Default is 1 (daily rolling).
        /// </summary>
        public int RollingIntervalDays { get; set; } = 1;

        /// <summary>
        /// Maximum number of log files to retain. Default is 31.
        /// </summary>
        public int RetainedFileCountLimit { get; set; } = 31;

        /// <summary>
        /// File size limit in bytes. Default is 100MB.
        /// </summary>
        public long? FileSizeLimitBytes { get; set; } = 100 * 1024 * 1024;

        /// <summary>
        /// Async buffer size for in-memory queue. Default is 50000 messages.
        /// Increase for high-volume applications to handle burst traffic.
        /// </summary>
        public int BufferSize { get; set; } = 50000;

        /// <summary>
        /// Block application thread when buffer is full to prevent log loss.
        /// Default is true for zero-data-loss guarantee.
        /// Set to false only if application performance is more critical than log completeness.
        /// </summary>
        public bool BlockWhenFull { get; set; } = true;

        /// <summary>
        /// Validates the configuration.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ApplicationName))
            {
                throw new ArgumentException("ApplicationName is required and cannot be empty.", nameof(ApplicationName));
            }

            // Validate application name doesn't contain invalid path characters
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (var c in ApplicationName)
            {
                if (Array.IndexOf(invalidChars, c) >= 0)
                {
                    throw new ArgumentException($"ApplicationName contains invalid character: {c}", nameof(ApplicationName));
                }
            }

            // Auto-detect environment if not provided
            if (string.IsNullOrWhiteSpace(Environment))
            {
                Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") 
                              ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                              ?? "Production";
            }
        }
    }
}
