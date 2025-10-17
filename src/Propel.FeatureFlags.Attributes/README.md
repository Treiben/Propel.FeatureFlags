## Propel.FeatureFlags.Attributes
[![Build and Test](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml/badge.svg)](https://github.com/Treiben/propel-feature-flags-csharp/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Propel.FeatureFlags.Attributes.svg)](https://www.nuget.org/packages/Propel.FeatureFlags.Attributes/)
[![NuGet Downloads](https://img.shields.io/nuget/dt/Propel.FeatureFlags.Attributes.svg?style=flat-square)](https://www.nuget.org/packages/Propel.FeatureFlags.Attributes/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-blue)](https://docs.microsoft.com/en-us/dotnet/standard/net-standard#net-standard-versions)

A lightweight attribute-based library for implementing feature flag gating on methods using AOP (Aspect-Oriented Programming) with dynamic proxy interception.

For detailed documentation and examples, visit the repository [readme](../../README.md).

### Overview
Propel.FeatureFlags.Attributes enables you to control method execution based on feature flag state using a simple declarative attribute. When a feature is disabled, the system can automatically fall back to an alternative implementation.

### Key Features
-	Declarative Feature Gating: Use [FeatureFlagged] attribute to gate method execution
-	Automatic Fallback: Specify a fallback method when feature is disabled
-	Dynamic Proxy Interception: Built on Castle.DynamicProxy for runtime behavior modification
-	Multiple Contexts: Supports both HTTP-based and non-HTTP scenarios (console apps, workers)
-	Type-Safe: Works with strongly-typed feature flag definitions

### Installation

```bash
dotnet add package Propel.FeatureFlags.Attributes
```

Usage
1. Define a Feature Flag
```csharp
public class NewEmailServiceFeatureFlag : FeatureFlagBase
{
    public NewEmailServiceFeatureFlag()
        : base(
            key: "new-email-service",
            name: "New Email Service",
            description: "Controls whether to use the new email service implementation",
            onOfMode: EvaluationMode.Off)
    {
    }
}
```
2. Annotate Your Methods
```csharp
public interface INotificationService
{
    Task<string> SendEmailAsync(string userId, string subject, string body);
    Task<string> SendEmailLegacyAsync(string userId, string subject, string body);
}

public class NotificationService : INotificationService
{
    [FeatureFlagged(type: typeof(NewEmailServiceFeatureFlag),  fallbackMethod: nameof(SendEmailLegacyAsync))]
    public virtual async Task<string> SendEmailAsync(string userId, string subject, string body)
    {
        // New implementation - executes when feature flag is ENABLED
        return "Email sent using new service.";
    }

    public virtual async Task<string> SendEmailLegacyAsync(string userId, string subject, string body)
    {
        // Legacy implementation - executes when feature flag is DISABLED
        return "Email sent using legacy service.";
    }
}
```
3. Register Services with Interception
For Web APIs (ASP.NET Core)
```csharp
builder.Services
    .ConfigureFeatureFlags(config =>
    {
        config.Interception.EnableHttpIntercepter = true;
    });

// Register with interception
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();
```
For Console Apps / Workers
```csharp
builder.Services
    .ConfigureFeatureFlags(config =>
    {
        config.Interception.EnableIntercepter = true;
    });

// Register with interception
builder.Services.RegisterWithFeatureFlagInterception<INotificationService, NotificationService>();
```
4. Use Your Service
```csharp
public class NotificationsEndpoints
{
    public static void MapNotificationsEndpoints(this WebApplication app)
    {
        app.MapGet("/send-email", async (INotificationService svc) =>
        {
            // Automatically routed to new or legacy implementation based on feature flag
            var result = await svc.SendEmailAsync("user123", "Subject", "Body");
            return Results.Ok(result);
        });
    }
}
```

### How It Works
1.	Proxy Generation: When you register a service with RegisterWithFeatureFlagInterception, a dynamic proxy is created for the interface
2.	Interception: The FeatureFlagInterceptor intercepts calls to methods decorated with [FeatureFlagged]
3.	Evaluation: The feature flag is evaluated in real-time using IFeatureFlagEvaluator
4.	Routing: If enabled, the decorated method executes; if disabled, the fallback method is invoked
### Important Notes
-	Methods decorated with [FeatureFlagged] must be virtual to enable interception
-	The fallback method must have the same signature as the decorated method
-	Both the interface and implementation must be registered with the DI container
-	Use EnableHttpIntercepter for web applications, EnableIntercepter for console/worker applications
### Extension Methods
| Method | Description | 
|--------|-------------| 
| AddHttpAttributeInterceptors() | Adds HTTP-based interceptor infrastructure | 
| AddAttributeInterceptors() | Adds basic interceptor infrastructure (non-HTTP) | 
| RegisterWithFeatureFlagInterception<TInterface, TImplementation>() | Registers a service with feature flag interception enabled |
### Dependencies
-	[Propel.FeatureFlags](../../README.md)
-	Castle.Core (DynamicProxy)
-	Microsoft.Extensions.DependencyInjection