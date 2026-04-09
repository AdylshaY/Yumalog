namespace Yumalog.Extensions
{
    using System;
    using Microsoft.Extensions.Configuration;
    using Yumalog.Configuration;

    /// <summary>
    /// Creates validated <see cref="CorporateLogConfiguration"/> instances from configuration sources.
    /// </summary>
    internal static class CorporateLogConfigurationBinder
    {
        /// <summary>
        /// Default section name used for Yumalog settings in configuration files.
        /// </summary>
        internal const string DefaultSectionName = "Yumalog";

        /// <summary>
        /// Binds a configuration section into a Yumalog configuration object.
        /// </summary>
        /// <param name="section">The configuration section containing Yumalog settings.</param>
        /// <returns>A validated Yumalog configuration instance.</returns>
        internal static CorporateLogConfiguration Bind(IConfiguration section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            var configuration = new CorporateLogConfiguration();
            section.Bind(configuration);
            configuration.Validate();

            return configuration;
        }

        /// <summary>
        /// Resolves and binds a named Yumalog section from a root configuration object.
        /// </summary>
        /// <param name="configuration">The root configuration object.</param>
        /// <param name="sectionName">The section name to bind.</param>
        /// <returns>A validated Yumalog configuration instance.</returns>
        internal static CorporateLogConfiguration BindSection(IConfiguration configuration, string sectionName)
        {
            if (configuration == null)
                throw new ArgumentNullException(nameof(configuration));

            if (string.IsNullOrWhiteSpace(sectionName))
                throw new ArgumentException("Section name is required.", nameof(sectionName));

            return Bind(configuration.GetSection(sectionName));
        }
    }
}