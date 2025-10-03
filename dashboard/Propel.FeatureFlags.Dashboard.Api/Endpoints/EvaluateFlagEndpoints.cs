using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluationServices;
using Propel.FeatureFlags.FlagEvaluators;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints;

public sealed class EvaluateFlagEndpoints : IEndpoint
{
	public void AddEndpoint(IEndpointRouteBuilder epRoutBuilder)
	{
		epRoutBuilder.MapGet("/api/feature-flags/evaluate/{key}",
			async (
				string key,
				[FromHeader(Name = "X-Scope")] string scope,
				[FromHeader(Name = "X-Application-Name")] string? applicationName,
				[FromHeader(Name = "X-Application-Version")] string? applicationVersion,
				string? tenantId,
				string? userId,
				string? kvAttributes,
				FlagEvaluationHandler evaluationHandler,
				CancellationToken cancellationToken) =>
			{
				return await evaluationHandler.HandleAsync(
					key,
					new FlagRequestHeaders(scope, applicationName, applicationVersion),
					tenantId,
					userId,
					kvAttributes,
					attributes: null,
					cancellationToken);
			})
			.RequireAuthorization(AuthorizationPolicies.HasReadActionPolicy)
			.WithName("EvaluateFeatureFlag")
			.WithTags("Feature Flags", "Evaluations", "Dashboard Api")
			.Produces<EvaluationResult>();
	}
}

public sealed class FlagEvaluationHandler(
	IFlagEvaluationManager evaluationManager,
	IFlagResolverService flagResolver,
	ILogger<FlagEvaluationHandler> logger)
{
	public async Task<IResult> HandleAsync(
		string key,
		FlagRequestHeaders headers,
		string? tenantId,
		string? userId,
		string? kvAttributes = null,
		Dictionary<string, object>? attributes = null,
		CancellationToken cancellationToken = default)
	{
		var (isValid, result, flag) = await flagResolver.ValidateAndResolveFlagAsync(key, headers, cancellationToken);
		if (!isValid) return result;

		try
		{
			// Validate and parse attributes
			var attributeDict = attributes;
			if (attributeDict == null && !string.IsNullOrEmpty(kvAttributes))
			{
				if (!SerializationHelpers.TryDeserialize(kvAttributes, out Dictionary<string, object>? deserializedAttributes))
				{
					return HttpProblemFactory.BadRequest(
						"Invalid Attributes Format",
						"The attributes parameter must be a valid JSON object. Example: {\"country\":\"US\",\"plan\":\"premium\"}",
						logger);
				}
				attributeDict = deserializedAttributes ?? [];
			}

			var context = new EvaluationContext(
					tenantId: tenantId,
					userId: userId,
					attributes: attributeDict);

			var evaluationResult = await evaluationManager.ProcessEvaluation(
				new EvaluationOptions(
						key: flag!.Identifier.Key,
						modeSet: flag.EvaluationOptions.ModeSet,
						schedule: flag.EvaluationOptions.Schedule,
						operationalWindow: flag.EvaluationOptions.OperationalWindow,
						targetingRules: flag.EvaluationOptions.TargetingRules,
						userAccessControl: flag.EvaluationOptions.UserAccessControl,
						tenantAccessControl: flag.EvaluationOptions.TenantAccessControl,
						variations: flag.EvaluationOptions.Variations), 
				context);

			return Results.Ok(evaluationResult);
		}
		catch (EvaluationArgumentException ex)
		{
			return HttpProblemFactory.BadRequest("Evaluation Argument Required", ex.Message, logger);
		}
		catch (ArgumentException ex)
		{
			return HttpProblemFactory.BadRequest("Invalid Argument", ex.Message, logger);
		}
		catch (Exception ex)
		{
			return HttpProblemFactory.InternalServerError(ex, logger);
		}
	}
}