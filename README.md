# Yumalog - Centralized Structured Logging

Enterprise-grade centralized logging library for .NET Framework and .NET Core applications with **Grafana Loki** integration via **Grafana Alloy** agent.

---

## 🎯 Overview

Yumalog is a structured logging library designed for distributed financial systems running on Windows Servers. It provides a unified logging interface for both legacy .NET Framework Windows Services and modern .NET Core/.NET 8 Worker Services.

**Key Principle:** Applications write logs **locally** to JSON files in isolation, then **Grafana Alloy** agent collects and forwards them to **Loki** for centralized analysis via **Grafana**.

---

## ✨ Features

- ✅ **.NET Standard 2.0** - Compatible with .NET Framework 4.6.1+ and .NET Core 2.0+/.NET 5+/.NET 8+
- 🎯 **Dual API** - Static manager for legacy apps, Dependency Injection extensions for modern apps
- 📝 **Structured Logging** - JSON format with key-value pairs (prevents string concatenation)
- 🔒 **Zero Data Loss Option** - Configurable buffer with blocking mode to ensure all logs are written
- 🏷️ **Auto-Enrichment** - Application name, environment, machine name, process ID automatically added
- 🔐 **File Isolation** - Each application writes to `C:\CorporateLogs\{ApplicationName}\` to prevent locking issues
- ⚡ **High Performance** - Async sink with 50,000 message buffer (all operations are non-blocking)
- 🛡️ **Graceful Shutdown** - Ensures all buffered logs are flushed before application exit
- 🔧 **Configurable** - Buffer size, file retention, rolling interval, and more
- 🚫 **No String Concatenation** - Forces structured logging with properties or objects

---

## 🏗️ Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│  Application (.NET Framework / .NET Core / .NET 8)              │
│  └─→ Yumalog Library                                            │
│      └─→ Serilog.Sinks.Async (50k buffer, non-blocking)        │
│          └─→ JSON File (C:\CorporateLogs\{AppName}\log-.json)  │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ↓ (File watching)
┌─────────────────────────────────────────────────────────────────┐
│  Grafana Alloy (Agent) - Installed once per server             │
│  - Watches: C:\CorporateLogs\*\*.json                          │
│  - Parses JSON and extracts labels                              │
│  - Tracks file offset (survives network outages)                │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ↓ (HTTP Push API)
┌─────────────────────────────────────────────────────────────────┐
│  Grafana Loki (Central Log Database)                            │
│  - Stores logs with labels (app, env, host, level)             │
│  - Efficient compression and retention                           │
└────────────────────────┬────────────────────────────────────────┘
                         │
                         ↓ (Query)
┌─────────────────────────────────────────────────────────────────┐
│  Grafana (Visualization & Dashboards)                           │
│  - LogQL queries, filtering, alerting                           │
└─────────────────────────────────────────────────────────────────┘
```

---

## 📦 Installation

### From Azure Artifacts (Private Feed)

```bash
dotnet nuget add source https://your-org.pkgs.visualstudio.com/_packaging/YourFeed/nuget/v3/index.json --name AzureArtifacts

dotnet add package Yumalog
```

### Manual Installation

```bash
# Clone repository
git clone https://github.com/AdylshaY/Yumalog.git

# Build and pack
cd Yumalog
dotnet pack --configuration Release

# Install locally
dotnet add package Yumalog --source ./Yumalog/bin/Release
```

---

## 🚀 Quick Start

### Option 1: Legacy Applications (.NET Framework, Windows Services)

Perfect for existing Windows Services without Dependency Injection.

```csharp
using System;
using System.Collections.Generic;
using Yumalog;

class Program
{
    static void Main()
    {
        // Initialize at application startup
        CorporateLogManager.Initialize("MyWindowsService", "Production");

        try
        {
            var logger = CorporateLogManager.Current;

            logger.LogInformation("Service started successfully");

            // Process work...
            ProcessOrders(logger);
        }
        catch (Exception ex)
        {
            var logger = CorporateLogManager.Current;
            logger.LogFatal("Service crashed", ex);
        }
        finally
        {
            // Critical: Flush logs before exit
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

### Option 2: Modern Applications (.NET Core/.NET 8 Worker Services)

Uses built-in Dependency Injection.

#### **Program.cs**

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Yumalog.Extensions;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        // Simple initialization
        services.AddCorporateLogging("MyWorkerService", "Production");

        // OR with custom configuration
        services.AddCorporateLogging(config =>
        {
            config.ApplicationName = "MyWorkerService";
            config.Environment = "Production";
            config.BufferSize = 100000;              // 100k for high-volume apps
            config.BlockWhenFull = true;              // Zero-data-loss guarantee
            config.RetainedFileCountLimit = 60;       // Keep 60 days
            config.FileSizeLimitBytes = 200 * 1024 * 1024; // 200MB per file
        });

        services.AddHostedService<OrderProcessorWorker>();
    })
    .Build();

await host.RunAsync();
```

#### **OrderProcessorWorker.cs**

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
                    { "ErrorType", ex.GetType().Name },
                    { "Timestamp", DateTime.UtcNow }
                });
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }

        // Flush remaining logs
        _logger.FlushAndShutdown();
    }

    private async Task ProcessBatch(CancellationToken ct)
    {
        var batchId = Guid.NewGuid();

        _logger.LogInformation("Starting batch", new Dictionary<string, object>
        {
            { "BatchId", batchId },
            { "StartTime", DateTime.UtcNow }
        });

        // Business logic...
        await Task.Delay(1000, ct);

        _logger.LogInformation("Batch completed", new Dictionary<string, object>
        {
            { "BatchId", batchId },
            { "ProcessedCount", 42 },
            { "Duration", 1.2 }
        });
    }
}
```

---

## 📚 API Reference

### ICorporateLogger Interface

All methods are **non-blocking** (uses `Serilog.Sinks.Async` internally).

```csharp
void LogInformation(string message, IDictionary<string, object> properties = null);
void LogWarning(string message, IDictionary<string, object> properties = null);
void LogError(string message, Exception exception = null, IDictionary<string, object> properties = null);
void LogDebug(string message, IDictionary<string, object> properties = null);
void LogFatal(string message, Exception exception = null, IDictionary<string, object> properties = null);
void LogInformationObject(string message, object data);
void FlushAndShutdown();
```

#### Examples

```csharp
// Information with properties
logger.LogInformation("User logged in", new Dictionary<string, object>
{
    { "UserId", "john.doe@company.com" },
    { "LoginTime", DateTime.UtcNow },
    { "IPAddress", "192.168.1.100" }
});

// Warning
logger.LogWarning("High memory usage detected", new Dictionary<string, object>
{
    { "MemoryMB", 1500 },
    { "Threshold", 1024 }
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
        { "ConnectionString", "Server=...(sanitized)" },
        { "RetryCount", 3 }
    });
}

// Debug (only if minimum level is Debug)
logger.LogDebug("Cache hit", new Dictionary<string, object>
{
    { "Key", "user:12345" },
    { "HitRate", 0.85 }
});

// Fatal (critical failures)
logger.LogFatal("Configuration file missing", new Exception("config.json not found"));

// Object logging (automatic destructuring)
logger.LogInformationObject("Order received", new
{
    OrderId = 12345,
    CustomerId = "CUST-001",
    Items = new[] { "Item1", "Item2" },
    TotalAmount = 1500.50m
});
```

---

## ⚙️ Configuration Options

### CorporateLogConfiguration

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ApplicationName` | `string` | **Required** | Used for directory and labeling (no special chars) |
| `Environment` | `string` | Auto-detected | `Development`, `Staging`, `Production` |
| `RollingIntervalDays` | `int` | `1` | Daily log file rolling |
| `RetainedFileCountLimit` | `int` | `31` | Keep last 31 files |
| `FileSizeLimitBytes` | `long?` | `104857600` (100MB) | Max file size before rolling |
| `BufferSize` | `int` | `50000` | Async buffer capacity (messages) |
| `BlockWhenFull` | `bool` | `true` | Block when buffer full to prevent log loss |

**Read-Only Properties:**

| Property | Value | Description |
|----------|-------|-------------|
| `BaseLogDirectory` | `C:\CorporateLogs` | Fixed corporate standard |
| `LogDirectory` | `C:\CorporateLogs\{ApplicationName}` | Application-specific directory |

---

### Buffer Configuration Guidance

| Scenario | BufferSize | BlockWhenFull | Rationale |
|----------|-----------|---------------|-----------|
| **Low Traffic** (< 100 logs/sec) | `10000` | `true` | Small buffer, guaranteed delivery |
| **Normal Traffic** (100-1000 logs/sec) | `50000` | `true` | Default - balanced |
| **High Traffic** (> 1000 logs/sec) | `100000` | `true` | Large buffer for bursts |
| **Performance Critical** | `50000` | `false` | ⚠️ Risk: drops logs if buffer fills |

**Example:**

```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "HighVolumeService";
    config.BufferSize = 100000;      // Handle 100k message bursts
    config.BlockWhenFull = true;      // Never lose logs
});
```

---

## 📁 Log File Structure

### Directory Layout

```
C:\CorporateLogs\
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

## 🔧 Grafana Alloy Configuration

Install **Grafana Alloy** once per server to collect logs from all applications.

### Installation

```powershell
# Download from Grafana website
# Install as Windows Service

# Configure alloy-config.alloy
```

### alloy-config.alloy

```hcl
// Watch all JSON files under C:\CorporateLogs
local.file_match "corporate_logs" {
  path_targets = [{
    __path__ = "C:/CorporateLogs/*/*.json"
  }]
}

// Read files with position tracking
loki.source.file "corporate_logs" {
  targets    = local.file_match.corporate_logs.targets
  forward_to = [loki.process.parse_json.receiver]
}

// Parse JSON and extract labels
loki.process "parse_json" {
  // Extract fields from JSON
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

  // Create Loki labels
  stage.labels {
    values = {
      app   = "application",
      env   = "environment",
      host  = "machine",
      level = "level",
    }
  }

  // Parse timestamp
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

    // Optional: Basic auth
    // basic_auth {
    //   username = "loki"
    //   password = "password"
    // }
  }
}
```

### Verify Alloy is Working

```powershell
# Check Alloy service status
Get-Service "Grafana Alloy"

# View Alloy logs
Get-Content "C:\Program Files\GrafanaLabs\Alloy\data\alloy.log" -Tail 50

# Test with a sample log
CorporateLogManager.Initialize("TestApp");
CorporateLogManager.Current.LogInformation("Test from Alloy setup");
CorporateLogManager.Shutdown();
```

---

## 📊 Grafana Queries (LogQL)

### Basic Queries

```logql
# All logs from specific app
{app="MyWindowsService"}

# Errors only
{app="MyWindowsService"} |= "Error"

# Specific environment
{env="Production"} 

# Multiple filters
{app="MyWindowsService", env="Production", level="Error"}

# Search in message
{app="MyWindowsService"} |= "order" |= "failed"
```

### Advanced Queries

```logql
# Count errors per minute
rate({app="MyWindowsService", level="Error"}[1m])

# Top 10 error messages
topk(10, 
  count_over_time({app="MyWindowsService", level="Error"}[1h])
)

# JSON field extraction
{app="MyWindowsService"} 
  | json 
  | Properties_OrderId = "12345"
```

---

## 🎯 Best Practices

### ✅ DO's

```csharp
// ✅ Use structured properties
logger.LogInformation("Order processed", new Dictionary<string, object>
{
    { "OrderId", orderId },
    { "Amount", amount },
    { "Duration", duration }
});

// ✅ Always initialize at startup
static void Main()
{
    CorporateLogManager.Initialize("MyApp");
    // ... application code
}

// ✅ Always flush before exit
finally
{
    CorporateLogManager.Shutdown();
}

// ✅ Log exceptions with context
catch (Exception ex)
{
    logger.LogError("Failed to process order", ex, new Dictionary<string, object>
    {
        { "OrderId", orderId },
        { "CustomerId", customerId }
    });
}

// ✅ Use object logging for complex data
logger.LogInformationObject("Order details", new
{
    Order = order,
    Customer = customer,
    Items = items
});
```

---

### ❌ DON'Ts

```csharp
// ❌ String concatenation (not searchable)
logger.LogInformation($"Order {orderId} processed with amount {amount}");

// ❌ Forgetting to flush
Main()
{
    CorporateLogManager.Initialize("MyApp");
    // ... code
    // ❌ Missing: CorporateLogManager.Shutdown();
}

// ❌ Logging sensitive data
logger.LogInformation("User login", new Dictionary<string, object>
{
    { "Password", password }  // ❌ Never log passwords!
});

// ❌ Logging in tight loops without throttling
for (int i = 0; i < 1000000; i++)
{
    logger.LogDebug($"Processing item {i}");  // ❌ Too many logs!
}

// ❌ Initializing multiple times
CorporateLogManager.Initialize("App1");
CorporateLogManager.Initialize("App2");  // ❌ Throws exception
```

---

## 🛠️ Troubleshooting

### Issue: Logs Not Appearing in Grafana

**Symptoms:** Application writes logs but Grafana shows nothing.

**Checklist:**
1. ✅ Check Alloy service is running:
   ```powershell
   Get-Service "Grafana Alloy"
   ```

2. ✅ Verify log files exist:
   ```powershell
   Get-ChildItem "C:\CorporateLogs\MyApp"
   ```

3. ✅ Check Alloy configuration path matches:
   ```hcl
   __path__ = "C:/CorporateLogs/*/*.json"  # Must match
   ```

4. ✅ Review Alloy logs for errors:
   ```powershell
   Get-Content "C:\Program Files\GrafanaLabs\Alloy\data\alloy.log" -Tail 100
   ```

5. ✅ Test Loki connectivity:
   ```powershell
   Invoke-WebRequest -Uri "http://loki-server:3100/ready"
   ```

---

### Issue: Application Can't Write Logs

**Symptoms:** `UnauthorizedAccessException` or empty log directory.

**Solution:**
```powershell
# Grant write permissions to service account
icacls "C:\CorporateLogs" /grant "NT AUTHORITY\NETWORK SERVICE:(OI)(CI)M"

# Or for specific app identity
icacls "C:\CorporateLogs" /grant "DOMAIN\ServiceAccount:(OI)(CI)M"
```

---

### Issue: High Memory Usage

**Symptoms:** Application consumes excessive RAM.

**Cause:** Buffer size too large or logs not being flushed.

**Solution:**
```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "MyApp";
    config.BufferSize = 10000;  // Reduce from 50k to 10k
    config.BlockWhenFull = true;
});
```

---

### Issue: Duplicate Logs

**Symptoms:** Same log appears multiple times in Grafana.

**Causes:**
- Multiple `Initialize()` calls
- Mixing static manager and DI in same app
- Multiple Alloy instances reading same files

**Solution:**
```csharp
// ✅ Initialize only once
if (!CorporateLogManager.IsInitialized)
{
    CorporateLogManager.Initialize("MyApp");
}

// ✅ Use only one approach (static OR DI, not both)
```

---

### Issue: Logs Lost During High Traffic

**Symptoms:** Missing log entries during peak load.

**Cause:** `BlockWhenFull = false` with insufficient buffer.

**Solution:**
```csharp
services.AddCorporateLogging(config =>
{
    config.ApplicationName = "MyApp";
    config.BufferSize = 100000;      // Increase buffer
    config.BlockWhenFull = true;      // Guarantee delivery
});
```

---

## 🧪 Testing

### Unit Test Example

```csharp
using Xunit;
using Yumalog;
using Yumalog.Configuration;

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

## 📈 Performance Metrics

| Scenario | Throughput | Latency | Notes |
|----------|-----------|---------|-------|
| Single log call | ~50k logs/sec | < 1ms | Non-blocking write to buffer |
| Buffer flush | ~5k logs/sec | N/A | Background thread writes to disk |
| High burst (50k logs) | Instant | 0ms | Buffered in memory, flushed async |
| Buffer full (blockWhenFull=true) | Blocks | Variable | Waits for disk I/O |

---

## 🔐 Security Considerations

### ✅ Best Practices

1. **Never log sensitive data:**
   ```csharp
   // ❌ Bad
   logger.LogInformation("Login", new Dictionary<string, object>
   {
       { "Password", password }
   });

   // ✅ Good
   logger.LogInformation("Login", new Dictionary<string, object>
   {
       { "Username", username },
       { "Success", true }
   });
   ```

2. **Sanitize connection strings:**
   ```csharp
   var sanitized = connectionString.Replace(password, "***");
   logger.LogError("DB error", ex, new Dictionary<string, object>
   {
       { "ConnectionString", sanitized }
   });
   ```

3. **File permissions:**
   ```powershell
   # Restrict access to log directory
   icacls "C:\CorporateLogs" /inheritance:r
   icacls "C:\CorporateLogs" /grant "Administrators:(OI)(CI)F"
   icacls "C:\CorporateLogs" /grant "ServiceAccount:(OI)(CI)M"
   ```

---

## 📖 Additional Resources

- [Serilog Documentation](https://serilog.net/)
- [Grafana Alloy Documentation](https://grafana.com/docs/alloy/)
- [Grafana Loki Documentation](https://grafana.com/docs/loki/)
- [LogQL Query Language](https://grafana.com/docs/loki/latest/logql/)

---

## 🤝 Contributing

Contributions are welcome! Please open an issue or submit a pull request.

### Development Setup

```bash
git clone https://github.com/AdylshaY/Yumalog.git
cd Yumalog
dotnet restore
dotnet build
dotnet test
```

---

## 🙏 Acknowledgments

- [Serilog](https://serilog.net/) - Excellent structured logging library
- [Grafana Labs](https://grafana.com/) - Loki and Alloy tools

---
