using Azure.Core;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public record UpdateTargetingRulesRequest(List<TargetingRuleRequest>? TargetingRules, bool RemoveTargetingRules);

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
			.WithTags("Feature Flags", "Operations", "Custom Targeting", "Targeting Rules", "Management Api")
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
			var (isValid, result, source) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
			if (!isValid) return result;

			var flagWithUpdatedRules = CreateFlagWithUpdatedRules(request, source!);
			flagWithUpdatedRules!.UpdateAuditTrail(currentUserService.UserName!);

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

	public FeatureFlag CreateFlagWithUpdatedRules(UpdateTargetingRulesRequest request, FeatureFlag source)
	{
		var srcConfig = source.Configuration;
		// Remove enabled mode as we're configuring specific targeting
		var modes = new EvaluationModes([.. source.Configuration.ActiveEvaluationModes.Modes]);
		modes.RemoveMode(EvaluationMode.On);

		var configuration = new FlagEvaluationConfiguration(
									identifier: source.Identifier,
									activeEvaluationModes: modes,
									schedule: srcConfig.Schedule,
									operationalWindow: srcConfig.OperationalWindow,
									userAccessControl: srcConfig.UserAccessControl,
									tenantAccessControl: srcConfig.TenantAccessControl,
									variations: srcConfig.Variations);
		if (request.RemoveTargetingRules || request.TargetingRules == null || request.TargetingRules.Count == 0)
		{
			// remove the TargetingRules mode
			modes.RemoveMode(EvaluationMode.TargetingRules);
		}
		else
		{
			// Replace existing targeting rules with new ones
			configuration.TargetingRules.AddRange([.. request.TargetingRules.Select(dto =>
							TargetingRuleFactory.CreateTargetingRule(
															dto.Attribute,
															dto.Operator,
															dto.Values,
															dto.Variation))]);

			// Add the TargetingRules evaluation mode
			modes.AddMode(EvaluationMode.TargetingRules);
		}

		return new FeatureFlag(Identifier: source.Identifier, Metadata: source.Metadata, Configuration: configuration);
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
