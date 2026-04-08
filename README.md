# Yumalog - Centralized Structured Logging

Centralized logging library for .NET Framework and .NET Core applications with Grafana Loki integration via Grafana Alloy agent.

---

## Overview

Yumalog is a structured logging library for distributed systems running on Windows Servers. It provides a unified logging interface for both legacy .NET Framework applications and modern .NET Core/.NET 5+ applications.

**Architecture:** Applications write logs to JSON files locally (`C:\ServiceLogs\{ApplicationName}`), then Grafana Alloy agent collects and forwards them to Loki for centralized monitoring via Grafana.

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

### Manual Build

```bash
git clone https://github.com/AdylshaY/Yumalog.git
cd Yumalog
dotnet pack --configuration Release
dotnet add package Yumalog --source ./Yumalog/bin/Release
```

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
        });

        services.AddHostedService<OrderProcessorWorker>();
    })
    .Build();

await host.RunAsync();
```

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

All methods are non-blocking (uses `Serilog.Sinks.Async` internally).

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
| `RollingIntervalDays` | `int` | `1` | Log file rolling interval |
| `RetainedFileCountLimit` | `int` | `31` | Number of log files to keep (min: 1) |
| `FileSizeLimitBytes` | `long?` | `104857600` (100MB) | Max file size (min: 1MB) |
| `BufferSize` | `int` | `50000` | Async buffer capacity (1,000-500,000) |
| `BlockWhenFull` | `bool` | `true` | Block when buffer full to prevent log loss |

**Read-Only Properties:**

| Property | Value | Description |
|----------|-------|-------------|
| `BaseLogDirectory` | `C:\ServiceLogs` | Base directory for all logs |
| `LogDirectory` | `C:\ServiceLogs\{ApplicationName}` | Application-specific directory |

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
});
```

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
