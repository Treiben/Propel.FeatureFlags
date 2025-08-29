using Propel.FeatureFlags;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public sealed class GetExpiringFlagsEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapGet("/api/feature-flags/expiring",
			async (
				int days,
				ExpirationHandler expirationHandler) =>
			{
				return await expirationHandler.HandleAsync(days);
			})
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
			.WithName("GetExpiringFlags")
			.WithTags("Feature Flags", "Lifecycle Management", "Monitoring", "Management Api")
			.Produces<List<FeatureFlagDto>>();
	}
}

public sealed class ExpirationHandler(
	IFeatureFlagRepository repository,
	ILogger<ExpirationHandler> logger)
{
	public async Task<IResult> HandleAsync(int days = 7)
	{
		// Validate days parameter
		if (days < 0)
		{
			return HttpProblemFactory.BadRequest(
				"Invalid Days Parameter", 
				"Days parameter must be a non-negative number",
				logger);
		}

		if (days > 365)
		{
			return HttpProblemFactory.BadRequest(
				"Invalid Days Parameter", 
				"Days parameter cannot exceed 365 days",
				logger);
		}

		try
		{
			var expirationDate = DateTime.UtcNow.AddDays(days);
			var flags = await repository.GetExpiringAsync(expirationDate);
			var flagDtos = flags.Select(f => new FeatureFlagDto(f)).ToList();
			
			logger.LogInformation("Retrieved {Count} flags expiring within {Days} days", 
				flagDtos.Count, days);
			
			return Results.Ok(flagDtos);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}


