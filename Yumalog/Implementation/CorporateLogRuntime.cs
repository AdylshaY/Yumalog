namespace Yumalog.Implementation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.Extensions.Logging;
    using Serilog;
    using Serilog.Core;
    using Yumalog.Diagnostics;

    /// <summary>
    /// Owns the shared Serilog pipeline and the lifecycle diagnostics around logger shutdown.
    /// </summary>
    internal sealed class CorporateLogRuntime : IDisposable
    {
        private readonly Logger _logger;
        private readonly string _applicationName;
        private readonly string _logDirectory;
        private readonly Action<CorporateLogDiagnosticEvent> _diagnosticListener;
        private readonly IReadOnlyList<CategoryLevelRule> _categoryMinimumLogLevels;
        private bool _disposed;

        /// <summary>
        /// Initializes a new runtime instance around the configured Serilog logger.
        /// </summary>
        public CorporateLogRuntime(
            Logger logger,
            string applicationName,
            string logDirectory,
            LogLevel minimumLogLevel,
            IDictionary<string, LogLevel> categoryMinimumLogLevels,
            Action<CorporateLogDiagnosticEvent> diagnosticListener = null)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            _diagnosticListener = diagnosticListener;
            MinimumLogLevel = minimumLogLevel;
            _categoryMinimumLogLevels = CreateCategoryRules(categoryMinimumLogLevels);
        }

        /// <summary>
        /// Gets the shared root Serilog logger.
        /// </summary>
        public Logger BaseLogger
        {
            get
            {
                ThrowIfDisposed();
                return _logger;
            }
        }

        /// <summary>
        /// Gets the minimum Microsoft logging level accepted by the runtime.
        /// </summary>
        public LogLevel MinimumLogLevel { get; }

        /// <summary>
        /// Determines whether a log event should be written for the supplied category and level.
        /// </summary>
        /// <param name="categoryName">The Microsoft logging category. Null for direct Yumalog calls.</param>
        /// <param name="logLevel">The candidate log level.</param>
        /// <returns><c>true</c> when the event should be accepted; otherwise <c>false</c>.</returns>
        public bool IsEnabled(string categoryName, LogLevel logLevel)
        {
            if (logLevel == LogLevel.None)
            {
                return false;
            }

            return logLevel >= ResolveMinimumLogLevel(categoryName);
        }

        /// <summary>
        /// Creates a category-enriched Serilog logger for Microsoft logging integration.
        /// </summary>
        /// <param name="categoryName">The Microsoft logger category name.</param>
        /// <returns>A Serilog logger enriched with the category when available.</returns>
        public Serilog.ILogger CreateCategoryLogger(string categoryName)
        {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(categoryName))
            {
                return _logger;
            }

            return _logger.ForContext(Constants.SourceContextPropertyName, categoryName);
        }

        /// <summary>
        /// Throws when the runtime has already been disposed.
        /// </summary>
        public void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(CorporateLogRuntime));
            }
        }

        /// <summary>
        /// Flushes pending events and releases the underlying Serilog resources.
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            EmitDiagnostic(CorporateLogDiagnosticEventType.ShutdownStarted,
                "Logger shutdown started. Buffered events are being flushed.");

            try
            {
                _logger.Dispose();
                _disposed = true;

                EmitDiagnostic(CorporateLogDiagnosticEventType.ShutdownCompleted,
                    "Logger shutdown completed successfully.");
            }
            catch (Exception ex)
            {
                EmitDiagnostic(CorporateLogDiagnosticEventType.ShutdownFailed,
                    "Logger shutdown failed before all buffered events could be flushed.",
                    ex);
                throw;
            }
        }

        private void EmitDiagnostic(
            CorporateLogDiagnosticEventType eventType,
            string message,
            Exception exception = null)
        {
            var listener = _diagnosticListener;
            if (listener == null)
            {
                return;
            }

            listener(new CorporateLogDiagnosticEvent(
                eventType,
                _applicationName,
                _logDirectory,
                message,
                exception));
        }

        private LogLevel ResolveMinimumLogLevel(string categoryName)
        {
            if (string.IsNullOrWhiteSpace(categoryName) || _categoryMinimumLogLevels.Count == 0)
            {
                return MinimumLogLevel;
            }

            foreach (var rule in _categoryMinimumLogLevels)
            {
                if (MatchesCategory(rule.CategoryName, categoryName))
                {
                    return rule.MinimumLogLevel;
                }
            }

            return MinimumLogLevel;
        }

        private static bool MatchesCategory(string ruleCategory, string categoryName)
        {
            return categoryName.Equals(ruleCategory, StringComparison.OrdinalIgnoreCase)
                || categoryName.StartsWith(ruleCategory + ".", StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyList<CategoryLevelRule> CreateCategoryRules(IDictionary<string, LogLevel> categoryMinimumLogLevels)
        {
            if (categoryMinimumLogLevels == null || categoryMinimumLogLevels.Count == 0)
            {
                return Array.Empty<CategoryLevelRule>();
            }

            return categoryMinimumLogLevels
                .Select(rule => new CategoryLevelRule(NormalizeCategoryName(rule.Key), rule.Value))
                .OrderByDescending(rule => rule.CategoryName.Length)
                .ToArray();
        }

        private static string NormalizeCategoryName(string categoryName)
        {
            var normalizedCategoryName = categoryName.Trim();

            if (normalizedCategoryName.EndsWith(".*", StringComparison.Ordinal))
            {
                normalizedCategoryName = normalizedCategoryName.Substring(0, normalizedCategoryName.Length - 2);
            }

            return normalizedCategoryName.TrimEnd('.');
        }

        private sealed class CategoryLevelRule
        {
            public CategoryLevelRule(string categoryName, LogLevel minimumLogLevel)
            {
                CategoryName = categoryName;
                MinimumLogLevel = minimumLogLevel;
            }

            public string CategoryName { get; }

            public LogLevel MinimumLogLevel { get; }
        }
    }
}