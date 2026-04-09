# Yumalog - Centralized Structured Logging

Centralized structured logging library for .NET Framework and .NET Core applications, designed for collection with Grafana Alloy and visualization through Grafana Loki and Grafana.

---

## Overview

Yumalog is a structured logging library for distributed systems running on Windows Servers. It provides a unified logging interface for both legacy .NET Framework applications and modern .NET Core/.NET 5+ applications.

**Architecture:** Applications write logs to JSON files locally (`C:\ServiceLogs\{ApplicationName}`), then Grafana Alloy can collect and forward them to Loki for centralized monitoring via Grafana.

Yumalog itself is a logging library. It does not bundle Grafana, Loki, or Alloy inside this repository. Instead, it produces structured JSON files that are intended to be collected by an external observability stack.

---

## Features

- **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+ and .NET Core 2.0+/.NET 5+
- **Dual API** - Static manager for legacy apps, Dependency Injection for modern apps
- **Structured Logging** - JSON format with key-value pairs
- **Configurable Buffer** - 50,000 message buffer with optional blocking mode
- **Auto-Enrichment** - Application name, environment, machine name, process ID
- **File Isolation** - Each application writes to `C:\ServiceLogs\{ApplicationName}\`
- **Async Sink** - Non-blocking logging operations via Serilog.Sinks.Async
- **Graceful Shutdown** - Ensures buffered logs are flushed before exit
- **Configurable** - Buffer size, file retention, rolling interval, file size limits
- **Runtime Diagnostics** - Shutdown and async buffer health events for rollout and operations

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Application (.NET Framework / .NET Core / .NET 5+)             │
│  └─→ Yumalog Library                                            │
│      └─→ Serilog.Sinks.Async (50k buffer, non-blocking)        │
│          └─→ JSON File (C:\ServiceLogs\{AppName}\log-.json)    │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ↓ (File watching)
┌─────────────────────────────────────────────────────────────────┐
│  Grafana Alloy (Agent) - Installed once per server             │
│  - Watches: C:\ServiceLogs\*\*.json                            │
│  - Parses JSON and extracts labels                              │
│  - Tracks file offset                                            │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ↓ (HTTP Push API)
┌─────────────────────────────────────────────────────────────────┐
│  Grafana Loki (Central Log Database)                            │
│  - Stores logs with labels (app, env, host, level)             │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ↓ (Query)
┌─────────────────────────────────────────────────────────────────┐
│  Grafana (Visualization & Dashboards)                           │
│  - LogQL queries, filtering, alerting                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## Installation

### NuGet Package

```bash
dotnet add package Yumalog
```

### Local Package Build

Use this flow when you want to build Yumalog locally and test it in separate Windows Service projects before publishing it to a private feed.

```powershell
dotnet build .\Yumalog\Yumalog.csproj -c Release
dotnet pack .\Yumalog\Yumalog.csproj -c Release -o .\artifacts\packages --no-build
```

The generated local package will be placed under:

```text
artifacts\packages\Yumalog.1.0.0.nupkg
```

Install it into a test project from the local package folder:

```powershell
dotnet add package Yumalog --source "C:\path\to\Yumalog\artifacts\packages"
```

If you rebuild the package multiple times while testing, use one of these approaches:

1. Increase the package version in `Yumalog.csproj` before packing again.
2. Or clear local NuGet caches before re-installing the same version.

```powershell
dotnet nuget locals all --clear
```

### Manual Build

```bash
git clone https://github.com/AdylshaY/Yumalog.git
cd Yumalog
dotnet build .\Yumalog\Yumalog.csproj -c Release
dotnet pack .\Yumalog\Yumalog.csproj -c Release -o .\artifacts\packages --no-build
dotnet add package Yumalog --source ./artifacts/packages
```

### Azure DevOps Private Feed

For internal company-wide usage, publish Yumalog to an Azure DevOps Artifacts feed and consume it from Windows Service projects like any other private NuGet package.

#### 1. Publish the package

Build and pack the project locally or in CI:

```powershell
dotnet build .\Yumalog\Yumalog.csproj -c Release
dotnet pack .\Yumalog\Yumalog.csproj -c Release -o .\artifacts\packages --no-build
```

Then publish the generated `.nupkg` to your Azure DevOps feed.

Example with `dotnet nuget push`:

```powershell
dotnet nuget push .\artifacts\packages\Yumalog.1.0.0.nupkg \
    --source "YourAzureArtifactsFeed" \
    --api-key az
```

Your machine or pipeline must already be authenticated to the Azure DevOps feed.

#### 2. Add the feed to consuming projects

After the private feed is available in NuGet sources, install the package in a service project:

```powershell
dotnet add package Yumalog --source "YourAzureArtifactsFeed"
```

Or restore through a `NuGet.config` that already includes the Azure Artifacts feed URL.

#### 3. Update consuming projects to a newer version

When a new Yumalog version is published, consuming projects can update with:

```powershell
dotnet add package Yumalog --version 1.0.1
```

or:

```powershell
dotnet restore
```

if package version management is already handled centrally.

### Recommended Internal Release Flow

For internal version rollout, use this sequence:

1. Update the `<Version>` value in `Yumalog.csproj`.
2. Build and run the Yumalog test suite.
3. Pack the new `.nupkg`.
4. Publish the package to the Azure DevOps Artifacts feed.
5. Update one or two pilot Windows Services first.
6. Validate diagnostics, log output, and Loki/Grafana visibility.
7. Roll the new package version out to broader internal services.

This keeps the package workflow consistent with the intended enterprise distribution model.

---

## Quick Start

### Legacy Applications (.NET Framework)

For applications without Dependency Injection:

```csharp
using System;
using System.Collections.Generic;
using Yumalog;

class Program
{
    static void Main()
    {
        CorporateLogManager.Initialize("MyWindowsService", "Production");

        try
        {
            var logger = CorporateLogManager.Current;
            logger.LogInformation("Service started");

            ProcessOrders(logger);
        }
        catch (Exception ex)
        {
            var logger = CorporateLogManager.Current;
            logger.LogFatal("Service crashed", ex);
        }
        finally
        {
            CorporateLogManager.Shutdown();
        }
    }

    static void ProcessOrders(ICorporateLogger logger)
    {
        logger.LogInformation("Processing orders", new Dictionary<string, object>
        {
            { "OrderCount", 150 },
            { "BatchId", Guid.NewGuid() }
        });
    }
}
```

---

### Modern Applications (.NET Core/.NET 5+)

Uses Dependency Injection:

**Program.cs**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yumalog.Extensions;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Simple initialization
        services.AddCorporateLogging("MyWorkerService", "Production");

        // OR with configuration
        services.AddCorporateLogging(config =>
        {
            config.ApplicationName = "MyWorkerService";
            config.Environment = "Production";
            config.BufferSize = 100000;
            config.BlockWhenFull = true;
            config.RetainedFileCountLimit = 60;
            config.DiagnosticListener = diagnostic =>
            {
                System.Diagnostics.Trace.WriteLine(
                    $"{diagnostic.TimestampUtc:o} {diagnostic.EventType} {diagnostic.Message}");
            };
        });

        services.AddHostedService<OrderProcessorWorker>();
    })
    .Build();

await host.RunAsync();
```

---

### ASP.NET Core APIs With Existing ILogger Usage

Use this mode when an API project already depends on `ILogger<T>` and you want Yumalog to become the backend provider without changing application code.

**Program.cs**

```csharp
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Hosting;
using Yumalog.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddCorporateLogging(builder.Configuration);

builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
```

This registration keeps `ILogger<T>` intact for controllers, services, middleware, and framework logs while routing accepted events through Yumalog's file-based pipeline.

Category-specific rules are matched by exact category name or prefix. More specific rules win. For example, a rule named `Microsoft` applies to `Microsoft.*`, while `Microsoft.Hosting` overrides that subset with a different minimum level.

**appsettings.json**

```json
{
    "Yumalog": {
        "ApplicationName": "MyApi",
        "BaseLogDirectory": "C:\\ServiceLogs",
        "MinimumLogLevel": "Information",
        "BlockWhenFull": false,
        "CategoryMinimumLogLevels": {
            "Microsoft": "Warning",
            "Microsoft.Hosting": "Information"
        }
    }
}
```

You can also bind a different section name, for example `builder.Logging.AddCorporateLogging(builder.Configuration, "Observability:Yumalog")`.

**OrderProcessorWorker.cs**

```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Yumalog.Abstractions;

public class OrderProcessorWorker : BackgroundService
{
    private readonly ICorporateLogger _logger;

    public OrderProcessorWorker(ICorporateLogger logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatch(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError("Batch processing failed", ex, new Dictionary<string, object>
                {
                    { "ErrorType", ex.GetType().Name }
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task ProcessBatch(CancellationToken ct)
    {
        var batchId = Guid.NewGuid();

        _logger.LogInformation("Starting batch", new Dictionary<string, object>
        {
            { "BatchId", batchId }
        });

        await Task.Delay(1000, ct);

        _logger.LogInformation("Batch completed", new Dictionary<string, object>
        {
            { "BatchId", batchId },
            { "ProcessedCount", 42 }
        });
    }
}
```

---

## API Reference

### ICorporateLogger Interface

All methods write through `Serilog.Sinks.Async`.
Under normal operation this keeps file I/O off the caller thread.
If `BlockWhenFull = true`, the caller can still wait temporarily when the async queue is saturated.

```csharp
void LogInformation(string message, IDictionary<string, object> properties = null);
void LogWarning(string message, IDictionary<string, object> properties = null);
void LogError(string message, Exception exception = null, IDictionary<string, object> properties = null);
void LogDebug(string message, IDictionary<string, object> properties = null);
void LogFatal(string message, Exception exception = null, IDictionary<string, object> properties = null);
void LogInformationObject(string message, object data);
```

For Dependency Injection usage, logger shutdown is container-managed. Consumers should not flush or dispose the logger manually during normal Windows Service execution.

### Examples

```csharp
// Information with properties
logger.LogInformation("User logged in", new Dictionary<string, object>
{
    { "UserId", "john.doe@company.com" },
    { "IPAddress", "192.168.1.100" }
});

// Warning
logger.LogWarning("High memory usage", new Dictionary<string, object>
{
    { "MemoryMB", 1500 }
});

// Error with exception
try
{
    // code that throws
}
catch (Exception ex)
{
    logger.LogError("Database connection failed", ex, new Dictionary<string, object>
    {
        { "RetryCount", 3 }
    });
}

// Debug
logger.LogDebug("Cache hit", new Dictionary<string, object>
{
    { "Key", "user:12345" }
});

// Fatal
logger.LogFatal("Configuration missing", new Exception("config.json not found"));

// Object logging
logger.LogInformationObject("Order received", new
{
    OrderId = 12345,
    CustomerId = "CUST-001",
    TotalAmount = 1500.50m
});
```

---

## Configuration Options

### CorporateLogConfiguration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApplicationName` | `string` | **Required** | Application identifier used for directory |
| `Environment` | `string` | Auto-detected | Environment name |
| `BaseLogDirectory` | `string` | `C:\ServiceLogs` | Root directory for application log folders |
| `RollingIntervalDays` | `int` | `1` | Daily rolling interval. Only `1` is currently supported. |
| `RetainedFileCountLimit` | `int` | `31` | Number of log files to keep (min: 1) |
| `FileSizeLimitBytes` | `long?` | `104857600` (100MB) | Max file size (min: 1MB) |
| `BufferSize` | `int` | `50000` | Async buffer capacity (1,000-500,000) |
| `BlockWhenFull` | `bool` | `true` | Block when buffer full to prevent log loss |
| `AsyncBufferMonitorInterval` | `TimeSpan` | `00:00:01` | Sampling interval for async buffer diagnostics |
| `AsyncBufferWarningUsageThresholdPercentage` | `int` | `80` | High-usage warning threshold for async buffer diagnostics |
| `DiagnosticListener` | `Action<CorporateLogDiagnosticEvent>` | `null` | Optional callback for logger lifecycle and buffer health events |

**Derived Property:**

| Property | Value | Description |
|----------|-------|-------------|
| `LogDirectory` | `{BaseLogDirectory}\{ApplicationName}` | Application-specific directory |

### Buffer Configuration

| Scenario | BufferSize | BlockWhenFull |
|----------|-----------|---------------|
| Low traffic (< 100 logs/sec) | `10000` | `true` |
| Normal traffic (100-1000 logs/sec) | `50000` | `true` |
| High traffic (> 1000 logs/sec) | `100000` | `true` |
| Performance critical | `50000` | `false` |

**Example:**

```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "HighVolumeService";
    config.BufferSize = 100000;
    config.BlockWhenFull = true;
    config.AsyncBufferWarningUsageThresholdPercentage = 75;
});
```

### Custom Log Directory

```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "PaymentsService";
    config.BaseLogDirectory = @"D:\ServiceLogs";
});
```

Yumalog validates the configured directory during logger creation.
If the folder cannot be created or written to, startup fails fast instead of silently losing logs later.

---

## Diagnostics

### What Diagnostics Are For

Yumalog diagnostics are infrastructure health signals, not business logs.

Use normal application logs for events such as:

- Order processed
- Batch failed
- Service started

Use diagnostics for questions such as:

- Did the logger shut down cleanly?
- Is the async queue approaching capacity?
- Were any messages dropped because the buffer overflowed?

Diagnostics are most useful during pilot rollout, load testing, production troubleshooting, and capacity tuning.

### How Diagnostics Work

If you provide a callback through `CorporateLogConfiguration.DiagnosticListener`, Yumalog invokes it when important lifecycle or buffer-health events occur.

```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "OrderService";
    config.DiagnosticListener = diagnostic =>
    {
        System.Diagnostics.Trace.WriteLine(
            $"{diagnostic.TimestampUtc:o} " +
            $"{diagnostic.EventType} " +
            $"{diagnostic.ApplicationName} " +
            $"{diagnostic.Message}");
    };
});
```

Legacy usage works the same way:

```csharp
CorporateLogManager.Initialize(new CorporateLogConfiguration
{
    ApplicationName = "LegacyService",
    DiagnosticListener = diagnostic =>
    {
        System.Diagnostics.Trace.WriteLine(
            $"{diagnostic.TimestampUtc:o} {diagnostic.EventType} {diagnostic.Message}");
    }
});
```

### Shutdown Diagnostics

These events are emitted when Yumalog is flushing buffered events and shutting down:

- `ShutdownStarted` - Logger shutdown began.
- `ShutdownCompleted` - Logger shutdown finished successfully.
- `ShutdownFailed` - Logger shutdown failed before completion.

These events tell you whether the logger completed an orderly shutdown.

### Async Buffer Diagnostics

These events are emitted by the async sink monitor:

- `AsyncBufferMonitoringStarted` - Async queue monitoring started.
- `AsyncBufferMonitoringStopped` - Async queue monitoring stopped.
- `AsyncBufferHighUsage` - Queue usage crossed the configured warning threshold.
- `AsyncBufferDroppedMessages` - The async queue dropped one or more events because it was full.

These events tell you whether the current buffer configuration is healthy under real traffic.

### Diagnostic Event Payload

Each diagnostic event includes the following information:

| Property | Description |
|----------|-------------|
| `EventType` | Type of diagnostic event |
| `ApplicationName` | Application that owns the logger |
| `LogDirectory` | Directory used by the file sink |
| `Message` | Human-readable description |
| `Exception` | Optional exception for failure events |
| `TimestampUtc` | UTC timestamp |
| `BufferSize` | Queue capacity, when applicable |
| `BufferCount` | Current queue depth, when applicable |
| `DroppedMessagesCount` | Total dropped messages observed, when applicable |

### Recommended Production Usage

Use a lightweight, non-throwing callback and route diagnostics to a separate operational channel such as `Trace`, Windows Event Log, an internal metric sink, or a health telemetry stream.

```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "OrdersService";
    config.BufferSize = 100000;
    config.BlockWhenFull = true;
    config.AsyncBufferWarningUsageThresholdPercentage = 80;
    config.AsyncBufferMonitorInterval = TimeSpan.FromSeconds(1);
    config.DiagnosticListener = diagnostic =>
    {
        System.Diagnostics.Trace.WriteLine(
            $"[{diagnostic.EventType}] " +
            $"App={diagnostic.ApplicationName}; " +
            $"Dir={diagnostic.LogDirectory}; " +
            $"Buffer={diagnostic.BufferCount}/{diagnostic.BufferSize}; " +
            $"Dropped={diagnostic.DroppedMessagesCount}; " +
            $"Message={diagnostic.Message}");
    };
});
```

### How To Interpret Diagnostics

#### During rollout

- Watch for `ShutdownCompleted` on controlled service stops.
- Watch for `AsyncBufferHighUsage` to identify services that need larger buffers.
- Treat `AsyncBufferDroppedMessages` as a sign that logs were lost under current settings.

#### During troubleshooting

- Missing tail logs: check whether `ShutdownStarted` and `ShutdownCompleted` were both emitted.
- Suspected overload: check for `AsyncBufferHighUsage` and `AsyncBufferDroppedMessages`.

### Important Guidance

- Keep the diagnostic callback lightweight.
- Do not throw exceptions from the callback.
- Do not call Yumalog again from inside the diagnostic callback.
- Treat diagnostics as infrastructure health signals, not business logs.

---

## Log File Structure

### Directory Layout

```
C:\ServiceLogs\
├── MyWindowsService\
│   ├── log-20240115.json
│   ├── log-20240116.json
│   └── log-20240117.json
├── MyWorkerService\
│   ├── log-20240115.json
│   └── log-20240116.json
└── AnotherApp\
    └── log-20240117.json
```

### JSON Log Format

Each log entry is a single-line JSON object:

```json
{
  "Timestamp": "2024-01-15T14:30:45.1234567Z",
  "Level": "Information",
  "MessageTemplate": "Processing order",
  "RenderedMessage": "Processing order",
  "Properties": {
    "Application": "MyWindowsService",
    "Environment": "Production",
    "MachineName": "PROD-SERVER-01",
    "ProcessId": 1234,
    "OrderId": 12345,
    "CustomerId": "CUST-001",
    "Amount": 1500.50
  }
}
```

### Error Log with Exception

```json
{
  "Timestamp": "2024-01-15T14:35:12.9876543Z",
  "Level": "Error",
  "MessageTemplate": "Database operation failed",
  "RenderedMessage": "Database operation failed",
  "Exception": "System.Data.SqlClient.SqlException: Timeout expired...",
  "Properties": {
    "Application": "MyWindowsService",
    "Environment": "Production",
    "MachineName": "PROD-SERVER-01",
    "ProcessId": 1234,
    "OperationId": "OP-98765",
    "RetryCount": 3
  }
}
```

---

## Grafana Alloy Configuration

Install Grafana Alloy once per server to collect logs from all applications.

## Observability Stack Setup

Yumalog does not require Docker, Loki, Grafana, or Alloy in order to write logs.
Those components are only needed when you want to collect, centralize, and visualize the generated JSON files.

There are two common setup models:

1. Docker-based local validation
Use this when you want a fast demo environment on your development machine.
Run only the observability stack in Docker: Loki, Grafana, and Alloy.
Your Windows Service or Worker Service projects continue running normally on the host machine and write logs to the host file system.

2. Direct server installation
Use this in a real server environment.
Install Grafana Alloy on each Windows server that produces logs, and install Loki and Grafana either centrally on dedicated servers or through your existing observability platform.

### Recommended Local Demo Setup With Docker

For a local demo or management presentation, the simplest model is:

1. Run your Yumalog-enabled Windows Services on the host machine.
2. Let them write JSON files under `C:\ServiceLogs`.
3. Run Loki, Grafana, and Alloy in Docker.
4. Mount `C:\ServiceLogs` into the Alloy container as a read-only volume.
5. Configure Alloy to tail the JSON files and push them to Loki.
6. Use Grafana Explore to query and filter the logs.

This keeps the demo close to the real target architecture while avoiding extra installation effort on your local machine.

### Docker-Based Installation Summary

If you are setting up the observability stack with Docker for the first time, create these components:

1. `docker-compose.yml`
Starts `loki`, `grafana`, and `alloy` containers.

2. `config.alloy`
Tells Alloy which JSON files to read and how to extract labels.

3. `loki-config.yaml`
Configures Loki storage and HTTP endpoints.

4. Grafana datasource provisioning file
Automatically points Grafana to the Loki container.

Important Docker note for Windows:
The host path containing Yumalog logs must be shared with Docker Desktop and mounted into the Alloy container, for example:

```yaml
volumes:
    - C:/ServiceLogs:/var/service-logs:ro
```

Once the stack is running, Alloy reads the host-generated Yumalog log files and forwards them to Loki.

### Real Server Installation Summary

In a real production deployment, Docker is optional.
The more typical Windows Server model is:

1. Deploy Yumalog-enabled services to the server.
2. Ensure services write to a known directory such as `C:\ServiceLogs`.
3. Install Grafana Alloy directly on the server as a Windows service.
4. Configure Alloy to watch the Yumalog log directory.
5. Send logs to a central Loki instance.
6. Use Grafana against that Loki instance for queries, dashboards, and troubleshooting.

### What Needs To Be Installed Where

#### On each application server

- Yumalog-enabled Windows Services or Worker Services
- Grafana Alloy
- Access to the configured local log directory

#### On the central observability side

- Grafana Loki
- Grafana

### Direct Installation Guidance

If you do not want to use Docker in production:

1. Install Grafana Alloy on the Windows server.
2. Configure Alloy with a file source that watches `C:\ServiceLogs\*\*.json`.
3. Point Alloy to your central Loki endpoint.
4. Configure Grafana with the Loki datasource.

The exact installation steps can vary by operating system version, service account model, and company standards, so use the official documentation as the base reference for the agent and UI components:

- Grafana Alloy installation and configuration
- Grafana Loki deployment and storage configuration
- Grafana datasource and dashboard setup

### Operational Notes

Regardless of whether you use Docker or direct installation:

1. Yumalog is responsible only for producing structured JSON files and diagnostics.
2. Alloy is responsible for reading the files and forwarding them.
3. Loki is responsible for storing and indexing the logs.
4. Grafana is responsible for querying and visualizing them.

This separation is intentional and matches the expected enterprise deployment model.

### alloy-config.alloy

```hcl
// Watch all JSON files under C:\ServiceLogs
local.file_match "service_logs" {
  path_targets = [{
    __path__ = "C:/ServiceLogs/*/*.json"
  }]
}

// Read files with position tracking
loki.source.file "service_logs" {
  targets    = local.file_match.service_logs.targets
  forward_to = [loki.process.parse_json.receiver]
}

// Parse JSON and extract labels
loki.process "parse_json" {
  stage.json {
    expressions = {
      timestamp   = "Timestamp",
      level       = "Level",
      message     = "RenderedMessage",
      application = "Properties.Application",
      environment = "Properties.Environment",
      machine     = "Properties.MachineName",
      exception   = "Exception",
    }
  }

  stage.labels {
    values = {
      app   = "application",
      env   = "environment",
      host  = "machine",
      level = "level",
    }
  }

  stage.timestamp {
    source = "timestamp"
    format = "RFC3339"
  }

  forward_to = [loki.write.default.receiver]
}

// Send to Loki
loki.write "default" {
  endpoint {
    url = "http://loki-server:3100/loki/api/v1/push"
  }
}
```

### Verify Configuration

```powershell
# Check Alloy service
Get-Service "Grafana Alloy"

# Verify log files
Get-ChildItem "C:\ServiceLogs" -Recurse -Filter "*.json"

# Test logging
CorporateLogManager.Initialize("TestApp");
CorporateLogManager.Current.LogInformation("Test message");
CorporateLogManager.Shutdown();
```

---

## Grafana Queries (LogQL)

### Basic Queries

```logql
# All logs from specific app
{app="MyWindowsService"}

# Errors only
{app="MyWindowsService", level="Error"}

# Specific environment
{env="Production"} 

# Multiple filters
{app="MyWindowsService", env="Production", level="Error"}

# Search in message
{app="MyWindowsService"} |= "order"
```

### Advanced Queries

```logql
# Count errors per minute
rate({app="MyWindowsService", level="Error"}[1m])

# Top 10 error messages
topk(10, count_over_time({level="Error"}[1h]) by (app))

# JSON field extraction
{app="MyWindowsService"} | json | Properties_OrderId = "12345"
```

---

## Best Practices

### Do's

```csharp
// Use structured properties
logger.LogInformation("Order processed", new Dictionary<string, object>
{
    { "OrderId", orderId },
    { "Amount", amount }
});

// Initialize at startup
static void Main()
{
    CorporateLogManager.Initialize("MyApp");
    // application code
}

// Always flush before exit
finally
{
    CorporateLogManager.Shutdown();
}

// Log exceptions with context
catch (Exception ex)
{
    logger.LogError("Failed to process order", ex, new Dictionary<string, object>
    {
        { "OrderId", orderId }
    });
}

// Use object logging for complex data
logger.LogInformationObject("Order details", new
{
    Order = order,
    Customer = customer
});

// Use diagnostics during rollout and operations
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "MyApp";
    config.DiagnosticListener = diagnostic =>
    {
        System.Diagnostics.Trace.WriteLine(
            $"{diagnostic.TimestampUtc:o} {diagnostic.EventType} {diagnostic.Message}");
    };
});
```

### Don'ts

```csharp
// Avoid string concatenation
logger.LogInformation($"Order {orderId} processed");  // Not searchable

// Don't forget to flush
Main()
{
    CorporateLogManager.Initialize("MyApp");
    // Missing: CorporateLogManager.Shutdown();
}

// Don't log sensitive data
logger.LogInformation("Login", new Dictionary<string, object>
{
    { "Password", password }  // Never log passwords
});

// Don't log in tight loops
for (int i = 0; i < 1000000; i++)
{
    logger.LogDebug($"Item {i}");  // Too many logs
}

// Don't initialize multiple times
CorporateLogManager.Initialize("App1");
CorporateLogManager.Initialize("App2");  // Throws exception

// Don't do heavy or recursive work inside the diagnostic callback
config.DiagnosticListener = diagnostic =>
{
    logger.LogInformation("Diagnostic callback");  // Avoid logging through Yumalog here
};
```

---

## Troubleshooting

### Logs Not Appearing in Grafana

**Check Alloy service:**
```powershell
Get-Service "Grafana Alloy"
```

**Verify log files exist:**
```powershell
Get-ChildItem "C:\ServiceLogs\MyApp"
```

**Check Alloy configuration path:**
```hcl
__path__ = "C:/ServiceLogs/*/*.json"
```

**Test Loki connectivity:**
```powershell
Invoke-WebRequest -Uri "http://loki-server:3100/ready"
```

---

### Application Can't Write Logs

**Grant write permissions:**
```powershell
icacls "C:\ServiceLogs" /grant "NT AUTHORITY\NETWORK SERVICE:(OI)(CI)M"
```

---

### High Memory Usage

**Reduce buffer size:**
```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "MyApp";
    config.BufferSize = 10000;
    config.BlockWhenFull = true;
});
```

**Or inspect diagnostics before changing durability-related settings:**

```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "MyApp";
    config.DiagnosticListener = diagnostic =>
    {
        if (diagnostic.EventType == CorporateLogDiagnosticEventType.AsyncBufferHighUsage)
        {
            System.Diagnostics.Trace.WriteLine(
                $"Buffer pressure detected: {diagnostic.BufferCount}/{diagnostic.BufferSize}");
        }
    };
});
```

If `AsyncBufferHighUsage` appears frequently, increase `BufferSize` or reduce log volume.
If `AsyncBufferDroppedMessages` appears, logs were lost because the queue was full.

---

### Verifying Logger Shutdown

**Use diagnostics to confirm orderly shutdown:**

```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "MyApp";
    config.DiagnosticListener = diagnostic =>
    {
        if (diagnostic.EventType == CorporateLogDiagnosticEventType.ShutdownCompleted)
        {
            System.Diagnostics.Trace.WriteLine("Logger shutdown completed successfully.");
        }
    };
});
```

---

### Duplicate Logs

**Initialize only once:**
```csharp
if (!CorporateLogManager.IsInitialized)
{
    CorporateLogManager.Initialize("MyApp");
}
```

---

## Testing

### Unit Test Example

```csharp
using Xunit;
using Yumalog;

public class LoggingTests : IDisposable
{
    [Fact]
    public void Initialize_WithValidAppName_Success()
    {
        CorporateLogManager.Initialize("TestApp", "Development");

        Assert.True(CorporateLogManager.IsInitialized);

        var logger = CorporateLogManager.Current;
        logger.LogInformation("Test log");

        CorporateLogManager.Shutdown();
    }

    [Fact]
    public void Initialize_WithEmptyAppName_ThrowsException()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            CorporateLogManager.Initialize("");
        });
    }

    public void Dispose()
    {
        if (CorporateLogManager.IsInitialized)
        {
            CorporateLogManager.Shutdown();
        }
    }
}
```

---

## Performance

| Scenario | Throughput | Notes |
|----------|-----------|-------|
| Single log call | ~50k logs/sec | Non-blocking write to buffer |
| Buffer flush | ~5k logs/sec | Background thread writes to disk |
| High burst | Instant | Buffered in memory |

---

## Security Considerations

**Never log sensitive data:**
```csharp
// Bad
logger.LogInformation("Login", new Dictionary<string, object>
{
    { "Password", password }
});

// Good
logger.LogInformation("Login", new Dictionary<string, object>
{
    { "Username", username },
    { "Success", true }
});
```

**Sanitize connection strings:**
```csharp
var sanitized = connectionString.Replace(password, "***");
logger.LogError("DB error", ex, new Dictionary<string, object>
{
    { "ConnectionString", sanitized }
});
```

**Set file permissions:**
```powershell
icacls "C:\ServiceLogs" /inheritance:r
icacls "C:\ServiceLogs" /grant "Administrators:(OI)(CI)F"
icacls "C:\ServiceLogs" /grant "ServiceAccount:(OI)(CI)M"
```

---

## Resources

- [Serilog Documentation](https://serilog.net/)
- [Grafana Alloy Documentation](https://grafana.com/docs/alloy/)
- [Grafana Loki Documentation](https://grafana.com/docs/loki/)
- [LogQL Query Language](https://grafana.com/docs/loki/latest/logql/)

---

## Contributing

Contributions are welcome. Open an issue or submit a pull request.

### Development Setup

```bash
git clone https://github.com/AdylshaY/Yumalog.git
cd Yumalog
dotnet restore
dotnet build
dotnet test
```

---

## License

MIT License - See LICENSE file for details

---

## Acknowledgments

Built with [Serilog](https://serilog.net/) and designed for [Grafana Loki](https://grafana.com/oss/loki/)

---
