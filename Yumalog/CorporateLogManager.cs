namespace Yumalog
{
    using System;
    using Yumalog.Abstractions;
    using Yumalog.Configuration;
    using Yumalog.Implementation;

    /// <summary>
    /// Static entry point for legacy applications that do not use Dependency Injection.
    /// </summary>
    /// <remarks>
    /// This type exists as a compatibility path for older services. New Windows Service applications
    /// should prefer the Dependency Injection registration extensions so the host can own logger
    /// lifetime and shutdown flushing automatically.
    /// </remarks>
    public static class CorporateLogManager
    {
        private static ICorporateLogger _instance;
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;
        private static bool _processExitHandlerRegistered = false;

        /// <summary>
        /// Gets the currently initialized logger instance.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the manager has not been initialized.
        /// </exception>
        public static ICorporateLogger Current
        {
            get
            {
                if (!_isInitialized)
                {
                    throw new InvalidOperationException(
                        "CorporateLogManager has not been initialized. Call Initialize() first.");
                }
                return _instance;
            }
        }

        /// <summary>
        /// Initializes corporate logging using the minimum required settings.
        /// </summary>
        /// <param name="applicationName">The name of the application (required).</param>
        /// <param name="environment">Environment name. Auto-detected during validation when omitted.</param>
        public static void Initialize(string applicationName, string environment = null)
        {
            if (string.IsNullOrWhiteSpace(applicationName))
            {
                throw new ArgumentException("Application name is required.", nameof(applicationName));
            }

            lock (_lock)
            {
                if (_isInitialized)
                {
                    throw new InvalidOperationException(
                        "CorporateLogManager has already been initialized. Call Shutdown() first to re-initialize.");
                }

                var configuration = new CorporateLogConfiguration
                {
                    ApplicationName = applicationName,
                    Environment = environment
                };

                _instance = LoggerFactory.CreateCorporateLogger(configuration);
                _isInitialized = true;

                // Register process exit handler for crash scenarios
                RegisterProcessExitHandler();
            }
        }

        /// <summary>
        /// Initializes corporate logging using a pre-built configuration object.
        /// </summary>
        /// <param name="configuration">Configuration values used to create the underlying logger.</param>
        public static void Initialize(CorporateLogConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            lock (_lock)
            {
                if (_isInitialized)
                {
                    throw new InvalidOperationException(
                        "CorporateLogManager has already been initialized. Call Shutdown() first to re-initialize.");
                }

                _instance = LoggerFactory.CreateCorporateLogger(configuration);
                _isInitialized = true;

                // Register process exit handler for crash scenarios
                RegisterProcessExitHandler();
            }
        }

        /// <summary>
        /// Flushes queued log events and releases logger resources.
        /// </summary>
        /// <remarks>
        /// Legacy applications should call this during an orderly shutdown. A process-exit handler is
        /// also registered as a best-effort safety net, but explicit shutdown is still the stronger path.
        /// </remarks>
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_isInitialized && _instance != null)
                {
                    DisposeCurrentLogger();
                    _instance = null;
                    _isInitialized = false;
                }
            }
        }

        /// <summary>
        /// Gets whether the static manager has been initialized.
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Registers a process-exit handler as a best-effort fallback for legacy applications.
        /// </summary>
        private static void RegisterProcessExitHandler()
        {
            if (_processExitHandlerRegistered)
            {
                return;
            }

            // AppDomain.ProcessExit is the last fallback for orderly process teardown paths.
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _processExitHandlerRegistered = true;
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            // This callback has a limited time budget, so exceptions are suppressed deliberately.
            lock (_lock)
            {
                if (_isInitialized && _instance != null)
                {
                    try
                    {
                        DisposeCurrentLogger();
                    }
                    catch
                    {
                        // Suppress exceptions during process exit
                        // Cannot reliably log errors at this point without blocking
                    }
                }
            }
        }

        private static void DisposeCurrentLogger()
        {
            // The logger implementation owns async sink flushing through IDisposable.
            if (_instance is IDisposable disposableLogger)
            {
                disposableLogger.Dispose();
            }
        }
    }
}
