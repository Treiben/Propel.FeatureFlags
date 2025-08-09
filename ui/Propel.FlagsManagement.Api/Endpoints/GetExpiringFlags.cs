using FeatureRabbit.Flags.Persistence;
using FeatureRabbit.Management.Api.Endpoints.Shared;

namespace FeatureRabbit.Management.Api.Endpoints;

public sealed class GetExpiringFlagsEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapGet("/api/feature-flags/expiring",
			async (
				ExpirationHandler expirationHandler) =>
			{
				return await expirationHandler.HandleAsync();
			})
			.WithName("GetExpiringFlags")
			.WithTags("Feature Flags", "Management", "Lifecycle")
			.Produces<Dictionary<string, FeatureFlagDto>>()
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy);
	}
}

public sealed class ExpirationHandler
{
	private readonly IFeatureFlagRepository _repository;
	private readonly ILogger<ExpirationHandler> _logger;

	public ExpirationHandler(
		IFeatureFlagRepository repository,
		ILogger<ExpirationHandler> logger)
	{
		_repository = repository;
		_logger = logger;
	}

	public async Task<IResult> HandleAsync(int days = 7)
	{
		try
		{
			var expirationDate = DateTime.UtcNow.AddDays(days);
			var flags = await _repository.GetExpiringAsync(expirationDate);
			var flagDtos = flags.Select(f => new FeatureFlagDto(f)).ToList();
			return Results.Ok(flagDtos);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error retrieving expiring flags");
			return Results.StatusCode(500);
		}
	}
}


