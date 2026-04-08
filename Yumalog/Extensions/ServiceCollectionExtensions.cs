namespace Yumalog.Extensions
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Yumalog.Abstractions;
    using Yumalog.Configuration;
    using Yumalog.Implementation;

    /// <summary>
    /// Dependency Injection extensions for Windows Service applications that consume Yumalog.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers Yumalog using the minimum required settings.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="applicationName">The name of the application (required).</param>
        /// <param name="environment">Environment name. Auto-detected during validation when omitted.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCorporateLogging(
            this IServiceCollection services, 
            string applicationName, 
            string environment = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (string.IsNullOrWhiteSpace(applicationName))
                throw new ArgumentException("Application name is required.", nameof(applicationName));

            var configuration = new CorporateLogConfiguration
            {
                ApplicationName = applicationName,
                Environment = environment
            };

            return AddCorporateLogging(services, configuration);
        }

        /// <summary>
        /// Registers Yumalog using a pre-built configuration object.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The logging configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// A single <see cref="SerilogCorporateLogger"/> instance is registered and then exposed through
        /// <see cref="ICorporateLogger"/>. This allows the DI container to own disposal so buffered log
        /// events are flushed during an orderly host shutdown.
        /// </remarks>
        public static IServiceCollection AddCorporateLogging(
            this IServiceCollection services, 
            CorporateLogConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            // Register the concrete singleton first so the container tracks its IDisposable lifetime.
            services.AddSingleton(provider => LoggerFactory.CreateCorporateLogger(configuration));

            // Expose the same singleton instance through the application-facing interface.
            services.AddSingleton<ICorporateLogger>(provider => provider.GetRequiredService<SerilogCorporateLogger>());

            return services;
        }

        /// <summary>
        /// Registers Yumalog using an inline configuration callback.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure logging options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCorporateLogging(
            this IServiceCollection services,
            Action<CorporateLogConfiguration> configureOptions)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configureOptions == null)
                throw new ArgumentNullException(nameof(configureOptions));

            var configuration = new CorporateLogConfiguration();
            configureOptions(configuration);
            configuration.Validate();

            return AddCorporateLogging(services, configuration);
        }
    }
}
