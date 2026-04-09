namespace Yumalog.Implementation
{
    using System;
    using Microsoft.Extensions.Logging;

    /// <summary>
    /// Bridges ASP.NET Core logging into the shared Yumalog runtime.
    /// </summary>
    internal sealed class YumalogLoggerProvider : ILoggerProvider
    {
        private readonly YumalogRuntime _runtime;
        private readonly YumalogScopeProvider _scopeProvider = new YumalogScopeProvider();

        /// <summary>
        /// Initializes a new provider instance.
        /// </summary>
        /// <param name="runtime">Shared Yumalog runtime used for all created loggers.</param>
        public YumalogLoggerProvider(YumalogRuntime runtime)
        {
            _runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
        }

        /// <inheritdoc />
        public ILogger CreateLogger(string categoryName)
        {
            return new YumalogMicrosoftLogger(categoryName, _runtime, _scopeProvider);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _runtime.Dispose();
        }
    }
}