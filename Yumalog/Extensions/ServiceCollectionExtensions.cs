namespace Yumalog.Extensions
{
    using System;
    using Microsoft.Extensions.Configuration;
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
        public static IServiceCollection AddYumalog(
            this IServiceCollection services, 
            string applicationName, 
            string environment = null)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (string.IsNullOrWhiteSpace(applicationName))
                throw new ArgumentException("Application name is required.", nameof(applicationName));

            var configuration = new YumalogConfiguration
            {
                ApplicationName = applicationName,
                Environment = environment
            };

            return AddYumalog(services, configuration);
        }

        /// <summary>
        /// Registers Yumalog using a pre-built configuration object.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The logging configuration.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <remarks>
        /// A single <see cref="SerilogYumalogLogger"/> instance is registered and then exposed through
        /// <see cref="IYumalogLogger"/>. This allows the DI container to own disposal so buffered log
        /// events are flushed during an orderly host shutdown.
        /// </remarks>
        public static IServiceCollection AddYumalog(
            this IServiceCollection services, 
            YumalogConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            // Register the shared runtime first so both legacy and ASP.NET Core integrations can reuse it.
            services.AddSingleton(provider => LoggerFactory.CreateRuntime(configuration));

            // Register the concrete wrapper so the container tracks its IDisposable lifetime.
            services.AddSingleton(provider => new SerilogYumalogLogger(provider.GetRequiredService<YumalogRuntime>()));

            // Expose the same singleton instance through the application-facing interface.
            services.AddSingleton<IYumalogLogger>(provider => provider.GetRequiredService<SerilogYumalogLogger>());

            return services;
        }

        /// <summary>
        /// Registers Yumalog using an inline configuration callback.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureOptions">Action to configure logging options.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddYumalog(
            this IServiceCollection services,
            Action<YumalogConfiguration> configureOptions)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            if (configureOptions == null)
                throw new ArgumentNullException(nameof(configureOptions));

            var configuration = new YumalogConfiguration();
            configureOptions(configuration);
            configuration.Validate();

            return AddYumalog(services, configuration);
        }

        /// <summary>
        /// Registers Yumalog using settings bound from the default <c>Yumalog</c> configuration section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The root configuration object.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddYumalog(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return AddYumalog(services, configuration, YumalogConfigurationBinder.DefaultSectionName);
        }

        /// <summary>
        /// Registers Yumalog using settings bound from a named configuration section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The root configuration object.</param>
        /// <param name="sectionName">The configuration section name.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddYumalog(
            this IServiceCollection services,
            IConfiguration configuration,
            string sectionName)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return AddYumalog(services, YumalogConfigurationBinder.BindSection(configuration, sectionName));
        }

        /// <summary>
        /// Registers Yumalog using a preselected configuration section.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="section">The configuration section containing Yumalog settings.</param>
        /// <returns>The service collection for chaining.</returns>
        public static IServiceCollection AddYumalog(
            this IServiceCollection services,
            IConfigurationSection section)
        {
            if (services == null)
                throw new ArgumentNullException(nameof(services));

            return AddYumalog(services, YumalogConfigurationBinder.Bind(section));
        }
    }
}
