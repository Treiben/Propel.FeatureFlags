using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Dashboard.Api.Infrastructure;
using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public interface IFlagResolverService
{
	Task<(bool, IResult, FeatureFlag?)> ValidateAndResolveFlagAsync(string key, FlagRequestHeaders headers, CancellationToken cancellationToken);
}

public class FlagResolverService(IDashboardRepository repository, ILogger<FlagResolverService> logger) : IFlagResolverService
{
	public async Task<(bool, IResult, FeatureFlag?)> ValidateAndResolveFlagAsync(string key, FlagRequestHeaders headers, CancellationToken cancellationToken)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return (false, HttpProblemFactory.BadRequest(
				"No feature flag key provided",
				"Feature flag key is required and cannot be empty or null.", logger), null);
		}

		// Validate required scope header
		if (string.IsNullOrWhiteSpace(headers.Scope))
		{
			return (false, HttpProblemFactory.BadRequest(
				"No feature flag scope provided", 
				"Feature flag scope is required. Use request header X-Scope: Application for application flags or X-Scope: Global for global flags.", logger), null);
		}

		// Parse scope enum
		if (!Enum.TryParse<Scope>(headers.Scope, true, out var parsedScope))
		{
			return (false, HttpProblemFactory.BadRequest(
				$"Invalid feature flag scope '{headers.Scope}'", 
				"Use request header X-Scope: Application for application flags or X-Scope: Global for global flags.", logger), null);
		}

		string application = headers.ApplicationName ?? "";
		string version = headers.ApplicationVersion ?? "1.0.0.0";
		if (parsedScope == Scope.Global)
		{
			application = "global";
			version = "0.0.0.0";
		}

		if (string.IsNullOrWhiteSpace(application))
			return (false, HttpProblemFactory.BadRequest(
				"No application name or version provided",
				"Application name with or without version required for Application scope requests. Pass name and version in headers X-Application-Name, X-Application-Version", logger), null);

		// Resolve flag
		var identifier = new FlagIdentifier(key, parsedScope, headers.ApplicationName, headers.ApplicationVersion);
		var flag = await repository.GetByKeyAsync(identifier, cancellationToken);
		if (flag == null)
		{
			return (false, HttpProblemFactory.NotFound("Feature flag", key, logger), null);
		}

		return (true, Results.Ok(), flag);
	}
}
