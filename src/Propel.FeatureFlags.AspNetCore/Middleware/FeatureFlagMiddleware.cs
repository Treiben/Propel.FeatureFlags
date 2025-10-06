using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.Clients;

namespace Propel.FeatureFlags.AspNetCore.Middleware;

public class GlobalFlag
{
	public string Key { get; set; } = string.Empty;
	public int StatusCode { get; set; } = 404;
	public object Response { get; set; } = new { error = "Feature not available" };
}

public class FeatureFlagMiddlewareOptions
{
	public bool EnableMaintenanceMode { get; set; } = false;
	public string MaintenanceFlagKey { get; set; } = "maintenance-mode";
	public object? MaintenanceResponse { get; set; }
	public List<GlobalFlag> GlobalFlags { get; set; } = [];
	public List<Func<HttpContext, Dictionary<string, object>>> AttributeExtractors { get; set; } = [];
	public Func<HttpContext, string?>? TenantIdExtractor { get; set; }
	public Func<HttpContext, string?>? UserIdExtractor { get; set; }
}

public class FeatureFlagMiddleware
{
	private readonly RequestDelegate _next;
	private readonly IGlobalFlagClient _globalFlags;
	private readonly IApplicationFlagClient _featureFlags;
	private readonly ILogger<FeatureFlagMiddleware> _logger;
	private readonly FeatureFlagMiddlewareOptions _options;

	public FeatureFlagMiddleware(
		RequestDelegate next,
		IGlobalFlagClient globalFlags,
		IApplicationFlagClient featureFlags,
		ILogger<FeatureFlagMiddleware> logger,
		FeatureFlagMiddlewareOptions options)
	{
		_next = next;
		_globalFlags = globalFlags;
		_featureFlags = featureFlags;
		_logger = logger;
		_options = options;
	}

	public async Task InvokeAsync(HttpContext context)
	{
		_logger.LogDebug("FeatureFlagMiddleware.InvokeAsync called for request {Path}", context.Request.Path);

		// Extract user ID using configured extractor or fallback
		var tenantId = ExtractTenantId(context);
		_logger.LogDebug("Tenant ID extracted: {TenantId}", tenantId ?? "null");
		var userId = ExtractUserId(context);
		_logger.LogDebug("User ID extracted: {UserId}", userId ?? "null");
		// Build base attributes
		var attributes = ExtractAttributes(context) ?? new Dictionary<string, object>();
		_logger.LogDebug("Attributes extracted: {@Attributes}", attributes);

		if (await CheckMaintenanceMode(tenantId, userId, attributes))
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

		// Check global feature gates
		foreach (var flag in _options.GlobalFlags)
		{
			_logger.LogDebug("Checking global flag: {FlagKey}", flag.Key);
			bool isEnabled = await _globalFlags.IsEnabledAsync(flagKey: flag.Key, tenantId: tenantId, userId: userId, attributes: attributes);
			_logger.LogDebug("Global flag {FlagKey} status: {IsEnabled}", flag.Key, isEnabled);

			if (!isEnabled)
			{
				_logger.LogInformation("Global flag {FlagKey} is disabled, returning {StatusCode} response",
					flag.Key, flag.StatusCode);
				context.Response.StatusCode = flag.StatusCode;
				context.Response.ContentType = "application/json";
				await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(flag.Response));
				return;
			}
		}

		// Add evaluator to context
		context.Items["FeatureFlagEvaluator"] = new HttpContextFeatureFlagEvaluator(_featureFlags, tenantId, userId, attributes);
		_logger.LogDebug("Feature flag evaluator added to HttpContext.Items");

		_logger.LogDebug("FeatureFlagMiddleware completed processing, calling next middleware");
		await _next(context);
	}

	private string? ExtractTenantId(HttpContext context)
	{
		string? tenantId = null;
		if (_options.TenantIdExtractor != null)
		{
			try
			{
				tenantId = _options.TenantIdExtractor(context);
				_logger.LogDebug("Extracted tenant ID using custom extractor: {TenantId}", tenantId ?? "null");
				return tenantId;
			}
			catch (Exception ex)
			{
				_logger.LogWarning(ex, "Error extracting tenant ID with custom extractor");
			}
		}

		tenantId = context.Request.Headers["X-Tenant-ID"].FirstOrDefault() ??
			   context.Request.Query["tenantId"].FirstOrDefault() ??
			   context.User.FindFirst("tenantId")?.Value;

		_logger.LogDebug("Extracted tenant ID using default extractors: {TenantId}", tenantId ?? "null");
		return tenantId;
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

	private Dictionary<string, object>? ExtractAttributes(HttpContext context)
	{
		var attributes = new Dictionary<string, object>
		{
			["userAgent"] = context.Request.Headers["User-Agent"].ToString(),
			["ipAddress"] = context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
			["path"] = context.Request.Path,
			["method"] = context.Request.Method
		};
		_logger.LogDebug("Base attributes: {@Attributes}", attributes);

		// Add custom attributes using configured extractors
		if (_options.AttributeExtractors != null)
		{
			foreach (var extractor in _options.AttributeExtractors)
			{
				try
				{
					var customAttributes = extractor(context);
					foreach (var attr in customAttributes)
					{
						attributes[attr.Key] = attr.Value;
					}
				}
				catch (Exception ex)
				{
					_logger.LogWarning(ex, "Error extracting custom attributes");
				}
			}
		}
		return attributes;
	}

	private async Task<bool> CheckMaintenanceMode(string? tenantId, string? userId, Dictionary<string, object> attributes)
	{
		if (!_options.EnableMaintenanceMode)
		{
			_logger.LogDebug("Maintenance mode is disabled");
			return false;
		}

		return await _globalFlags.IsEnabledAsync(flagKey: _options.MaintenanceFlagKey,
			tenantId: tenantId, userId: userId, attributes: attributes);
	}
}
