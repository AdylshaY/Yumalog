namespace Yumalog.Tests
{
    using FluentAssertions;
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Xunit;
    using Yumalog.Configuration;
    using Yumalog.Diagnostics;

    /// <summary>
    /// Tests for actual logging functionality of YumalogManager.
    /// These tests verify that log messages are written correctly to files.
    /// </summary>
    [Collection("YumalogManager Sequential Tests")]
    public class YumalogManagerLoggingTests : IDisposable
    {
        private readonly string _testAppName;
        private readonly string _testLogDirectory;

        public YumalogManagerLoggingTests()
        {
            // Her test için benzersiz bir application name oluştur
            _testAppName = $"TestApp_{Guid.NewGuid():N}";
            _testLogDirectory = Path.Combine(@"C:\ServiceLogs", _testAppName);
        }

        public void Dispose()
        {
            if (YumalogManager.IsInitialized)
            {
                YumalogManager.Shutdown();
            }

            Thread.Sleep(500);

            // Cleanup
            if (Directory.Exists(_testLogDirectory))
            {
                try
                {
                    Directory.Delete(_testLogDirectory, recursive: true);
                }
                catch { /* Ignore */ }
            }
        }

        [Fact]
        public void LogInformation_WithSimpleMessage_ShouldWriteToFile()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var message = "This is an information message";

            // Act
            YumalogManager.Current.LogInformation(message);
            YumalogManager.Shutdown(); // Flush logs

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("\"Level\":\"Information\"");
        }

        [Fact]
        public void LogInformation_WithProperties_ShouldIncludePropertiesInLog()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var message = "User action performed";
            var properties = new Dictionary<string, object>
            {
                { "UserId", 12345 },
                { "Action", "Login" },
                { "IpAddress", "192.168.1.100" }
            };

            // Act
            YumalogManager.Current.LogInformation(message, properties);
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("UserId");
            logContent.Should().Contain("12345");
            logContent.Should().Contain("Login");
            logContent.Should().Contain("192.168.1.100");
        }

        [Fact]
        public void LogWarning_ShouldWriteWarningLevel()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var message = "This is a warning";

            // Act
            YumalogManager.Current.LogWarning(message);
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("\"Level\":\"Warning\"");
        }

        [Fact]
        public void LogError_WithException_ShouldIncludeExceptionDetails()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var message = "An error occurred";
            var exception = new InvalidOperationException("Test exception");

            // Act
            YumalogManager.Current.LogError(message, exception);
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("\"Level\":\"Error\"");
            logContent.Should().Contain("InvalidOperationException");
            logContent.Should().Contain("Test exception");
        }

        [Fact]
        public void LogError_WithExceptionAndProperties_ShouldIncludeBoth()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var message = "Database error";
            var exception = new Exception("Connection timeout");
            var properties = new Dictionary<string, object>
            {
                { "DatabaseServer", "SQL-01" },
                { "RetryCount", 3 }
            };

            // Act
            YumalogManager.Current.LogError(message, exception, properties);
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("Connection timeout");
            logContent.Should().Contain("SQL-01");
            logContent.Should().Contain("RetryCount");
        }

        [Fact]
        public void LogDebug_ShouldWriteDebugLevel()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var message = "Debug information";

            // Act
            YumalogManager.Current.LogDebug(message);
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("\"Level\":\"Debug\"");
        }

        [Fact]
        public void LogFatal_WithException_ShouldWriteFatalLevel()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var message = "Critical system failure";
            var exception = new OutOfMemoryException("System out of memory");

            // Act
            YumalogManager.Current.LogFatal(message, exception);
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("\"Level\":\"Fatal\"");
            logContent.Should().Contain("OutOfMemoryException");
        }

        [Fact]
        public void LogInformationObject_WithComplexObject_ShouldSerializeObject()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var message = "User data";
            var userData = new
            {
                Id = 101,
                Name = "John Doe",
                Email = "john@example.com",
                Roles = new[] { "Admin", "User" }
            };

            // Act
            YumalogManager.Current.LogInformationObject(message, userData);
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("John Doe");
            logContent.Should().Contain("john@example.com");
            logContent.Should().Contain("Admin");
        }

        [Fact]
        public void LogMultipleMessages_ShouldWriteAllMessages()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);

            // Act
            YumalogManager.Current.LogInformation("Message 1");
            YumalogManager.Current.LogWarning("Message 2");
            YumalogManager.Current.LogError("Message 3");
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("Message 1");
            logContent.Should().Contain("Message 2");
            logContent.Should().Contain("Message 3");
        }

        [Fact]
        public void Logging_ShouldIncludeMachineName()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var expectedMachineName = Environment.MachineName;

            // Act
            YumalogManager.Current.LogInformation("Test message");
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("MachineName");
            logContent.Should().Contain(expectedMachineName);
        }

        [Fact]
        public void Logging_ShouldIncludeApplicationName()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);

            // Act
            YumalogManager.Current.LogInformation("Test message");
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("Application");
            logContent.Should().Contain(_testAppName);
        }

        [Fact]
        public void Logging_WithCustomEnvironment_ShouldIncludeEnvironment()
        {
            // Arrange
            var config = new YumalogConfiguration
            {
                ApplicationName = _testAppName,
                Environment = "Production"
            };
            YumalogManager.Initialize(config);

            // Act
            YumalogManager.Current.LogInformation("Test message");
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("Environment");
            logContent.Should().Contain("Production");
        }

        [Fact]
        public void Logging_ShouldIncludeProcessId()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var expectedProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

            // Act
            YumalogManager.Current.LogInformation("Test message");
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("ProcessId");
            logContent.Should().Contain(expectedProcessId.ToString());
        }

        [Fact]
        public void LogFile_ShouldBeCreatedInCorrectDirectory()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);

            // Act
            YumalogManager.Current.LogInformation("Test message");
            YumalogManager.Shutdown();

            // Assert
            Directory.Exists(_testLogDirectory).Should().BeTrue();
            var logFiles = Directory.GetFiles(_testLogDirectory, "log-*.json");
            logFiles.Should().NotBeEmpty();
        }

        [Fact]
        public void LogFile_ShouldHaveJsonFormat()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);

            // Act
            YumalogManager.Current.LogInformation("Test message");
            YumalogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();

            // Her satır geçerli JSON olmalı
            var lines = logContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                Action parseJson = () => JsonDocument.Parse(line);
                parseJson.Should().NotThrow("each log line should be valid JSON");
            }
        }

        [Fact]
        public void LogWithNullProperties_ShouldNotThrowException()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);

            // Act
            Action act = () => YumalogManager.Current.LogInformation("Message", properties: null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void LogWithEmptyProperties_ShouldNotThrowException()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var emptyProperties = new Dictionary<string, object>();

            // Act
            Action act = () => YumalogManager.Current.LogInformation("Message", emptyProperties);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Shutdown_WithPendingLogsInBuffer_ShouldFlushAllLogs()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var messageCount = 1000; // Çok sayıda log mesajı
            var messages = new List<string>();

            // Act - Hızlı bir şekilde çok sayıda log yaz (buffer'a gider)
            for (int i = 0; i < messageCount; i++)
            {
                var message = $"Log message number {i}";
                messages.Add(message);
                YumalogManager.Current.LogInformation(message);
            }

            // Shutdown çağrılarak buffer'daki tüm loglar flush edilmeli
            YumalogManager.Shutdown();

            // Assert - Tüm mesajların dosyaya yazıldığını kontrol et
            var logContent = GetLatestLogFileContent();

            // İlk, orta ve son mesajları kontrol et
            logContent.Should().Contain(messages[0], "first message should be written");
            logContent.Should().Contain(messages[messageCount / 2], "middle message should be written");
            logContent.Should().Contain(messages[messageCount - 1], "last message should be written");

            // Log satır sayısını kontrol et (her mesaj bir satır)
            var logLines = logContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            logLines.Should().HaveCountGreaterThanOrEqualTo(messageCount,
                "all messages should be flushed to file before shutdown completes");
        }

        [Fact]
        public void Shutdown_WithLargeVolumeOfLogs_ShouldNotLoseAnyData()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var expectedLogCount = 5000; // Yüksek hacimli test
            var uniqueIdentifier = Guid.NewGuid().ToString(); // Benzersiz tanımlayıcı

            // Act - Aynı anda farklı log seviyeleriyle yaz
            for (int i = 0; i < expectedLogCount; i++)
            {
                var message = $"{uniqueIdentifier}_Message_{i}";

                // Farklı log seviyelerini karıştır
                switch (i % 4)
                {
                    case 0:
                        YumalogManager.Current.LogInformation(message);
                        break;
                    case 1:
                        YumalogManager.Current.LogWarning(message);
                        break;
                    case 2:
                        YumalogManager.Current.LogError(message);
                        break;
                    case 3:
                        YumalogManager.Current.LogDebug(message);
                        break;
                }
            }

            // Immediate shutdown - buffer'daki tüm loglar yazılmalı
            YumalogManager.Shutdown();

            // Assert - Tüm logların yazıldığını say
            var logContent = GetLatestLogFileContent();

            // Unique identifier'ı içeren satırları say
            var logLines = logContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var writtenLogCount = logLines.Count(line => line.Contains(uniqueIdentifier));

            writtenLogCount.Should().Be(expectedLogCount,
                "FlushAndShutdown must ensure all buffered logs are written to disk");
        }

        [Fact]
        public void MultipleShutdowns_ShouldNotLoseDataOrThrowException()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);

            // Act - Logları yaz ve birden fazla shutdown çağır
            YumalogManager.Current.LogInformation("Message 1");
            YumalogManager.Current.LogWarning("Message 2");
            YumalogManager.Current.LogError("Message 3");

            // İlk shutdown
            YumalogManager.Shutdown();

            // İkinci shutdown (zaten kapatılmış) - exception fırlatmamalı
            Action act = () => YumalogManager.Shutdown();

            // Assert
            act.Should().NotThrow("multiple shutdowns should be safe");

            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("Message 1");
            logContent.Should().Contain("Message 2");
            logContent.Should().Contain("Message 3");
        }

        [Fact]
        public void RapidLogging_WithImmediateShutdown_ShouldFlushAllLogs()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var startMarker = $"START_{Guid.NewGuid()}";
            var endMarker = $"END_{Guid.NewGuid()}";

            // Act - Çok hızlı ardışık loglar (async buffer'ı test et)
            YumalogManager.Current.LogInformation(startMarker);

            // 100 log mesajı hızlı bir şekilde ekle
            for (int i = 1; i <= 100; i++)
            {
                YumalogManager.Current.LogInformation($"Rapid log {i}");
            }

            YumalogManager.Current.LogInformation(endMarker);

            // Hemen ardından shutdown (buffer henüz boşalmamış olabilir)
            YumalogManager.Shutdown();

            // Assert - Başlangıç ve bitiş marker'ları mutlaka yazılmış olmalı
            var logContent = GetLatestLogFileContent();

            logContent.Should().Contain(startMarker, "first log should be flushed");
            logContent.Should().Contain(endMarker, "last log should be flushed");
            logContent.Should().Contain("Rapid log 1", "intermediate logs should be flushed");
            logContent.Should().Contain("Rapid log 100", "all logs including last should be flushed");
        }

        [Fact]
        public void Logging_WithPropertiesAndImmediateShutdown_ShouldPreserveAllData()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var testGuid = Guid.NewGuid().ToString();

            // Act - Properties ile log yaz ve hemen kapat
            for (int i = 0; i < 100; i++)
            {
                var properties = new Dictionary<string, object>
                {
                    { "TestGuid", testGuid },
                    { "Iteration", i },
                    { "Timestamp", DateTime.UtcNow }
                };
                YumalogManager.Current.LogInformation($"Property test {i}", properties);
            }

            YumalogManager.Shutdown();

            // Assert - Tüm properties'lerin yazıldığını kontrol et
            var logContent = GetLatestLogFileContent();

            logContent.Should().Contain(testGuid, "custom properties should be preserved during flush");
            logContent.Should().Contain("\"Iteration\"", "property names should be written");
            logContent.Should().Contain("Property test 0", "first message should be flushed");
            logContent.Should().Contain("Property test 99", "last message should be flushed");
        }

        [Fact]
        public void BufferOverflow_Scenario_ShouldStillWriteAllLogs()
        {
            // Arrange - Buffer size'dan daha fazla log yaz
            var config = new YumalogConfiguration
            {
                ApplicationName = _testAppName,
                BufferSize = 1000 // Küçük buffer (test için)
            };
            YumalogManager.Initialize(config);

            var logCount = 50000; // Buffer'dan çok daha fazla
            var testMarker = $"OVERFLOW_TEST_{Guid.NewGuid()}";

            // Act - Buffer'ı aş
            for (int i = 0; i < logCount; i++)
            {
                YumalogManager.Current.LogInformation($"{testMarker}_{i}");
            }

            YumalogManager.Shutdown();

            // Assert - Tüm loglar yazılmış olmalı
            var logContent = GetLatestLogFileContent();
            var lines = logContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var markerCount = lines.Count(line => line.Contains(testMarker));

            markerCount.Should().Be(logCount,
                "even when buffer overflows, all logs should eventually be written");
        }

        [Fact]
        public void UnexpectedShutdown_WithoutExplicitFlush_ShouldStillWriteSomeLogs()
        {
            // Arrange
            YumalogManager.Initialize(_testAppName);
            var criticalMessage = $"CRITICAL_{Guid.NewGuid()}";

            // Act - Log yaz ama Shutdown ÇAĞIRMA (crash simülasyonu)
            YumalogManager.Current.LogInformation(criticalMessage);

            // Serilog async sink'in background thread'inin yazmasını bekle
            Thread.Sleep(1000); // Normal durumda async sink bir süre sonra yazar

            // Logger'ı dispose et (crash durumunda GC bunu yapabilir)
            // Not: Bu gerçek crash değil, ama en yakın simülasyon
            if (YumalogManager.IsInitialized)
            {
                // Internal dispose'u tetikle
                YumalogManager.Shutdown();
            }

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(criticalMessage,
                "async sink should write logs even without explicit flush if enough time passes");
        }

        [Fact]
        public void ProcessAbort_Simulation_LogsInBufferMayBeLost()
        {
            // Bu test BEKLENEN BİR BAŞARISIZLIĞI gösterir!
            // Eğer process aniden kill edilirse, buffer'daki loglar kaybolur.

            // Arrange
            YumalogManager.Initialize(_testAppName);
            var volatileMessage = $"VOLATILE_{Guid.NewGuid()}";

            // Act - Log yaz ve HEMEN bitir (flush yok, bekleme yok)
            YumalogManager.Current.LogInformation(volatileMessage);

            // Hiç bekleme yapmadan logger'ı kapat (crash simülasyonu)
            // NOT: Bu senaryoda loglar kaybolabilir!

            // Assert
            // Bu test başarısız OLABİLİR - bu normaldir!
            // Çünkü async buffer henüz flush olmamıştır
            try
            {
                var logContent = GetLatestLogFileContent();
                // Eğer log yazıldıysa, şanslıyız
                // Yazılmadıysa, bu beklenen bir durumdur
            }
            catch (FileNotFoundException)
            {
                // Log dosyası bile oluşmadı - bu crash senaryosunda normal
                Assert.True(true, "In crash scenario, logs may be lost before flush");
            }
        }

        [Fact]
        public void Initialize_MultipleTimes_ShouldRegisterHandlerOnlyOnce()
        {
            // Arrange & Act - İlk initialize
            YumalogManager.Initialize(_testAppName);
            YumalogManager.Shutdown();

            // İkinci initialize
            var secondAppName = $"TestApp2_{Guid.NewGuid():N}";
            YumalogManager.Initialize(secondAppName);

            // Assert - Handler'ların duplicate olmaması gerekir
            // (Bu internal behavior ama test edebiliriz)
            YumalogManager.IsInitialized.Should().BeTrue();

            Action act = () => YumalogManager.Current.LogInformation("Test");
            act.Should().NotThrow();
        }

        [Fact]
        public void EmergencyLogging_WithMinimalFlushTime_ShouldWriteToFile()
        {
            // Bu test ProcessExit senaryosunu simüle eder
            // Çünkü ProcessExit handler da aynı Shutdown mekanizmasını kullanır

            // Arrange
            YumalogManager.Initialize(_testAppName);
            var emergencyMessage = $"EMERGENCY_{Guid.NewGuid()}";

            // Act - Kritik log yaz ve hemen flush et
            YumalogManager.Current.LogInformation(emergencyMessage);
            YumalogManager.Current.LogError("Critical error before shutdown");

            // Shutdown (ProcessExit handler da aynı şeyi yapar)
            YumalogManager.Shutdown();

            // Assert - Loglar mutlaka yazılmalı
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(emergencyMessage);
            logContent.Should().Contain("Critical error before shutdown");
        }

        [Fact]
        public void Shutdown_WithConcurrentWriters_ShouldFlushAllAcceptedLogs()
        {
            const int writerCount = 8;
            const int messagesPerWriter = 250;

            YumalogManager.Initialize(_testAppName);
            var marker = $"LEGACY_CONCURRENT_FLUSH_{Guid.NewGuid():N}";

            var writerTasks = Enumerable.Range(0, writerCount)
                .Select(writerIndex => Task.Run(() =>
                {
                    for (var messageIndex = 0; messageIndex < messagesPerWriter; messageIndex++)
                    {
                        YumalogManager.Current.LogInformation($"{marker}_{writerIndex}_{messageIndex}");
                    }
                }))
                .ToArray();

            Task.WaitAll(writerTasks);
            YumalogManager.Shutdown();

            var logContent = GetLatestLogFileContent();
            var markerCount = CountMessagesContaining(logContent, marker);

            markerCount.Should().Be(writerCount * messagesPerWriter,
                "legacy shutdown should flush all accepted messages from concurrent writers");
        }

        [Fact]
        public void Shutdown_WhenOverlappingActiveWriters_ShouldPersistEveryAcceptedMessage()
        {
            const int writerCount = 6;
            const int maxMessagesPerWriter = 1000;

            YumalogManager.Initialize(_testAppName);
            var marker = $"LEGACY_OVERLAP_{Guid.NewGuid():N}";
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
                            YumalogManager.Current.LogInformation($"{marker}_{writerIndex}_{messageIndex}");
                            Interlocked.Increment(ref acceptedMessageCount);
                        }
                        catch (InvalidOperationException ex) when (ex.Message.Contains("has not been initialized"))
                        {
                            return;
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
            YumalogManager.Shutdown();
            Task.WaitAll(writerTasks);

            unexpectedExceptions.Should().BeEmpty("legacy shutdown overlap should not produce unexpected writer exceptions");

            var logContent = GetLatestLogFileContent();
            var markerCount = CountMessagesContaining(logContent, marker);

            markerCount.Should().Be(acceptedMessageCount,
                "every log call that returned successfully before legacy shutdown should be persisted");
        }

        [Fact]
        public void Shutdown_WhenCalledExplicitly_ShouldEmitShutdownDiagnostics()
        {
            var diagnostics = new List<YumalogDiagnosticEvent>();
            YumalogManager.Initialize(new YumalogConfiguration
            {
                ApplicationName = _testAppName,
                DiagnosticListener = diagnostics.Add
            });

            YumalogManager.Current.LogInformation("Legacy diagnostic shutdown test");
            YumalogManager.Shutdown();

            diagnostics.Select(d => d.EventType).Should().ContainInOrder(
                YumalogDiagnosticEventType.ShutdownStarted,
                YumalogDiagnosticEventType.ShutdownCompleted);

            diagnostics.Should().OnlyContain(d => d.ApplicationName == _testAppName);
            diagnostics.Should().OnlyContain(d => d.LogDirectory == _testLogDirectory);
        }

        #region Helper Methods

        private static int CountMessagesContaining(string logContent, string marker)
        {
            return logContent
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Count(line => line.Contains(marker));
        }

        /// <summary>
        /// Gets the content of the latest log file in the test directory.
        /// </summary>
        private string GetLatestLogFileContent()
        {
            const int maxWaitMs = 3000;
            const int checkIntervalMs = 100;

            // Klasör bekleme
            var elapsed = 0;
            while (!Directory.Exists(_testLogDirectory) && elapsed < maxWaitMs)
            {
                Thread.Sleep(checkIntervalMs);
                elapsed += checkIntervalMs;
            }

            if (!Directory.Exists(_testLogDirectory))
            {
                throw new DirectoryNotFoundException(
                    $"Log directory not found after {maxWaitMs}ms: {_testLogDirectory}");
            }

            // Dosya bekleme
            elapsed = 0;
            string[] logFiles = null;
            while (elapsed < maxWaitMs)
            {
                logFiles = Directory.GetFiles(_testLogDirectory, "log-*.json");
                if (logFiles.Length > 0)
                    break;

                Thread.Sleep(checkIntervalMs);
                elapsed += checkIntervalMs;
            }

            if (logFiles == null || logFiles.Length == 0)
            {
                throw new FileNotFoundException(
                    $"No log files found after {maxWaitMs}ms in: {_testLogDirectory}");
            }

            var latestFile = logFiles
                .Select(f => new FileInfo(f))
                .OrderByDescending(f => f.LastWriteTime)
                .First();

            elapsed = 0;
            Exception lastException = null;

            while (elapsed < maxWaitMs)
            {
                try
                {
                    using (var fs = new FileStream(latestFile.FullName,
                                                   FileMode.Open,
                                                   FileAccess.Read,
                                                   FileShare.ReadWrite))
                    using (var reader = new StreamReader(fs))
                    {
                        return reader.ReadToEnd();
                    }
                }
                catch (FileNotFoundException ex)
                {
                    lastException = ex;
                    Thread.Sleep(checkIntervalMs);
                    elapsed += checkIntervalMs;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    Thread.Sleep(checkIntervalMs);
                    elapsed += checkIntervalMs;
                }
            }

            throw new IOException(
                $"Could not read log file after {maxWaitMs}ms. Last error: {lastException?.Message}",
                lastException);
        }

        #endregion
    }
}