using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Client;

namespace Propel.FeatureFlags.AspNetCore.Middleware;

public class GlobalFlag
{
	public string FlagKey { get; set; } = string.Empty;
	public int StatusCode { get; set; } = 404;
	public object Response { get; set; } = new { error = "Feature not available" };
}

public class FeatureFlagMiddlewareOptions
{
	public bool EnableMaintenanceMode { get; set; } = true;
	public string MaintenanceFlagKey { get; set; } = "maintenance-mode";
	public object? MaintenanceResponse { get; set; }
	public List<GlobalFlag> GlobalFlags { get; set; } = new();
	public List<Func<HttpContext, Dictionary<string, object>>> AttributeExtractors { get; set; } = new();
	public Func<HttpContext, string?>? UserIdExtractor { get; set; }
}

public class FeatureFlagMiddleware
{
	private readonly RequestDelegate _next;
	private readonly IFeatureFlagClient _featureFlags;
	private readonly ILogger<FeatureFlagMiddleware> _logger;
	private readonly FeatureFlagMiddlewareOptions _options;

	public FeatureFlagMiddleware(
		RequestDelegate next,
		IFeatureFlagClient featureFlags,
		ILogger<FeatureFlagMiddleware> logger,
		FeatureFlagMiddlewareOptions options)
	{
		_next = next;
		_featureFlags = featureFlags;
		_logger = logger;
		_options = options;

		// Logging AFTER logger is initialized
		_logger.LogDebug("FeatureFlagMiddleware initialized with MaintenanceMode={EnableMaintenanceMode}, MaintenanceFlagKey={MaintenanceFlagKey}",
			_options.EnableMaintenanceMode, _options.MaintenanceFlagKey);
	}

	public async Task InvokeAsync(HttpContext context)
	{
		_logger.LogDebug("FeatureFlagMiddleware.InvokeAsync called for request {Path}", context.Request.Path);
		
		// Extract user ID using configured extractor or fallback
		var userId = ExtractUserId(context);
		_logger.LogDebug("User ID extracted: {UserId}", userId ?? "null");

		// Build base attributes
		var attributes = new Dictionary<string, object>
		{
			["userAgent"] = context.Request.Headers["User-Agent"].ToString(),
			["ipAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			["path"] = context.Request.Path,
			["method"] = context.Request.Method
		};
		_logger.LogDebug("Base attributes: {@Attributes}", attributes);

		// Add custom attributes using configured extractors
		foreach (var extractor in _options.AttributeExtractors)
		{
			try
			{
				var customAttributes = extractor(context);
				foreach (var attr in customAttributes)
				{
					attributes[attr.Key] = attr.Value;
				}
				_logger.LogDebug("Added custom attributes from extractor");
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error extracting custom attributes");
			}
		}

		// Check maintenance mode
		if (_options.EnableMaintenanceMode)
		{
			_logger.LogDebug("Checking maintenance mode flag: {MaintenanceFlagKey}", _options.MaintenanceFlagKey);
			bool isInMaintenance = await _featureFlags.IsEnabledAsync(_options.MaintenanceFlagKey, userId, attributes);
			_logger.LogDebug("Maintenance mode status: {IsInMaintenance}", isInMaintenance);
			
			if (isInMaintenance)
			{
				_logger.LogInformation("Maintenance mode is active, returning 503 response");
				context.Response.StatusCode = 503;
				context.Response.ContentType = "application/json";

				var response = _options.MaintenanceResponse ?? new
				{
					error = "Service temporarily unavailable for maintenance",
					retryAfter = "300"
				};

				await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
				return;
			}
		}
		else
		{
			_logger.LogDebug("Maintenance mode is disabled");
		}

		// Check global feature gates
		foreach (var globalFlag in _options.GlobalFlags)
		{
			_logger.LogDebug("Checking global flag: {FlagKey}", globalFlag.FlagKey);
			bool isEnabled = await _featureFlags.IsEnabledAsync(globalFlag.FlagKey, userId, attributes);
			_logger.LogDebug("Global flag {FlagKey} status: {IsEnabled}", globalFlag.FlagKey, isEnabled);
			
			if (!isEnabled)
			{
				_logger.LogInformation("Global flag {FlagKey} is disabled, returning {StatusCode} response",
					globalFlag.FlagKey, globalFlag.StatusCode);
				context.Response.StatusCode = globalFlag.StatusCode;
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(globalFlag.Response));
				return;
			}
		}

		// Add evaluator to context
		context.Items["FeatureFlagEvaluator"] = new HttpContextFeatureFlagEvaluator(_featureFlags, userId, attributes);
		_logger.LogDebug("Feature flag evaluator added to HttpContext.Items");

		_logger.LogDebug("FeatureFlagMiddleware completed processing, calling next middleware");
		await _next(context);
	}

	private string? ExtractUserId(HttpContext context)
	{
		string? userId = null;
		if (_options.UserIdExtractor != null)
		{
			try
			{
				userId = _options.UserIdExtractor(context);
				_logger.LogDebug("Extracted user ID using custom extractor: {UserId}", userId ?? "null");
				return userId;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error extracting user ID with custom extractor");
			}
		}

		// Default extraction
		 userId = context.User.Identity?.Name ??
			   context.Request.Headers["X-User-ID"].FirstOrDefault() ??
			   context.Request.Query["userId"].FirstOrDefault();
		   
		_logger.LogDebug("Extracted user ID using default extractors: {UserId}", userId ?? "null");
		return userId;
	}
}
