namespace Yumalog.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using FluentAssertions;
    using Microsoft.Extensions.DependencyInjection;
    using Xunit;
    using Yumalog.Abstractions;
    using Yumalog.Configuration;
    using Yumalog.Extensions;

    public class ServiceCollectionExtensionsTests : IDisposable
    {
        private readonly string _testAppName;
        private readonly string _testLogDirectory;
        private readonly string _testBaseDirectory;

        public ServiceCollectionExtensionsTests()
        {
            _testAppName = $"DiTestApp_{Guid.NewGuid():N}";
            _testBaseDirectory = Path.Combine(Path.GetTempPath(), $"YumalogTests_{Guid.NewGuid():N}");
            _testLogDirectory = Path.Combine(_testBaseDirectory, _testAppName);
        }

        public void Dispose()
        {
            if (Directory.Exists(_testBaseDirectory))
            {
                try
                {
                    Directory.Delete(_testBaseDirectory, recursive: true);
                }
                catch
                {
                }
            }
        }

        [Fact]
        public void AddCorporateLogging_WhenProviderIsDisposed_ShouldFlushBufferedLogs()
        {
            var services = new ServiceCollection();
            services.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory,
                BufferSize = 1000
            });

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ICorporateLogger>();
            var marker = $"DI_FLUSH_{Guid.NewGuid():N}";

            for (var index = 0; index < 500; index++)
            {
                logger.LogInformation($"{marker}_{index}");
            }

            provider.Dispose();

            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain($"{marker}_0");
            logContent.Should().Contain($"{marker}_499");
        }

        [Fact]
        public void AddCorporateLogging_WhenProviderIsDisposed_ShouldDisposeLoggerInstance()
        {
            var services = new ServiceCollection();
            services.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory
            });

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ICorporateLogger>();

            provider.Dispose();

            Action act = () => logger.LogInformation("Should fail after provider disposal");
            act.Should().Throw<ObjectDisposedException>();
        }

        [Fact]
        public void AddCorporateLogging_WithCustomBaseDirectory_ShouldWriteLogsToThatDirectory()
        {
            var services = new ServiceCollection();
            services.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory
            });

            using var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ICorporateLogger>();
            logger.LogInformation("Custom path test");

            provider.Dispose();

            Directory.Exists(_testLogDirectory).Should().BeTrue();
            Directory.GetFiles(_testLogDirectory, "log-*.json").Should().NotBeEmpty();
        }

        [Fact]
        public void AddCorporateLogging_WhenBaseDirectoryCannotBeWritten_ShouldFailFast()
        {
            var invalidBasePath = Path.Combine(_testBaseDirectory, "blocked-root.txt");
            Directory.CreateDirectory(_testBaseDirectory);
            File.WriteAllText(invalidBasePath, "blocking file");

            var services = new ServiceCollection();
            services.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = invalidBasePath
            });

            using var provider = services.BuildServiceProvider();

            Action act = () => provider.GetRequiredService<ICorporateLogger>();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*could not be created or written to*");
        }

        private string GetLatestLogFileContent()
        {
            const int maxWaitMs = 3000;
            const int checkIntervalMs = 100;

            var elapsed = 0;
            while (!Directory.Exists(_testLogDirectory) && elapsed < maxWaitMs)
            {
                System.Threading.Thread.Sleep(checkIntervalMs);
                elapsed += checkIntervalMs;
            }

            if (!Directory.Exists(_testLogDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Log directory not found after {maxWaitMs}ms: {_testLogDirectory}");
            }

            elapsed = 0;
            string[] logFiles = null;
            while (elapsed < maxWaitMs)
            {
                logFiles = Directory.GetFiles(_testLogDirectory, "log-*.json");
                if (logFiles.Length > 0)
                {
                    break;
                }

                System.Threading.Thread.Sleep(checkIntervalMs);
                elapsed += checkIntervalMs;
            }

            if (logFiles == null || logFiles.Length == 0)
            {
                throw new FileNotFoundException(
                    $"No log files found in {_testLogDirectory} after {maxWaitMs}ms");
            }

            var latestLogFile = logFiles.OrderByDescending(File.GetLastWriteTimeUtc).First();
            return File.ReadAllText(latestLogFile);
        }
    }
}