using FluentValidation;
using Propel.FeatureFlags;
using Propel.FeatureFlags.Cache;
using Propel.FeatureFlags.Core;
using Propel.FlagsManagement.Api.Endpoints.Dto;
using Propel.FlagsManagement.Api.Endpoints.Shared;

namespace Propel.FlagsManagement.Api.Endpoints;

public record UpdateTargetingRulesRequest(List<TargetingRuleDto>? TargetingRules, bool RemoveTargetingRules);

public record TargetingRuleDto(string Attribute, TargetingOperator Operator, List<string> Values, string Variation);

public sealed class UpdateTargetingRulesEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/targeting-rules",
			async (
				string key,
				UpdateTargetingRulesRequest request,
				UpdateTargetingRulesHandler targetingRulesHandler,
				CancellationToken cancellationToken) =>
			{
				return await targetingRulesHandler.HandleAsync(key, request, cancellationToken);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<UpdateTargetingRulesRequest>>()
			.WithName("UpdateTargetingRules")
			.WithTags("Feature Flags", "Operations", "Custom Targeting", "Targeting Rules", "Management Api")
			.Produces<FeatureFlagResponse>()
			.ProducesValidationProblem();
	}
}

public sealed class UpdateTargetingRulesHandler(
		IFeatureFlagRepository repository,
		ICurrentUserService currentUserService,
		ILogger<UpdateTargetingRulesHandler> logger,
		IFeatureFlagCache? cache = null)
{
	public async Task<IResult> HandleAsync(string key, UpdateTargetingRulesRequest request,
		CancellationToken cancellationToken)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger);
		}

		try
		{
			var flag = await repository.GetAsync(key, cancellationToken);
			if (flag == null)
			{
				return HttpProblemFactory.NotFound("Feature flag", key, logger);
			}

			// Update audit record
			flag.LastModified = new FeatureFlags.Core.Audit(timestamp: DateTime.UtcNow, actor: currentUserService.UserName!);

			// Remove enabled mode as we're configuring specific targeting
			flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.Enabled);

			if (request.RemoveTargetingRules)
			{
				// Clear all targeting rules and remove the TargetingRules mode
				flag.TargetingRules.Clear();
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.TargetingRules);
			}
			else if (request.TargetingRules != null && request.TargetingRules.Count > 0)
			{
				// Replace existing targeting rules with new ones
				flag.TargetingRules = [.. request.TargetingRules.Select(dto => 
							TargetingRuleFactory.CreaterTargetingRule(
															dto.Attribute, 
															dto.Operator, 
															dto.Values,
															dto.Variation))];

				// Add the TargetingRules evaluation mode
				flag.ActiveEvaluationModes.AddMode(EvaluationMode.TargetingRules);
			}
			else
			{
				// Clear targeting rules if empty list provided and remove the TargetingRules mode
				flag.TargetingRules.Clear();
				flag.ActiveEvaluationModes.RemoveMode(EvaluationMode.TargetingRules);
			}

			var updatedFlag = await repository.UpdateAsync(flag, cancellationToken);

			if (cache != null) await cache.RemoveAsync(key, cancellationToken);

			logger.LogInformation("Feature flag {Key} targeting rules updated by {User}",
				key, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
		}
		catch (OperationCanceledException ex) when (ex.CancellationToken.IsCancellationRequested)
		{
			return HttpProblemFactory.ClientClosedRequest(logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}

public sealed class UpdateTargetingRulesRequestValidator : AbstractValidator<UpdateTargetingRulesRequest>
{
	public UpdateTargetingRulesRequestValidator()
	{
		// Validate targeting rules when not removing them
		RuleForEach(x => x.TargetingRules)
			.SetValidator(new TargetingRuleDtoValidator())
			.When(x => !x.RemoveTargetingRules && x.TargetingRules != null);

		RuleFor(x => x.TargetingRules)
			.Must(rules => rules == null || rules.Count <= 50)
			.WithMessage("Maximum of 50 targeting rules allowed");

		// Ensure no duplicate attribute-operator combinations
		RuleFor(x => x.TargetingRules)
			.Must(rules => rules == null || 
				rules.GroupBy(r => new { r.Attribute, r.Operator }).All(g => g.Count() == 1))
			.When(x => !x.RemoveTargetingRules)
			.WithMessage("Duplicate attribute-operator combinations are not allowed");
	}
}

public sealed class TargetingRuleDtoValidator : AbstractValidator<TargetingRuleDto>
{
	public TargetingRuleDtoValidator()
	{
		RuleFor(x => x.Attribute)
			.NotEmpty()
			.WithMessage("Targeting rule attribute is required")
			.MaximumLength(100)
			.WithMessage("Targeting rule attribute cannot exceed 100 characters");

		RuleFor(x => x.Values)
			.NotEmpty()
			.WithMessage("At least one value is required for targeting rule")
			.Must(values => values.Count <= 100)
			.WithMessage("Maximum of 100 values allowed per targeting rule");

		RuleForEach(x => x.Values)
			.NotEmpty()
			.WithMessage("Targeting rule values cannot be empty")
			.MaximumLength(1000)
			.WithMessage("Targeting rule value cannot exceed 1000 characters");

		RuleFor(x => x.Variation)
			.NotEmpty()
			.WithMessage("Variation is required for targeting rule")
			.MaximumLength(50)
			.WithMessage("Variation name cannot exceed 50 characters");

		// Validate numeric operations have valid numeric values
		RuleFor(x => x.Values)
			.Must(values => values.All(v => double.TryParse(v, out _)))
			.When(x => x.Operator is TargetingOperator.GreaterThan or TargetingOperator.LessThan)
			.WithMessage("Numeric operators require all values to be valid numbers");
	}
}
