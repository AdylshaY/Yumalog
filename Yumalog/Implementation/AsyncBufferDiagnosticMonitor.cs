namespace Yumalog.Implementation
{
    using System;
    using System.Threading;
    using Serilog.Sinks.Async;
    using Yumalog.Diagnostics;

    /// <summary>
    /// Observes the Serilog async sink buffer and emits Yumalog diagnostic events for backpressure signals.
    /// </summary>
    internal sealed class AsyncBufferDiagnosticMonitor : IAsyncLogEventSinkMonitor, IDisposable
    {
        private readonly string _applicationName;
        private readonly string _logDirectory;
        private readonly Action<CorporateLogDiagnosticEvent> _diagnosticListener;
        private readonly TimeSpan _monitorInterval;
        private readonly int _warningUsageThresholdPercentage;
        private readonly object _sync = new object();

        private Timer _timer;
        private IAsyncLogEventSinkInspector _inspector;
        private long _lastDroppedMessagesCount;
        private bool _warningActive;

        /// <summary>
        /// Creates a new async buffer diagnostics monitor.
        /// </summary>
        public AsyncBufferDiagnosticMonitor(
            string applicationName,
            string logDirectory,
            Action<CorporateLogDiagnosticEvent> diagnosticListener,
            TimeSpan monitorInterval,
            int warningUsageThresholdPercentage)
        {
            _applicationName = applicationName ?? throw new ArgumentNullException(nameof(applicationName));
            _logDirectory = logDirectory ?? throw new ArgumentNullException(nameof(logDirectory));
            _diagnosticListener = diagnosticListener ?? throw new ArgumentNullException(nameof(diagnosticListener));
            _monitorInterval = monitorInterval;
            _warningUsageThresholdPercentage = warningUsageThresholdPercentage;
        }

        /// <inheritdoc />
        public void StartMonitoring(IAsyncLogEventSinkInspector inspector)
        {
            if (inspector == null)
            {
                throw new ArgumentNullException(nameof(inspector));
            }

            lock (_sync)
            {
                _inspector = inspector;
                _lastDroppedMessagesCount = inspector.DroppedMessagesCount;
                _warningActive = false;
                _timer = new Timer(_ => CheckHealth(), null, _monitorInterval, _monitorInterval);
            }

            EmitDiagnostic(
                CorporateLogDiagnosticEventType.AsyncBufferMonitoringStarted,
                "Async log buffer monitoring started.",
                inspector.BufferSize,
                inspector.Count,
                inspector.DroppedMessagesCount);
        }

        /// <inheritdoc />
        public void StopMonitoring(IAsyncLogEventSinkInspector inspector)
        {
            lock (_sync)
            {
                _timer?.Dispose();
                _timer = null;
                _inspector = null;
                _warningActive = false;
            }

            if (inspector != null)
            {
                EmitDiagnostic(
                    CorporateLogDiagnosticEventType.AsyncBufferMonitoringStopped,
                    "Async log buffer monitoring stopped.",
                    inspector.BufferSize,
                    inspector.Count,
                    inspector.DroppedMessagesCount);
            }
        }

        /// <summary>
        /// Executes a single health check against the currently attached inspector.
        /// </summary>
        internal void CheckHealth()
        {
            IAsyncLogEventSinkInspector inspector;

            lock (_sync)
            {
                inspector = _inspector;
            }

            if (inspector == null)
            {
                return;
            }

            var usagePercentage = inspector.BufferSize == 0
                ? 0
                : (int)Math.Ceiling(inspector.Count * 100d / inspector.BufferSize);

            if (usagePercentage >= _warningUsageThresholdPercentage)
            {
                if (!_warningActive)
                {
                    EmitDiagnostic(
                        CorporateLogDiagnosticEventType.AsyncBufferHighUsage,
                        $"Async log buffer usage reached {usagePercentage}% of capacity.",
                        inspector.BufferSize,
                        inspector.Count,
                        inspector.DroppedMessagesCount);
                    _warningActive = true;
                }
            }
            else
            {
                _warningActive = false;
            }

            if (inspector.DroppedMessagesCount > _lastDroppedMessagesCount)
            {
                _lastDroppedMessagesCount = inspector.DroppedMessagesCount;
                EmitDiagnostic(
                    CorporateLogDiagnosticEventType.AsyncBufferDroppedMessages,
                    "Async log buffer dropped messages because the queue reached capacity.",
                    inspector.BufferSize,
                    inspector.Count,
                    inspector.DroppedMessagesCount);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            StopMonitoring(_inspector);
        }

        private void EmitDiagnostic(
            CorporateLogDiagnosticEventType eventType,
            string message,
            int? bufferSize = null,
            int? bufferCount = null,
            long? droppedMessagesCount = null,
            Exception exception = null)
        {
            _diagnosticListener(new CorporateLogDiagnosticEvent(
                eventType,
                _applicationName,
                _logDirectory,
                message,
                exception,
                bufferSize,
                bufferCount,
                droppedMessagesCount));
        }
    }
}