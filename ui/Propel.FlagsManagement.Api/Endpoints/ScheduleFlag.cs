using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

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
		.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
		.AddEndpointFilter<ValidationFilter<ScheduleFlagRequest>>()
		.WithName("ScheduleFeatureFlag")
		.WithTags("Feature Flags", "Lifecycle Management", "Operations", "Management Api")
		.Produces<FeatureFlagDto>()
		.ProducesValidationProblem();
	}
}

public sealed class ScheduleFlagHandler(
	IFeatureFlagRepository repository,
	IFeatureFlagCache cache,
	ILogger<ScheduleFlagHandler> logger,
	CurrentUserService currentUserService)
{
	public async Task<IResult> HandleAsync(string key, ScheduleFlagRequest request)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			// Update flag for scheduling
			flag.Status = FeatureFlagStatus.Scheduled;
			flag.ScheduledEnableDate = request.EnableDate.ToUniversalTime();
			flag.ScheduledDisableDate = request.DisableDate?.ToUniversalTime();
			flag.UpdatedBy = currentUserService.UserName!;

			var updatedFlag = await repository.UpdateAsync(flag);
			await cache.RemoveAsync(key);

			var scheduleInfo = request.DisableDate.HasValue 
				? $"enable at {request.EnableDate:yyyy-MM-dd HH:mm} UTC, disable at {request.DisableDate:yyyy-MM-dd HH:mm} UTC"
				: $"enable at {request.EnableDate:yyyy-MM-dd HH:mm} UTC";

			logger.LogInformation("Feature flag {Key} scheduled by {User} to {ScheduleInfo}",
				key, currentUserService.UserName, scheduleInfo);

			return Results.Ok(new FeatureFlagDto(updatedFlag));
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class ScheduleFlagRequestValidator : AbstractValidator<ScheduleFlagRequest>
{
	public ScheduleFlagRequestValidator()
	{
		RuleFor(c => c.EnableDate)
			.GreaterThan(DateTime.UtcNow)
			.WithMessage("Enable date must be in the future. Use immediate operations for current changes");

		RuleFor(c => c.DisableDate)
			.GreaterThan(c => c.EnableDate)
			.When(c => c.DisableDate.HasValue)
			.WithMessage("Disable date must be after enable date");

		RuleFor(c => c.DisableDate)
			.GreaterThan(DateTime.UtcNow)
			.When(c => c.DisableDate.HasValue)
			.WithMessage("Disable date must be in the future");
	}
}


