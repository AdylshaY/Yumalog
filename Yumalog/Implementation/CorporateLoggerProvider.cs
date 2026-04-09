namespace Yumalog.Implementation
{
    using System;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Bridges ASP.NET Core logging into the shared Yumalog runtime.
    /// </summary>
    internal sealed class CorporateLoggerProvider : ILoggerProvider
    {
        private readonly CorporateLogRuntime _runtime;
        private readonly CorporateScopeProvider _scopeProvider = new CorporateScopeProvider();

        /// <summary>
        /// Initializes a new provider instance.
        /// </summary>
        /// <param name="runtime">Shared Yumalog runtime used for all created loggers.</param>
        public CorporateLoggerProvider(CorporateLogRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName)
        {
            return new CorporateMicrosoftLogger(categoryName, _runtime, _scopeProvider);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _runtime.Dispose();
        }
    }
}