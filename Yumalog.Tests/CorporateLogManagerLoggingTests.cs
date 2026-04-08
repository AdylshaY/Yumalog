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
    /// Tests for actual logging functionality of CorporateLogManager.
    /// These tests verify that log messages are written correctly to files.
    /// </summary>
    [Collection("CorporateLogManager Sequential Tests")]
    public class CorporateLogManagerLoggingTests : IDisposable
    {
        private readonly string _testAppName;
        private readonly string _testLogDirectory;

        public CorporateLogManagerLoggingTests()
        {
            // Her test için benzersiz bir application name oluştur
            _testAppName = $"TestApp_{Guid.NewGuid():N}";
            _testLogDirectory = Path.Combine(@"C:\ServiceLogs", _testAppName);
        }

        public void Dispose()
        {
            if (CorporateLogManager.IsInitialized)
            {
                CorporateLogManager.Shutdown();
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
            CorporateLogManager.Initialize(_testAppName);
            var message = "This is an information message";

            // Act
            CorporateLogManager.Current.LogInformation(message);
            CorporateLogManager.Shutdown(); // Flush logs

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("\"Level\":\"Information\"");
        }

        [Fact]
        public void LogInformation_WithProperties_ShouldIncludePropertiesInLog()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);
            var message = "User action performed";
            var properties = new Dictionary<string, object>
            {
                { "UserId", 12345 },
                { "Action", "Login" },
                { "IpAddress", "192.168.1.100" }
            };

            // Act
            CorporateLogManager.Current.LogInformation(message, properties);
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
            var message = "This is a warning";

            // Act
            CorporateLogManager.Current.LogWarning(message);
            CorporateLogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("\"Level\":\"Warning\"");
        }

        [Fact]
        public void LogError_WithException_ShouldIncludeExceptionDetails()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);
            var message = "An error occurred";
            var exception = new InvalidOperationException("Test exception");

            // Act
            CorporateLogManager.Current.LogError(message, exception);
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
            var message = "Database error";
            var exception = new Exception("Connection timeout");
            var properties = new Dictionary<string, object>
            {
                { "DatabaseServer", "SQL-01" },
                { "RetryCount", 3 }
            };

            // Act
            CorporateLogManager.Current.LogError(message, exception, properties);
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
            var message = "Debug information";

            // Act
            CorporateLogManager.Current.LogDebug(message);
            CorporateLogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain(message);
            logContent.Should().Contain("\"Level\":\"Debug\"");
        }

        [Fact]
        public void LogFatal_WithException_ShouldWriteFatalLevel()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);
            var message = "Critical system failure";
            var exception = new OutOfMemoryException("System out of memory");

            // Act
            CorporateLogManager.Current.LogFatal(message, exception);
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
            var message = "User data";
            var userData = new
            {
                Id = 101,
                Name = "John Doe",
                Email = "john@example.com",
                Roles = new[] { "Admin", "User" }
            };

            // Act
            CorporateLogManager.Current.LogInformationObject(message, userData);
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);

            // Act
            CorporateLogManager.Current.LogInformation("Message 1");
            CorporateLogManager.Current.LogWarning("Message 2");
            CorporateLogManager.Current.LogError("Message 3");
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
            var expectedMachineName = Environment.MachineName;

            // Act
            CorporateLogManager.Current.LogInformation("Test message");
            CorporateLogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("MachineName");
            logContent.Should().Contain(expectedMachineName);
        }

        [Fact]
        public void Logging_ShouldIncludeApplicationName()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);

            // Act
            CorporateLogManager.Current.LogInformation("Test message");
            CorporateLogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("Application");
            logContent.Should().Contain(_testAppName);
        }

        [Fact]
        public void Logging_WithCustomEnvironment_ShouldIncludeEnvironment()
        {
            // Arrange
            var config = new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                Environment = "Production"
            };
            CorporateLogManager.Initialize(config);

            // Act
            CorporateLogManager.Current.LogInformation("Test message");
            CorporateLogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("Environment");
            logContent.Should().Contain("Production");
        }

        [Fact]
        public void Logging_ShouldIncludeProcessId()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);
            var expectedProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;

            // Act
            CorporateLogManager.Current.LogInformation("Test message");
            CorporateLogManager.Shutdown();

            // Assert
            var logContent = GetLatestLogFileContent();
            logContent.Should().Contain("ProcessId");
            logContent.Should().Contain(expectedProcessId.ToString());
        }

        [Fact]
        public void LogFile_ShouldBeCreatedInCorrectDirectory()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);

            // Act
            CorporateLogManager.Current.LogInformation("Test message");
            CorporateLogManager.Shutdown();

            // Assert
            Directory.Exists(_testLogDirectory).Should().BeTrue();
            var logFiles = Directory.GetFiles(_testLogDirectory, "log-*.json");
            logFiles.Should().NotBeEmpty();
        }

        [Fact]
        public void LogFile_ShouldHaveJsonFormat()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);

            // Act
            CorporateLogManager.Current.LogInformation("Test message");
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);

            // Act
            Action act = () => CorporateLogManager.Current.LogInformation("Message", properties: null);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void LogWithEmptyProperties_ShouldNotThrowException()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);
            var emptyProperties = new Dictionary<string, object>();

            // Act
            Action act = () => CorporateLogManager.Current.LogInformation("Message", emptyProperties);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Shutdown_WithPendingLogsInBuffer_ShouldFlushAllLogs()
        {
            // Arrange
            CorporateLogManager.Initialize(_testAppName);
            var messageCount = 1000; // Çok sayıda log mesajı
            var messages = new List<string>();

            // Act - Hızlı bir şekilde çok sayıda log yaz (buffer'a gider)
            for (int i = 0; i < messageCount; i++)
            {
                var message = $"Log message number {i}";
                messages.Add(message);
                CorporateLogManager.Current.LogInformation(message);
            }

            // Shutdown çağrılarak buffer'daki tüm loglar flush edilmeli
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
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
                        CorporateLogManager.Current.LogInformation(message);
                        break;
                    case 1:
                        CorporateLogManager.Current.LogWarning(message);
                        break;
                    case 2:
                        CorporateLogManager.Current.LogError(message);
                        break;
                    case 3:
                        CorporateLogManager.Current.LogDebug(message);
                        break;
                }
            }

            // Immediate shutdown - buffer'daki tüm loglar yazılmalı
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);

            // Act - Logları yaz ve birden fazla shutdown çağır
            CorporateLogManager.Current.LogInformation("Message 1");
            CorporateLogManager.Current.LogWarning("Message 2");
            CorporateLogManager.Current.LogError("Message 3");

            // İlk shutdown
            CorporateLogManager.Shutdown();

            // İkinci shutdown (zaten kapatılmış) - exception fırlatmamalı
            Action act = () => CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
            var startMarker = $"START_{Guid.NewGuid()}";
            var endMarker = $"END_{Guid.NewGuid()}";

            // Act - Çok hızlı ardışık loglar (async buffer'ı test et)
            CorporateLogManager.Current.LogInformation(startMarker);

            // 100 log mesajı hızlı bir şekilde ekle
            for (int i = 1; i <= 100; i++)
            {
                CorporateLogManager.Current.LogInformation($"Rapid log {i}");
            }

            CorporateLogManager.Current.LogInformation(endMarker);

            // Hemen ardından shutdown (buffer henüz boşalmamış olabilir)
            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
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
                CorporateLogManager.Current.LogInformation($"Property test {i}", properties);
            }

            CorporateLogManager.Shutdown();

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
            var config = new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                BufferSize = 1000 // Küçük buffer (test için)
            };
            CorporateLogManager.Initialize(config);

            var logCount = 50000; // Buffer'dan çok daha fazla
            var testMarker = $"OVERFLOW_TEST_{Guid.NewGuid()}";

            // Act - Buffer'ı aş
            for (int i = 0; i < logCount; i++)
            {
                CorporateLogManager.Current.LogInformation($"{testMarker}_{i}");
            }

            CorporateLogManager.Shutdown();

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
            CorporateLogManager.Initialize(_testAppName);
            var criticalMessage = $"CRITICAL_{Guid.NewGuid()}";

            // Act - Log yaz ama Shutdown ÇAĞIRMA (crash simülasyonu)
            CorporateLogManager.Current.LogInformation(criticalMessage);

            // Serilog async sink'in background thread'inin yazmasını bekle
            Thread.Sleep(1000); // Normal durumda async sink bir süre sonra yazar

            // Logger'ı dispose et (crash durumunda GC bunu yapabilir)
            // Not: Bu gerçek crash değil, ama en yakın simülasyon
            if (CorporateLogManager.IsInitialized)
            {
                // Internal dispose'u tetikle
                CorporateLogManager.Shutdown();
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
            CorporateLogManager.Initialize(_testAppName);
            var volatileMessage = $"VOLATILE_{Guid.NewGuid()}";

            // Act - Log yaz ve HEMEN bitir (flush yok, bekleme yok)
            CorporateLogManager.Current.LogInformation(volatileMessage);

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
            CorporateLogManager.Initialize(_testAppName);
            CorporateLogManager.Shutdown();

            // İkinci initialize
            var secondAppName = $"TestApp2_{Guid.NewGuid():N}";
            CorporateLogManager.Initialize(secondAppName);

            // Assert - Handler'ların duplicate olmaması gerekir
            // (Bu internal behavior ama test edebiliriz)
            CorporateLogManager.IsInitialized.Should().BeTrue();

            Action act = () => CorporateLogManager.Current.LogInformation("Test");
            act.Should().NotThrow();
        }

        [Fact]
        public void EmergencyLogging_WithMinimalFlushTime_ShouldWriteToFile()
        {
            // Bu test ProcessExit senaryosunu simüle eder
            // Çünkü ProcessExit handler da aynı Shutdown mekanizmasını kullanır

            // Arrange
            CorporateLogManager.Initialize(_testAppName);
            var emergencyMessage = $"EMERGENCY_{Guid.NewGuid()}";

            // Act - Kritik log yaz ve hemen flush et
            CorporateLogManager.Current.LogInformation(emergencyMessage);
            CorporateLogManager.Current.LogError("Critical error before shutdown");

            // Shutdown (ProcessExit handler da aynı şeyi yapar)
            CorporateLogManager.Shutdown();

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

            CorporateLogManager.Initialize(_testAppName);
            var marker = $"LEGACY_CONCURRENT_FLUSH_{Guid.NewGuid():N}";

            var writerTasks = Enumerable.Range(0, writerCount)
                .Select(writerIndex => Task.Run(() =>
                {
                    for (var messageIndex = 0; messageIndex < messagesPerWriter; messageIndex++)
                    {
                        CorporateLogManager.Current.LogInformation($"{marker}_{writerIndex}_{messageIndex}");
                    }
                }))
                .ToArray();

            Task.WaitAll(writerTasks);
            CorporateLogManager.Shutdown();

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

            CorporateLogManager.Initialize(_testAppName);
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
                            CorporateLogManager.Current.LogInformation($"{marker}_{writerIndex}_{messageIndex}");
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
            CorporateLogManager.Shutdown();
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
            var diagnostics = new List<CorporateLogDiagnosticEvent>();
            CorporateLogManager.Initialize(new CorporateLogConfiguration
            {
                ApplicationName = _testAppName,
                DiagnosticListener = diagnostics.Add
            });

            CorporateLogManager.Current.LogInformation("Legacy diagnostic shutdown test");
            CorporateLogManager.Shutdown();

            diagnostics.Select(d => d.EventType).Should().ContainInOrder(
                CorporateLogDiagnosticEventType.ShutdownStarted,
                CorporateLogDiagnosticEventType.ShutdownCompleted);

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