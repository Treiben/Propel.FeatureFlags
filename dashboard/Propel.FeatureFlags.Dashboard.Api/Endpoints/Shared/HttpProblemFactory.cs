using Microsoft.AspNetCore.Mvc;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

/// <summary>
/// Factory for creating standardized HTTP problem detail responses following RFC 7807.
/// </summary>
public static class HttpProblemFactory
{
	private const string BaseRfcUrl = "https://tools.ietf.org/html/rfc";

	/// <summary>
	/// Creates a 500 Internal Server Error response for unexpected exceptions.
	/// </summary>
	public static IResult InternalServerError(Exception? ex = null, ILogger? logger = null, string? detail = null)
	{
		logger?.LogError(ex, "Internal server error occurred: {Message}", ex?.Message);

		return Results.Problem(
			title: "Internal Server Error",
			detail: detail ?? "An unexpected error occurred while processing your request. Please try again later.",
			statusCode: StatusCodes.Status500InternalServerError,
			type: $"{BaseRfcUrl}7231#section-6.6.1");
	}

	/// <summary>
	/// Creates a 500 Internal Server Error response for database-related exceptions.
	/// </summary>
	public static IResult DatabaseError(Exception? ex = null, ILogger? logger = null, string? detail = null)
	{
		logger?.LogError(ex, "Database error occurred: {Message}", ex?.Message);

		return Results.Problem(
			title: "Database Error",
			detail: detail ?? "An error occurred while accessing the database. Please try again later.",
			statusCode: StatusCodes.Status500InternalServerError,
			type: $"{BaseRfcUrl}7231#section-6.6.1");
	}

	/// <summary>
	/// Creates a 499 Client Closed Request response when the client cancels the request.
	/// </summary>
	public static IResult ClientClosedRequest(ILogger? logger = null)
	{
		logger?.LogWarning("Request was canceled by the client");
		return Results.StatusCode(499); // Client Closed Request - non-standard but widely used
	}

	/// <summary>
	/// Creates a 404 Not Found response for missing resources.
	/// </summary>
	public static IResult NotFound(string resourceType, object id, ILogger? logger = null)
	{
		logger?.LogWarning("{ResourceType} not found with ID: {Id}", resourceType, id);
		
		return Results.Problem(
			title: "Resource Not Found",
			detail: $"{resourceType} with ID '{id}' was not found.",
			statusCode: StatusCodes.Status404NotFound,
			type: $"{BaseRfcUrl}7231#section-6.5.4");
	}

	/// <summary>
	/// Creates a 404 Not Found response for a missing resource by key.
	/// </summary>
	public static IResult NotFound(string resourceType, string key, ILogger? logger = null)
	{
		logger?.LogWarning("{ResourceType} not found with key: {Key}", resourceType, key);
		
		return Results.Problem(
			title: "Resource Not Found",
			detail: $"{resourceType} with key '{key}' was not found.",
			statusCode: StatusCodes.Status404NotFound,
			type: $"{BaseRfcUrl}7231#section-6.5.4");
	}

	/// <summary>
	/// Creates a 400 Bad Request response with a simple message.
	/// </summary>
	public static IResult BadRequest(string message, ILogger? logger = null)
	{
		logger?.LogWarning("Bad request: {Message}", message);
		
		return Results.Problem(
			title: "Bad Request",
			detail: message,
			statusCode: StatusCodes.Status400BadRequest,
			type: $"{BaseRfcUrl}7231#section-6.5.1");
	}

	/// <summary>
	/// Creates a 400 Bad Request response with detailed information.
	/// </summary>
	public static IResult BadRequest(string title, string detail, ILogger? logger = null)
	{
		logger?.LogWarning("Bad request: {Title}. Detail: {Detail}", title, detail);
		
		return Results.Problem(
			title: title,
			detail: detail,
			statusCode: StatusCodes.Status400BadRequest,
			type: $"{BaseRfcUrl}7231#section-6.5.1");
	}

	/// <summary>
	/// Creates a 400 Bad Request response for validation failures.
	/// Uses the standard validation problem format for field-level errors.
	/// </summary>
	public static IResult ValidationFailed(IDictionary<string, string[]> errors, ILogger? logger = null)
	{
		logger?.LogWarning("Validation failed with {Count} errors", errors.Count);
		return Results.ValidationProblem(errors);
	}

	/// <summary>
	/// Creates a 422 Unprocessable Entity response for business rule violations.
	/// </summary>
	public static IResult UnprocessableEntity(string detail, ILogger? logger = null)
	{
		logger?.LogWarning("Unprocessable entity: {Detail}", detail);
		
		return Results.Problem(
			title: "Unprocessable Entity",
			detail: detail,
			statusCode: StatusCodes.Status422UnprocessableEntity,
			type: $"{BaseRfcUrl}4918#section-11.2");
	}

	/// <summary>
	/// Creates a 409 Conflict response for resource conflicts.
	/// </summary>
	public static IResult Conflict(string detail, ILogger? logger = null)
	{
		logger?.LogWarning("Conflict: {Detail}", detail);
		
		return Results.Problem(
			title: "Resource Conflict",
			detail: detail,
			statusCode: StatusCodes.Status409Conflict,
			type: $"{BaseRfcUrl}7231#section-6.5.8");
	}

	/// <summary>
	/// Creates a 409 Conflict response for specific resource type conflicts.
	/// </summary>
	public static IResult Conflict(string resourceType, string conflictReason, ILogger? logger = null)
	{
		logger?.LogWarning("Conflict with {ResourceType}: {Reason}", resourceType, conflictReason);
		
		return Results.Problem(
			title: $"{resourceType} Conflict",
			detail: conflictReason,
			statusCode: StatusCodes.Status409Conflict,
			type: $"{BaseRfcUrl}7231#section-6.5.8");
	}

	/// <summary>
	/// Creates a 401 Unauthorized response for authentication failures.
	/// </summary>
	public static IResult Unauthorized(string? detail = null, ILogger? logger = null)
	{
		logger?.LogWarning("Unauthorized access attempt");
		
		return Results.Problem(
			title: "Unauthorized",
			detail: detail ?? "Authentication is required to access this resource.",
			statusCode: StatusCodes.Status401Unauthorized,
			type: $"{BaseRfcUrl}7235#section-3.1");
	}

	/// <summary>
	/// Creates a 403 Forbidden response for authorization failures.
	/// </summary>
	public static IResult Forbidden(string? detail = null, ILogger? logger = null)
	{
		logger?.LogWarning("Forbidden access attempt");
		
		return Results.Problem(
			title: "Forbidden",
			detail: detail ?? "You do not have permission to access this resource.",
			statusCode: StatusCodes.Status403Forbidden,
			type: $"{BaseRfcUrl}7231#section-6.5.3");
	}

	/// <summary>
	/// Creates a 429 Too Many Requests response for rate limiting.
	/// </summary>
	public static IResult TooManyRequests(string? detail = null, TimeSpan? retryAfter = null, ILogger? logger = null)
	{
		logger?.LogWarning("Rate limit exceeded");

		var problemDetails = new ProblemDetails
		{
			Title = "Too Many Requests",
			Detail = detail ?? "Rate limit exceeded. Please try again later.",
			Status = StatusCodes.Status429TooManyRequests,
			Type = $"{BaseRfcUrl}6585#section-4"
		};

		if (retryAfter.HasValue)
		{
			problemDetails.Extensions["retryAfter"] = retryAfter.Value.TotalSeconds;
		}

		return Results.Problem(problemDetails);
	}

	/// <summary>
	/// Creates a 503 Service Unavailable response for temporary service issues.
	/// </summary>
	public static IResult ServiceUnavailable(string? detail = null, TimeSpan? retryAfter = null, ILogger? logger = null)
	{
		logger?.LogWarning("Service unavailable");

		var problemDetails = new ProblemDetails
		{
			Title = "Service Unavailable",
			Detail = detail ?? "The service is temporarily unavailable. Please try again later.",
			Status = StatusCodes.Status503ServiceUnavailable,
			Type = $"{BaseRfcUrl}7231#section-6.6.4"
		};

		if (retryAfter.HasValue)
		{
			problemDetails.Extensions["retryAfter"] = retryAfter.Value.TotalSeconds;
		}

		return Results.Problem(problemDetails);
	}

	/// <summary>
	/// Creates a custom problem response with specified status code and details.
	/// </summary>
	public static IResult CustomProblem(
		string title,
		string detail,
		int statusCode,
		string? type = null,
		IDictionary<string, object?>? extensions = null,
		ILogger? logger = null)
	{
		logger?.LogWarning("Custom problem response: {Title} ({StatusCode})", title, statusCode);

		var problemDetails = new ProblemDetails
		{
			Title = title,
			Detail = detail,
			Status = statusCode,
			Type = type
		};

		if (extensions != null)
		{
			foreach (var extension in extensions)
			{
				problemDetails.Extensions[extension.Key] = extension.Value;
			}
		}

		return Results.Problem(problemDetails);
	}
}