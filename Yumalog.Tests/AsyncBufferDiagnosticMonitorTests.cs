namespace Yumalog.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using FluentAssertions;
    using Serilog.Sinks.Async;
    using Xunit;
    using Yumalog.Diagnostics;
    using Yumalog.Implementation;

    public class AsyncBufferDiagnosticMonitorTests
    {
        [Fact]
        public void StartMonitoring_ShouldEmitMonitoringStartedDiagnostic()
        {
            var diagnostics = new List<CorporateLogDiagnosticEvent>();
            var monitor = CreateMonitor(diagnostics.Add, TimeSpan.FromMinutes(1), 80);
            var inspector = new FakeAsyncLogEventSinkInspector { BufferSize = 1000, Count = 25, DroppedMessagesCount = 0 };

            monitor.StartMonitoring(inspector);

            diagnostics.Select(d => d.EventType).Should().Contain(CorporateLogDiagnosticEventType.AsyncBufferMonitoringStarted);
            diagnostics.Should().ContainSingle(d =>
                d.EventType == CorporateLogDiagnosticEventType.AsyncBufferMonitoringStarted &&
                d.BufferSize == 1000 &&
                d.BufferCount == 25);

            monitor.Dispose();
        }

        [Fact]
        public void CheckHealth_WhenUsageCrossesThreshold_ShouldEmitHighUsageDiagnosticOncePerBreach()
        {
            var diagnostics = new List<CorporateLogDiagnosticEvent>();
            var monitor = CreateMonitor(diagnostics.Add, TimeSpan.FromMinutes(1), 80);
            var inspector = new FakeAsyncLogEventSinkInspector { BufferSize = 100, Count = 79, DroppedMessagesCount = 0 };
            monitor.StartMonitoring(inspector);

            monitor.CheckHealth();
            inspector.Count = 80;
            monitor.CheckHealth();
            inspector.Count = 90;
            monitor.CheckHealth();
            inspector.Count = 10;
            monitor.CheckHealth();
            inspector.Count = 85;
            monitor.CheckHealth();

            diagnostics.Count(d => d.EventType == CorporateLogDiagnosticEventType.AsyncBufferHighUsage).Should().Be(2);
            monitor.Dispose();
        }

        [Fact]
        public void CheckHealth_WhenDroppedMessagesIncrease_ShouldEmitDroppedMessagesDiagnostic()
        {
            var diagnostics = new List<CorporateLogDiagnosticEvent>();
            var monitor = CreateMonitor(diagnostics.Add, TimeSpan.FromMinutes(1), 80);
            var inspector = new FakeAsyncLogEventSinkInspector { BufferSize = 100, Count = 100, DroppedMessagesCount = 0 };
            monitor.StartMonitoring(inspector);

            inspector.DroppedMessagesCount = 5;
            monitor.CheckHealth();

            diagnostics.Should().ContainSingle(d =>
                d.EventType == CorporateLogDiagnosticEventType.AsyncBufferDroppedMessages &&
                d.DroppedMessagesCount == 5);

            monitor.Dispose();
        }

        [Fact]
        public void StopMonitoring_ShouldEmitMonitoringStoppedDiagnostic()
        {
            var diagnostics = new List<CorporateLogDiagnosticEvent>();
            var monitor = CreateMonitor(diagnostics.Add, TimeSpan.FromMinutes(1), 80);
            var inspector = new FakeAsyncLogEventSinkInspector { BufferSize = 1000, Count = 0, DroppedMessagesCount = 0 };
            monitor.StartMonitoring(inspector);

            monitor.StopMonitoring(inspector);

            diagnostics.Select(d => d.EventType).Should().Contain(CorporateLogDiagnosticEventType.AsyncBufferMonitoringStopped);
        }

        private static AsyncBufferDiagnosticMonitor CreateMonitor(
            Action<CorporateLogDiagnosticEvent> listener,
            TimeSpan interval,
            int threshold)
        {
            return new AsyncBufferDiagnosticMonitor(
                "TestApp",
                @"C:\ServiceLogs\TestApp",
                listener,
                interval,
                threshold);
        }

        private sealed class FakeAsyncLogEventSinkInspector : IAsyncLogEventSinkInspector
        {
            public int BufferSize { get; set; }

            public int Count { get; set; }

            public long DroppedMessagesCount { get; set; }
        }
    }
}