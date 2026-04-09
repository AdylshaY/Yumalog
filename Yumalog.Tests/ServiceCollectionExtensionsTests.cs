namespace Yumalog.Tests
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Xunit;
    using Yumalog.Abstractions;
    using Yumalog.Configuration;
    using Yumalog.Diagnostics;
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

        [Fact]
        public void AddCorporateLogging_WithConcurrentWritersAndProviderDispose_ShouldFlushAllAcceptedLogs()
        {
            const int writerCount = 8;
            const int messagesPerWriter = 250;

            var services = new ServiceCollection();
            services.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory,
                BufferSize = 1000
            });

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ICorporateLogger>();
            var marker = $"CONCURRENT_FLUSH_{Guid.NewGuid():N}";

            var writerTasks = Enumerable.Range(0, writerCount)
                .Select(writerIndex => Task.Run(() =>
                {
                    for (var messageIndex = 0; messageIndex < messagesPerWriter; messageIndex++)
                    {
                        logger.LogInformation($"{marker}_{writerIndex}_{messageIndex}");
                    }
                }))
                .ToArray();

            Task.WaitAll(writerTasks);
            provider.Dispose();

            var logContent = GetLatestLogFileContent();
            var markerCount = CountMessagesContaining(logContent, marker);

            markerCount.Should().Be(writerCount * messagesPerWriter,
                "all accepted messages from concurrent writers should be flushed during provider disposal");
        }

        [Fact]
        public void AddCorporateLogging_WhenShutdownOverlapsActiveWriters_ShouldPersistEveryAcceptedMessage()
        {
            const int writerCount = 6;
            const int maxMessagesPerWriter = 1000;

            var services = new ServiceCollection();
            services.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory,
                BufferSize = 1000
            });

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ICorporateLogger>();
            var marker = $"OVERLAP_SHUTDOWN_{Guid.NewGuid():N}";
            var startGate = new ManualResetEventSlim(false);
            var acceptedMessageCount = 0;
            var unexpectedExceptions = new ConcurrentQueue<Exception>();

            var writerTasks = Enumerable.Range(0, writerCount)
                .Select(writerIndex => Task.Run(() =>
                {
                    startGate.Wait();

                    for (var messageIndex = 0; messageIndex < maxMessagesPerWriter; messageIndex++)
                    {
                        try
                        {
                            logger.LogInformation($"{marker}_{writerIndex}_{messageIndex}");
                            Interlocked.Increment(ref acceptedMessageCount);
                        }
                        catch (ObjectDisposedException)
                        {
                            return;
                        }
                        catch (Exception ex)
                        {
                            unexpectedExceptions.Enqueue(ex);
                            return;
                        }
                    }
                }))
                .ToArray();

            startGate.Set();
            Thread.Sleep(100);
            provider.Dispose();
            Task.WaitAll(writerTasks);

            unexpectedExceptions.Should().BeEmpty("shutdown overlap should not produce unexpected writer exceptions");

            var logContent = GetLatestLogFileContent();
            var markerCount = CountMessagesContaining(logContent, marker);

            markerCount.Should().Be(acceptedMessageCount,
                "every log call that returned successfully before shutdown should be persisted");
        }

        [Fact]
        public void AddCorporateLogging_WhenProviderIsDisposed_ShouldEmitShutdownDiagnostics()
        {
            var diagnostics = new List<CorporateLogDiagnosticEvent>();
            var services = new ServiceCollection();
            services.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory,
                DiagnosticListener = diagnostics.Add
            });

            var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ICorporateLogger>();
            logger.LogInformation("Diagnostic shutdown test");

            provider.Dispose();

            diagnostics.Select(d => d.EventType).Should().ContainInOrder(
                CorporateLogDiagnosticEventType.ShutdownStarted,
                CorporateLogDiagnosticEventType.ShutdownCompleted);

            diagnostics.Should().OnlyContain(d => d.ApplicationName == _testAppName);
            diagnostics.Should().OnlyContain(d => d.LogDirectory == _testLogDirectory);
        }

        [Fact]
        public void AddCorporateLogging_WithLoggingBuilder_ShouldWriteILoggerMessagesToYumalogFiles()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory,
                BufferSize = 1000
            }));

            using var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<ServiceCollectionExtensionsTests>>();
            var marker = $"MEL_PROVIDER_{Guid.NewGuid():N}";

            logger.LogInformation("{Marker} processed request {RequestId}", marker, 42);

            provider.Dispose();

            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(marker);
            logContent.Should().Contain("\"RequestId\":42");
            logContent.Should().Contain(nameof(ServiceCollectionExtensionsTests));
        }

        [Fact]
        public void AddCorporateLogging_WithLoggingBuilder_ShouldHonorMinimumLogLevel()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory,
                BufferSize = 1000,
                MinimumLogLevel = LogLevel.Warning
            }));

            using var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<ServiceCollectionExtensionsTests>>();
            var suppressedMarker = $"MEL_SUPPRESSED_{Guid.NewGuid():N}";
            var writtenMarker = $"MEL_WRITTEN_{Guid.NewGuid():N}";

            logger.LogInformation("{Marker} should not be written", suppressedMarker);
            logger.LogWarning("{Marker} should be written", writtenMarker);

            provider.Dispose();

            var logContent = GetLatestLogFileContent();
            logContent.Should().NotContain(suppressedMarker);
            logContent.Should().Contain(writtenMarker);
        }

        [Fact]
        public void AddCorporateLogging_WithLoggingBuilder_ShouldCaptureScopesAndExposeICorporateLogger()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory,
                BufferSize = 1000
            }));

            using var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<ServiceCollectionExtensionsTests>>();
            var corporateLogger = provider.GetRequiredService<ICorporateLogger>();
            var correlationId = Guid.NewGuid().ToString("N");
            var marker = $"MEL_SCOPE_{Guid.NewGuid():N}";

            using (logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
            {
                logger.LogWarning("{Marker} scope test", marker);
            }

            corporateLogger.LogInformation("Corporate logger remains available");
            provider.Dispose();

            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(marker);
            logContent.Should().Contain(correlationId);
            logContent.Should().Contain("Corporate logger remains available");
        }

        [Fact]
        public void AddCorporateLogging_WithLoggingBuilder_ShouldWriteExceptionsAndEventMetadata()
        {
            var services = new ServiceCollection();
            services.AddLogging(builder => builder.AddCorporateLogging(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BaseLogDirectory = _testBaseDirectory,
                BufferSize = 1000
            }));

            using var provider = services.BuildServiceProvider();
            var logger = provider.GetRequiredService<ILogger<ServiceCollectionExtensionsTests>>();
            var marker = $"MEL_EXCEPTION_{Guid.NewGuid():N}";
            var exception = new InvalidOperationException("Provider exception test");

            logger.Log(
                LogLevel.Error,
                new EventId(77, "ProviderFailure"),
                exception,
                "{Marker} failed during pipeline step {Step}",
                marker,
                "serialize");

            provider.Dispose();

            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(marker);
            logContent.Should().Contain("Provider exception test");
            logContent.Should().Contain("\"EventId\":77");
            logContent.Should().Contain("ProviderFailure");
            logContent.Should().Contain("serialize");
        }

        private static int CountMessagesContaining(string logContent, string marker)
        {
            return logContent
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.Contains(marker));
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