# Propel.FeatureFlags.Redis

[![Build and Test](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.Redis.svg)](https://www.nuget.org/packages/Propel.FeatureFlags.Redis/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Propel.FeatureFlags.Redis.svg?style=flat-square)](https://www.nuget.org/packages/Propel.FeatureFlags.Redis/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-standard-versions)

Redis-based distributed caching provider for Propel.FeatureFlags with optional two-level caching architecture for optimal performance and resilience.

## Features

- **Two-Level Caching** - In-memory L1 cache + Redis L2 cache for maximum performance
- **Circuit Breaker Pattern** - Automatic fallback when Redis is unavailable
- **Connection Resilience** - Automatic reconnection and exponential backoff
- **Cache Stampede Prevention** - Jitter-based expiration to avoid thundering herd
- **Sliding Expiration** - Frequently accessed flags stay cached longer
- **Production-Ready** - Optimized for high-throughput, low-latency scenarios
- **Monitoring Support** - Built-in logging and connection event tracking

## Installation
```bash
dotnet add package Propel.FeatureFlags.Redis
```


## Caching Architecture

### Two-Level Caching Strategy

When Redis is enabled, Propel.FeatureFlags uses a two-tier caching architecture:

```markdown
┌─────────────────────────────────────────────────────┐
│  Application Request                                │
└─────────────────┬───────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────┐
│  L1: In-Memory Cache (per instance)                 │
│    • Ultra-fast (microseconds)                      │
│    • Short TTL (10-15 seconds default)              │
│    • Size-limited (1000 flags default)              │
│    • No network calls                               │
└─────────────────┬───────────────────────────────────┘
                  │ Cache Miss
                  ▼
┌─────────────────────────────────────────────────────┐
│  L2: Redis Distributed Cache (shared)               │
│    • Fast (milliseconds)                            │
│    • Longer TTL (60 minutes default)                │
│    • Shared across all instances                    │
│    • Sliding expiration for hot flags               │
└─────────────────┬───────────────────────────────────┘
                  │ Cache Miss
                  ▼
┌─────────────────────────────────────────────────────┐
│  Database (PostgreSQL / SQL Server)                 │
│    • Persistent storage                             │
│    • Source of truth                                │
└─────────────────────────────────────────────────────┘
```


**Benefits:**
- **99%+ cache hit rate** - Most requests served from L1 cache (microseconds)
- **Reduced Redis load** - L1 cache absorbs traffic spikes
- **Cross-instance consistency** - L2 cache ensures all instances see updates within seconds
- **Automatic failover** - Circuit breaker falls back to L1 cache if Redis is down

### Cache Keys and Prefixes

All cache keys are prefixed with `propel-flags` and use structured namespacing:
```csharp
// Global flags (shared across all applications) 
    propel-flags:global:{flagKey}
// Application-scoped flags (specific to app and version) 
    propel-flags:app:{applicationName}:{flagKey} 
    propel-flags:app:{applicationName}:{version}:{flagKey}
```

**Examples:**
- `propel-flags:global:maintenance-mode`
- `propel-flags:app:MyWebApi:new-checkout-flow`
- `propel-flags:app:MyWebApi:v2-0-0:enhanced-search`

This structure prevents key collisions and enables:
- Clear separation between global and application flags
- Version-specific feature rollouts
- Easy cache invalidation by pattern

## Quick Start

### Basic Configuration (Two-Level Caching)

```csharp
using Propel.FeatureFlags.Redis;
var builder = WebApplication.CreateBuilder(args);
builder.Services 
    .ConfigureFeatureFlags(config => 
    { 
        config.RegisterFlagsWithContainer = true; 
        config.EnableFlagFactory = true;
        // ⚠️ IMPORTANT: Do NOT set LocalCacheConfiguration when using Redis
        // Redis provider manages both L1 and L2 caching internally
    })
    .AddPostgreSqlFeatureFlags(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddRedisCache(builder.Configuration.GetConnectionString("RedisConnection")!);

var app = builder.Build(); 
await app.AutoDeployFlags(); 
app.Run();
```

### Advanced Configuration
```csharp
builder.Services 
    .ConfigureFeatureFlags(config =>
    { 
        config.RegisterFlagsWithContainer = true;
        config.EnableFlagFactory = true;
        // ⚠️ Do NOT configure LocalCacheConfiguration here when using Redis
    })
    .AddPostgreSqlFeatureFlags(postgresConnectionString)
    .AddRedisCache(redisConnectionString, options =>
    {
        // L1 Cache (In-Memory) Configuration
        options.EnableInMemoryCache = true;        // Enable L1 cache (default: true)
        options.LocalCacheSizeLimit = 2000;        // Max flags in memory (default: 1000)
        options.LocalCacheDurationSeconds = 15;    // L1 TTL in seconds (default: 10)
    
        // L2 Cache (Redis) Configuration
        options.CacheDurationInMinutes = 120;      // Redis TTL in minutes (default: 60)
    
        // Circuit Breaker Configuration
        options.CircuitBreakerThreshold = 5;       // Failures before opening (default: 3)
        options.CircuitBreakerDurationSeconds = 30; // Open duration (default: 30)
    
        // Connection Settings
        options.RedisTimeoutMilliseconds = 7000;   // Operation timeout (default: 5000)
        options.MaxReconnectAttempts = 10;         // Reconnection attempts (default: 10)
    });
```

## Configuration Options

### RedisCacheConfiguration

| Property | Default | Description |
|----------|---------|-------------|
| **L1 Cache (In-Memory)** | | |
| `EnableInMemoryCache` | `true` | Enable local in-memory cache (L1) |
| `LocalCacheSizeLimit` | `1000` | Maximum number of flags in memory |
| `LocalCacheDurationSeconds` | `10` | L1 cache TTL (short for consistency) |
| **L2 Cache (Redis)** | | |
| `CacheDurationInMinutes` | `60` | Redis cache TTL (includes ±5 min jitter) |
| **Circuit Breaker** | | |
| `CircuitBreakerThreshold` | `3` | Consecutive failures before opening |
| `CircuitBreakerDurationSeconds` | `30` | Duration circuit stays open |
| **Connection** | | |
| `RedisTimeoutMilliseconds` | `5000` | Operation timeout (connect, sync, async) |
| `MaxReconnectAttempts` | `10` | Maximum reconnection attempts |

### Connection Strings

#### Simple Local Redis
```csharp
{ "ConnectionStrings": { "RedisConnection": "localhost:6379" } }
```

#### Production Redis with Password
```csharp
{ "ConnectionStrings": { "RedisConnection": "cache.example.com:6380,password=yourpassword,ssl=true" } }
```


#### Azure Redis Cache
```csharp
{ "ConnectionStrings": { "RedisConnection": "your-cache.redis.cache.windows.net:6380,password=yourkey,ssl=true,abortConnect=false" } }
```

#### AWS ElastiCache
```csharp
{ "ConnectionStrings": { "RedisConnection": "your-cluster.cache.amazonaws.com:6379,ssl=false" } }
```


## Caching Recommendations

### ⚠️ Important: Local vs. Redis Cache Configuration

**When using Redis (recommended for production):**
```csharp
// ✅ CORRECT - Let Redis provider manage both L1 and L2 
caching builder.Services 
    .ConfigureFeatureFlags(config => 
    { 
        config.RegisterFlagsWithContainer = true; 
        config.EnableFlagFactory = true; // Do NOT set LocalCacheConfiguration
    }) 
    .AddRedisCache(redisConnectionString, options => 
    { 
        options.EnableInMemoryCache = true;  // L1 cache
        options.LocalCacheSizeLimit = 1000; 
        options.CacheDurationInMinutes = 60; // L2 cache 
    });
```


**When NOT using Redis (development or simple deployments):**
```csharp
// ✅ CORRECT - Configure local caching only
builder.Services
        .ConfigureFeatureFlags(config => 
            { 
                config.RegisterFlagsWithContainer = true; 
                config.EnableFlagFactory = true;
                // Configure local cache when Redis is NOT used
                config.LocalCacheConfiguration = new LocalCacheConfiguration
                {
                    LocalCacheEnabled = true,
                    CacheDurationInMinutes = 60,
                    CacheSizeLimit = 1000
                };
            })
        .AddPostgreSqlFeatureFlags(connectionString);
// No Redis - local cache only
```


### Production Recommendations

| Scenario | Caching Strategy | Rationale |
|----------|------------------|-----------|
| **Multi-Instance Production** | Redis (L1 + L2) | Required for consistency across instances |
| **Single-Instance Production** | Local Cache Only | Sufficient, simpler setup |
| **High-Traffic Production** | Redis with tuned L1 | Reduces database load by 95%+ |
| **Development** | Local Cache or None | Simpler debugging, faster iterations |
| **No Redis Available** | Local Cache (Required) | At minimum, enables basic caching |

### Cache Duration Guidelines

| Environment | L1 Duration | L2 (Redis) Duration |
|-------------|-------------|---------------------|
| Development | 5-10 seconds | 10-30 minutes |
| Staging | 10-15 seconds | 30-60 minutes |
| Production | 15-30 seconds | 60-180 minutes |
| High-Traffic | 30-60 seconds | 120-240 minutes |

**Shorter L1 duration = better consistency across instances**  
**Longer L2 duration = reduced database load**

## Circuit Breaker Pattern

```markdown
The Redis cache includes a built-in circuit breaker to handle Redis outages gracefully:
┌─────────────────────┐ 
│  Redis Available    │ 
│  (Circuit Closed)   │ 
└──────────┬──────────┘ 
           │ 
 3+ Consecutive Failures ▼ 
┌─────────────────────┐ 
│  Circuit Open       │ 
│  (30 seconds)       │ 
│  • L1 cache only    │ 
│  • No Redis calls   │ 
└──────────┬──────────┘ 
           │ 
      After 30s ▼ 
┌─────────────────────┐ 
│  Half-Open          │ 
│  • Test Redis       │ 
│  • If success: Close│ 
│  • If fail: Reopen  │ 
└─────────────────────┘
```


**Benefits:**
- Application continues functioning with L1 cache
- Prevents cascading failures
- Automatic recovery when Redis is restored
- Configurable thresholds and duration

## Advanced Features

### Sliding Expiration

Frequently accessed flags automatically extend their Redis TTL:
```csharp
// Hot flags stay cached longer 
await cache.GetAsync(cacheKey); // Refreshes expiration on each read
```


### Cache Stampede Prevention

Random jitter (±5 minutes) prevents synchronized cache expirations:
```csharp
// Instead of all flags expiring at exactly 60 minutes:
// Flags expire between 55-65 minutes
```

### Monitoring and Logging
```csharp
builder.Services.AddLogging(config => 
    { 
        config.AddConsole(); 
        config.SetMinimumLevel(LogLevel.Debug); // See cache hit/miss logs 
    });
```

**Log Events:**
- Cache hits/misses with latency
- Circuit breaker state changes
- Redis connection failures/restorations
- Performance metrics

### Health Checks
```csharp
// Validate Redis connection on startup 
var redisHealthy = await app.Services.ValidateRedisConnectionAsync();
if (!redisHealthy)
{ 
    logger.LogWarning("Redis unavailable - running with L1 cache only"); 
}
```


## Performance Characteristics

### Typical Latencies

| Operation | L1 Cache | L2 Redis | Database |
|-----------|----------|----------|----------|
| Cache Hit | < 1 μs | 1-5 ms | 10-50 ms |
| Cache Miss | N/A | 1-5 ms | 10-50 ms |

### Throughput

- **With Redis**: 50,000+ flag evaluations/second per instance
- **Without Redis**: 500-1,000 evaluations/second (database limited)

### Cache Hit Rates

- **L1 Cache**: 95-99% (microsecond responses)
- **L2 Redis**: 90-95% (millisecond responses)
- **Database**: < 5% (only on cache misses)

## Best Practices

1. **Always use Redis in production** with multiple instances
2. **Enable L1 cache** for best performance (default: enabled)
3. **Tune L1 duration** based on consistency requirements (10-30 seconds typical)
4. **Set longer L2 duration** to reduce database load (60-180 minutes)
5. **Monitor circuit breaker** for Redis health issues
6. **Use appropriate cache size limits** based on flag count
7. **Configure timeouts** appropriately for your network latency

## Troubleshooting

### Issue: High Redis Latency
```csharp
options.RedisTimeoutMilliseconds = 10000; // Increase timeout 
options.LocalCacheDurationSeconds = 30;   // Longer L1 cache
```

### Issue: Inconsistent Flag Values Across Instances

```csharp
options.LocalCacheDurationSeconds = 5;    // Shorter L1 duration 
options.CacheDurationInMinutes = 30;      // Shorter L2 duration
```

### Issue: Circuit Breaker Opening Frequently
```csharp
options.CircuitBreakerThreshold = 10;           // More tolerant 
options.CircuitBreakerDurationSeconds = 60;     // Longer recovery time options.RedisTimeoutMilliseconds = 10000;       // Increase timeout
```

### Issue: Memory Pressure from L1 Cache
```csharp
options.LocalCacheSizeLimit = 500;        // Reduce cache size 
options.LocalCacheDurationSeconds = 5;    // Shorter TTL
```


## Requirements

- .NET Standard 2.0 or higher
- Redis 5.0+ (Redis 6.0+ recommended)
- StackExchange.Redis 2.8.0+

## Related Packages

- [Propel.FeatureFlags](../../src/Propel.FeatureFlags/) - Core feature flag library
- [Propel.FeatureFlags.PostgreSql](../Propel.FeatureFlags.PostgreSql/) - PostgreSQL repository
- [Propel.FeatureFlags.SqlServer](../Propel.FeatureFlags.SqlServer/) - SQL Server repository
- [Propel.FeatureFlags.DependencyInjection.Extensions](../Propel.FeatureFlags.DependencyInjection.Extensions/) - DI configuration helpers
- [Propel.FeatureFlags.AspNetCore](../../src/Propel.FeatureFlags.AspNetCore/) - ASP.NET Core middleware