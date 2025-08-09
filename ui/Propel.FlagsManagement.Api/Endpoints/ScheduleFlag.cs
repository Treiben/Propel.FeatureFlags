using FeatureRabbit.Flags.Cache;
using FeatureRabbit.Flags.Core;
using FeatureRabbit.Flags.Persistence;
using FeatureRabbit.Management.Api.Endpoints.Shared;
using FluentValidation;

namespace FeatureRabbit.Management.Api.Endpoints;

public record ScheduleFlagRequest(DateTime EnableDate, DateTime? DisableDate);
public sealed class ScheduleFlagEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/schedule",
			async (
				string key,
				ScheduleFlagRequest request,
				ScheduleFlagHandler scheduleFlagHandler) =>
		{
			return await scheduleFlagHandler.HandleAsync(key, request);
		})
		.RequireAuthorization()
		.AddEndpointFilter<ValidationFilter<ScheduleFlagRequestValidator>>()
		.WithName("ScheduleFeatureFlag")
		.WithTags("Feature Flags", "Scheduling", "Management")
		.Produces<FeatureFlagDto>();
	}

	public sealed class ScheduleFlagRequestValidator : AbstractValidator<ScheduleFlagRequest>
	{
		public ScheduleFlagRequestValidator()
		{
			RuleFor(c => c.EnableDate.ToUniversalTime())
				.LessThanOrEqualTo(DateTime.UtcNow)
				.WithMessage("Unable to schedule feature flag for backward date. Use simple toggling instead");
		}
	}
}

public sealed class ScheduleFlagHandler {
	private readonly IFeatureFlagRepository _repository;
	private readonly IFeatureFlagCache _cache;
	private readonly ILogger<ScheduleFlagHandler> _logger;
	private readonly CurrentUserService _currentUserService;
	public ScheduleFlagHandler(
		IFeatureFlagRepository repository,
		IFeatureFlagCache cache,
		ILogger<ScheduleFlagHandler> logger,
		CurrentUserService currentUserService)
	{
		_repository = repository;
		_cache = cache;
		_logger = logger;
		_currentUserService = currentUserService;
	}
	public async Task<IResult> HandleAsync(string key, ScheduleFlagRequest request)
	{
		try
		{
			var flag = await _repository.GetAsync(key);
			if (flag == null)
				return Results.NotFound($"Feature flag '{key}' not found");

			flag.Status = FeatureFlagStatus.Scheduled;
			flag.ScheduledEnableDate = request.EnableDate;
			flag.ScheduledDisableDate = request.DisableDate;
			flag.UpdatedBy = _currentUserService.UserName;

			await _repository.UpdateAsync(flag);
			await _cache.RemoveAsync(key);

			_logger.LogInformation("Feature flag {Key} scheduled by {User} for {EnableDate}",
				key, _currentUserService.UserName, request.EnableDate);

			return Results.Ok(new FeatureFlagDto(flag));
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Error scheduling feature flag {Key}", key);
			return Results.StatusCode(500);
		}
	}
}


