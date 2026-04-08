namespace Yumalog.Configuration
{
    using System;

    /// <summary>
    /// Configuration settings that control Yumalog file output and buffering behavior.
    /// </summary>
    public class CorporateLogConfiguration
    {
        private const string DefaultBaseLogDirectory = @"C:\ServiceLogs";
        private int _bufferSize = 50000;
        private int _retainedFileCountLimit = 31;
        private long? _fileSizeLimitBytes = 100 * 1024 * 1024;

        /// <summary>
        /// Logical application name used for directory creation and log enrichment.
        /// </summary>
        public string ApplicationName { get; set; }

        /// <summary>
        /// Environment name (for example Development, Staging, or Production).
        /// If omitted, <see cref="Validate"/> resolves it from environment variables.
        /// </summary>
        public string Environment { get; set; }

        /// <summary>
        /// Root directory used for local log files.
        /// Defaults to the Windows Service convention <c>C:\ServiceLogs</c>.
        /// Consumers can override it when a service must write to a different provisioned location.
        /// </summary>
        public string BaseLogDirectory { get; set; } = DefaultBaseLogDirectory;

        /// <summary>
        /// Full application-specific log directory under <see cref="BaseLogDirectory"/>.
        /// </summary>
        public string LogDirectory => System.IO.Path.Combine(BaseLogDirectory, ApplicationName);

        /// <summary>
        /// Reserved for future rolling policy customization.
        /// The current file sink implementation always rolls daily.
        /// </summary>
        public int RollingIntervalDays { get; set; } = 1;

        /// <summary>
        /// Maximum number of rolled log files to retain on disk.
        /// </summary>
        public int RetainedFileCountLimit
        {
            get => _retainedFileCountLimit;
            set
            {
                if (value < 1)
                    throw new ArgumentOutOfRangeException(nameof(RetainedFileCountLimit),
                        "Must retain at least 1 log file.");
                _retainedFileCountLimit = value;
            }
        }

        /// <summary>
        /// Maximum size of an individual log file before a size-based roll occurs.
        /// The default is 100 MB.
        /// </summary>
        public long? FileSizeLimitBytes
        {
            get => _fileSizeLimitBytes;
            set
            {
                if (value.HasValue && value.Value < 1024 * 1024)
                    throw new ArgumentOutOfRangeException(nameof(FileSizeLimitBytes),
                        "File size limit must be at least 1MB (1048576 bytes).");
                _fileSizeLimitBytes = value;
            }
        }

        /// <summary>
        /// In-memory queue size used by the asynchronous Serilog sink.
        /// Increase this value for services that emit large bursts of events.
        /// </summary>
        public int BufferSize
        {
            get => _bufferSize;
            set
            {
                if (value < 1000 || value > 500000)
                    throw new ArgumentOutOfRangeException(nameof(BufferSize),
                        "BufferSize must be between 1,000 and 500,000 messages.");
                _bufferSize = value;
            }
        }

        /// <summary>
        /// Controls what happens when the async queue is full.
        /// When true, the caller waits until there is capacity, favoring log durability over latency.
        /// When false, the sink may drop events instead of stalling the caller.
        /// </summary>
        public bool BlockWhenFull { get; set; } = true;

        /// <summary>
        /// Validates required configuration values and resolves missing defaults.
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ApplicationName))
            {
                throw new ArgumentException("ApplicationName is required and cannot be empty.", nameof(ApplicationName));
            }

            if (string.IsNullOrWhiteSpace(BaseLogDirectory))
            {
                throw new ArgumentException("BaseLogDirectory is required and cannot be empty.", nameof(BaseLogDirectory));
            }

            if (!System.IO.Path.IsPathRooted(BaseLogDirectory))
            {
                throw new ArgumentException("BaseLogDirectory must be an absolute path.", nameof(BaseLogDirectory));
            }

            // Validate the application name before it is used as part of a directory path.
            var invalidChars = System.IO.Path.GetInvalidFileNameChars();
            foreach (var c in ApplicationName)
            {
                if (Array.IndexOf(invalidChars, c) >= 0)
                {
                    throw new ArgumentException($"ApplicationName contains invalid character: {c}", nameof(ApplicationName));
                }
            }

            // Resolve the environment once so downstream components receive a stable value.
            if (string.IsNullOrWhiteSpace(Environment))
            {
                Environment = System.Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
                              ?? System.Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
                              ?? "Production";
            }
        }
    }
}
