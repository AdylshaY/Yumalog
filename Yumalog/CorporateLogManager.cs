namespace Yumalog
{
    using System;
    using Yumalog.Abstractions;
    using Yumalog.Configuration;
    using Yumalog.Implementation;

    /// <summary>
    /// Static manager for legacy applications without Dependency Injection.
    /// Provides global access to corporate logging.
    /// </summary>
    public static class CorporateLogManager
    {
        private static ICorporateLogger _instance;
        private static readonly object _lock = new object();
        private static bool _isInitialized = false;
        private static bool _processExitHandlerRegistered = false;

        /// <summary>
        /// Gets the current logger instance. Must call Initialize first.
        /// </summary>
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
        /// Initializes the corporate logging system with required application name.
        /// </summary>
        /// <param name="applicationName">The name of the application (required).</param>
        /// <param name="environment">Environment name. Auto-detected if not provided.</param>
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
        /// Initializes with custom configuration.
        /// </summary>
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
        /// Flushes remaining logs and shuts down the logging system.
        /// Call this before application exit.
        /// </summary>
        public static void Shutdown()
        {
            lock (_lock)
            {
                if (_isInitialized && _instance != null)
                {
                    _instance.FlushAndShutdown();
                    _instance = null;
                    _isInitialized = false;
                }
            }
        }

        /// <summary>
        /// Checks if the logging system has been initialized.
        /// </summary>
        public static bool IsInitialized => _isInitialized;

        /// <summary>
        /// Registers a handler for process exit to ensure logs are flushed even in crash scenarios.
        /// This provides a safety net when Shutdown() is not explicitly called.
        /// </summary>
        private static void RegisterProcessExitHandler()
        {
            if (_processExitHandlerRegistered)
            {
                return;
            }

            // AppDomain.ProcessExit is called when:
            // - Application closes normally
            // - Ctrl+C is pressed (console apps)
            // - Windows service stops
            // - Some types of unhandled exceptions
            AppDomain.CurrentDomain.ProcessExit += OnProcessExit;
            _processExitHandlerRegistered = true;
        }

        private static void OnProcessExit(object sender, EventArgs e)
        {
            // Last chance to flush logs before process terminates
            // This handler has a limited time window (~2-3 seconds on Windows)
            lock (_lock)
            {
                if (_isInitialized && _instance != null)
                {
                    try
                    {
                        _instance.FlushAndShutdown();
                    }
                    catch
                    {
                        // Suppress exceptions during process exit
                        // Cannot reliably log errors at this point without blocking
                    }
                }
            }
        }
    }
}
