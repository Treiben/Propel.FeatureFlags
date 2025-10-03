using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record UpdateTargetingRulesRequest(List<TargetingRuleRequest>? TargetingRules, bool RemoveTargetingRules, string? Notes);

public record TargetingRuleRequest(string Attribute, TargetingOperator Operator, List<string> Values, string Variation);

public sealed class UpdateTargetingRulesEndpoint : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapPost("/api/feature-flags/{key}/targeting-rules",
			async (
				string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				UpdateTargetingRulesRequest request,
				UpdateTargetingRulesHandler handler,
				CancellationToken cancellationToken) =>
			{
				return await handler.HandleAsync(key, new FlagRequestHeaders(scope, applicationName, applicationVersion), request, cancellationToken);
			})
			.RequireAuthorization(AuthorizationPolicies.HasWriteActionPolicy)
			.AddEndpointFilter<ValidationFilter<UpdateTargetingRulesRequest>>()
			.WithName("UpdateTargetingRules")
			.WithTags("Feature Flags", "Operations", "Custom Targeting", "Targeting Rules", "Dashboard Api")
			.Produces<FeatureFlagResponse>()
			.ProducesValidationProblem();
	}
}

public sealed class UpdateTargetingRulesHandler(
		IDashboardRepository repository,
		ICurrentUserService currentUserService,
		IFlagResolverService flagResolver,
		ICacheInvalidationService cacheInvalidationService,
		ILogger<UpdateTargetingRulesHandler> logger)
{
	public async Task<IResult> HandleAsync(
		string key,
		FlagRequestHeaders headers,
		UpdateTargetingRulesRequest request,
		CancellationToken cancellationToken)
	{
		try
		{
			var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			var flagWithUpdatedRules = CreateFlagWithUpdatedRules(request, flag!);

			var updatedFlag = await repository.UpdateAsync(flagWithUpdatedRules, cancellationToken);
			await cacheInvalidationService.InvalidateFlagAsync(updatedFlag.Identifier, cancellationToken);

			logger.LogInformation("Feature flag {Key} targeting rules updated by {User}",
				key, currentUserService.UserName);

			return Results.Ok(new FeatureFlagResponse(updatedFlag));
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest(ex.Message, logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}

	private FeatureFlag CreateFlagWithUpdatedRules(UpdateTargetingRulesRequest request, FeatureFlag flag)
	{
		var oldconfig = flag.EvaluationOptions;

		// Remove enabled/disabled modes as we're configuring specific targeting
		HashSet<EvaluationMode> modes = [.. oldconfig.ModeSet.Modes];
		modes.Remove(EvaluationMode.On);
		modes.Remove(EvaluationMode.Off);

		FlagEvaluationOptions configuration;
		FlagAdministration metadata;
		if (request.RemoveTargetingRules || request.TargetingRules == null || request.TargetingRules.Count == 0)
		{
			// Remove the TargetingRules mode
			modes.Remove(EvaluationMode.TargetingRules);
			// Clear existing targeting rules
			configuration = oldconfig with {
				ModeSet = modes.Count == 0 ? EvaluationMode.Off : new ModeSet(modes),
				TargetingRules = [] };
			// add to change history
			metadata = flag.Administration with
			{
				ChangeHistory = [.. flag.Administration.ChangeHistory,
					AuditTrail.FlagModified(currentUserService.UserName!, request.Notes ?? "All targeting rules removed")]
			};
		}
		else
		{
			// Ensure TargetingRules mode is active
			modes.Add(EvaluationMode.TargetingRules);
			// Replace existing targeting rules with new ones
			configuration = oldconfig with
			{
				ModeSet = new ModeSet(modes),
				TargetingRules = [.. request.TargetingRules.Select(dto =>
					TargetingRuleFactory.CreateTargetingRule(
													dto.Attribute,
													dto.Operator,
													dto.Values,
													dto.Variation))]
			};
			// add to change history
			metadata = flag.Administration with
			{
				ChangeHistory = [.. flag.Administration.ChangeHistory,
					AuditTrail.FlagModified(currentUserService.UserName!, request.Notes ?? "Targeting rules updated")]
			};
		}

		return flag with { Administration = metadata, EvaluationOptions = configuration };
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

public sealed class TargetingRuleDtoValidator : AbstractValidator<TargetingRuleRequest>
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
