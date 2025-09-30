using Microsoft.AspNetCore.Authorization;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public static class AuthorizationPolicies
{
	public static AuthorizationPolicy HasWriteActionPolicy { get; }
		= new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.RequireClaim("scope", "write")
		.Build();

	public static AuthorizationPolicy HasReadActionPolicy { get; }
		= new AuthorizationPolicyBuilder()
		.RequireAuthenticatedUser()
		.RequireClaim("scope", "read")
		.Build();
}