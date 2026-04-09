namespace Yumalog.Extensions
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Yumalog.Configuration;
    using Yumalog.Implementation;

    /// <summary>
    /// ASP.NET Core logging builder extensions for using Yumalog as an <see cref="ILogger"/> backend.
    /// </summary>
    public static class LoggingBuilderExtensions
    {
        /// <summary>
        /// Registers Yumalog as a Microsoft logging provider using the minimum required settings.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="applicationName">The name of the application (required).</param>
        /// <param name="environment">Environment name. Auto-detected during validation when omitted.</param>
        /// <returns>The logging builder for chaining.</returns>
        public static ILoggingBuilder AddYumalog(
            this ILoggingBuilder builder,
            string applicationName,
            string environment = null)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (string.IsNullOrWhiteSpace(applicationName))
                throw new ArgumentException("Application name is required.", nameof(applicationName));

            return AddYumalog(builder, new YumalogConfiguration
            {
                ApplicationName = applicationName,
                Environment = environment
            });
        }

        /// <summary>
        /// Registers Yumalog as a Microsoft logging provider using a pre-built configuration object.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="configuration">The logging configuration.</param>
        /// <returns>The logging builder for chaining.</returns>
        /// <remarks>
        /// This registration keeps the standard <see cref="ILogger{TCategoryName}"/> application model intact
        /// while routing accepted log events through Yumalog's file-based Serilog pipeline.
        /// </remarks>
        public static ILoggingBuilder AddYumalog(
            this ILoggingBuilder builder,
            YumalogConfiguration configuration)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            configuration.Validate();

            ServiceCollectionExtensions.AddYumalog(builder.Services, configuration);
            builder.Services.AddSingleton<YumalogLoggerProvider>(
                provider => new YumalogLoggerProvider(provider.GetRequiredService<YumalogRuntime>()));
            builder.Services.AddSingleton<ILoggerProvider>(
                provider => provider.GetRequiredService<YumalogLoggerProvider>());

            return builder;
        }

        /// <summary>
        /// Registers Yumalog as a Microsoft logging provider using an inline configuration callback.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="configureOptions">Action to configure logging options.</param>
        /// <returns>The logging builder for chaining.</returns>
        public static ILoggingBuilder AddYumalog(
            this ILoggingBuilder builder,
            Action<YumalogConfiguration> configureOptions)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            if (configureOptions == null)
                throw new ArgumentNullException(nameof(configureOptions));

            var configuration = new YumalogConfiguration();
            configureOptions(configuration);
            configuration.Validate();

            return AddYumalog(builder, configuration);
        }

        /// <summary>
        /// Registers Yumalog as a Microsoft logging provider using the default <c>Yumalog</c> configuration section.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="configuration">The root configuration object.</param>
        /// <returns>The logging builder for chaining.</returns>
        public static ILoggingBuilder AddYumalog(
            this ILoggingBuilder builder,
            IConfiguration configuration)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            return AddYumalog(builder, configuration, YumalogConfigurationBinder.DefaultSectionName);
        }

        /// <summary>
        /// Registers Yumalog as a Microsoft logging provider using a named configuration section.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="configuration">The root configuration object.</param>
        /// <param name="sectionName">The configuration section name.</param>
        /// <returns>The logging builder for chaining.</returns>
        public static ILoggingBuilder AddYumalog(
            this ILoggingBuilder builder,
            IConfiguration configuration,
            string sectionName)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            return AddYumalog(builder, YumalogConfigurationBinder.BindSection(configuration, sectionName));
        }

        /// <summary>
        /// Registers Yumalog as a Microsoft logging provider using a preselected configuration section.
        /// </summary>
        /// <param name="builder">The logging builder.</param>
        /// <param name="section">The configuration section containing Yumalog settings.</param>
        /// <returns>The logging builder for chaining.</returns>
        public static ILoggingBuilder AddYumalog(
            this ILoggingBuilder builder,
            IConfigurationSection section)
        {
            if (builder == null)
                throw new ArgumentNullException(nameof(builder));

            return AddYumalog(builder, YumalogConfigurationBinder.Bind(section));
        }
    }
}