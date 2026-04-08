namespace Yumalog.Configuration
{
    using System;
    using Yumalog.Diagnostics;

    /// <summary>
    /// Configuration settings that control Yumalog file output and buffering behavior.
    /// </summary>
    public class CorporateLogConfiguration
    {
        private const string DefaultBaseLogDirectory = @"C:\ServiceLogs";
        private TimeSpan _asyncBufferMonitorInterval = TimeSpan.FromSeconds(1);
        private int _bufferSize = 50000;
        private int _retainedFileCountLimit = 31;
        private int _asyncBufferWarningUsageThresholdPercentage = 80;
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
        /// Rolling interval expressed in days.
        /// </summary>
        /// <remarks>
        /// Yumalog currently supports only daily rolling for Windows Service deployments.
        /// Set this value to <c>1</c>.
        /// </remarks>
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
        /// Interval used to sample async buffer health metrics when diagnostics are enabled.
        /// </summary>
        public TimeSpan AsyncBufferMonitorInterval
        {
            get => _asyncBufferMonitorInterval;
            set
            {
                if (value <= TimeSpan.Zero)
                    throw new ArgumentOutOfRangeException(nameof(AsyncBufferMonitorInterval),
                        "AsyncBufferMonitorInterval must be greater than zero.");
                _asyncBufferMonitorInterval = value;
            }
        }

        /// <summary>
        /// Buffer usage percentage that triggers a high-usage diagnostic event.
        /// </summary>
        public int AsyncBufferWarningUsageThresholdPercentage
        {
            get => _asyncBufferWarningUsageThresholdPercentage;
            set
            {
                if (value < 1 || value > 100)
                    throw new ArgumentOutOfRangeException(nameof(AsyncBufferWarningUsageThresholdPercentage),
                        "AsyncBufferWarningUsageThresholdPercentage must be between 1 and 100.");
                _asyncBufferWarningUsageThresholdPercentage = value;
            }
        }

        /// <summary>
        /// Optional callback invoked for lifecycle diagnostics emitted by Yumalog.
        /// </summary>
        /// <remarks>
        /// Use this hook to observe shutdown start, completion, or failure during rollout and operations.
        /// The callback should be lightweight and non-throwing. When diagnostics are enabled, Yumalog also
        /// emits async buffer monitoring events for queue pressure and dropped-message visibility.
        /// </remarks>
        public Action<CorporateLogDiagnosticEvent> DiagnosticListener { get; set; }

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

            if (RollingIntervalDays != 1)
            {
                throw new ArgumentOutOfRangeException(nameof(RollingIntervalDays),
                    "Yumalog currently supports only daily rolling. Set RollingIntervalDays to 1.");
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
