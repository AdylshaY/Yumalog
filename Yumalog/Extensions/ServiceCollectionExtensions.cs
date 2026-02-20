namespace Yumalog.Extensions
{
    using System;
    using Microsoft.Extensions.DependencyInjection;
    using Yumalog.Abstractions;
    using Yumalog.Configuration;
    using Yumalog.Implementation;

    /// <summary>
    /// Dependency Injection extensions for modern .NET applications.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds corporate logging to the service collection.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="applicationName">The name of the application (required).</param>
        /// <param name="environment">Environment name. Auto-detected if not provided.</param>
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
        /// Adds corporate logging to the service collection with custom configuration.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The logging configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddCorporateLogging(
            this IServiceCollection services, 
            CorporateLogConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            // Register as singleton
            services.AddSingleton<ICorporateLogger>(provider =>
            {
                return LoggerFactory.CreateCorporateLogger(configuration);
            });

            return services;
        }

        /// <summary>
        /// Adds corporate logging with configuration builder.
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
