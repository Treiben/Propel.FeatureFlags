using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace Propel.FlagsManagement.Api.Endpoints.Shared;

public interface IFlagResolverService
{
	Task<(bool, IResult, FeatureFlag?)> ValidateAndResolveFlagAsync(string key, FlagRequestHeaders headers, CancellationToken cancellationToken);
}

public class FlagResolverService(IFlagManagementRepository repository, ILogger<FlagResolverService> logger) : IFlagResolverService
{
	public async Task<(bool, IResult, FeatureFlag?)> ValidateAndResolveFlagAsync(string key, FlagRequestHeaders headers, CancellationToken cancellationToken)
	{
		// Validate key parameter
		if (string.IsNullOrWhiteSpace(key))
		{
			return (false, HttpProblemFactory.BadRequest("Feature flag key cannot be empty or null", logger), null);
		}

		// Validate required scope header
		if (string.IsNullOrWhiteSpace(headers.Scope))
		{
			return (false, HttpProblemFactory.BadRequest("Scope header (X-Scope) is required", logger), null);
		}

		// Parse scope enum
		if (!Enum.TryParse<Scope>(headers.Scope, true, out var parsedScope))
		{
			return (false, HttpProblemFactory.BadRequest("Invalid scope value", $"'{headers.Scope}' is not a valid scope value", logger), null);
		}

		// Resolve flag
		var flagKey = new FlagKey(key, parsedScope, headers.ApplicationName, headers.ApplicationVersion);
		var flag = await repository.GetAsync(flagKey, cancellationToken);
		if (flag == null)
		{
			return (false, HttpProblemFactory.NotFound("Feature flag", key, logger), null);
		}

		return (true, Results.Ok(), flag);
	}
}
