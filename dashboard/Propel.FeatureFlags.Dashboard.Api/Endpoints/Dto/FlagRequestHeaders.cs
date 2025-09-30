using Microsoft.AspNetCore.Mvc;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;

/// <summary>
/// Scope is required and used to identify the context for the feature flag operation.
/// Values: Application or Global.
/// 
/// ApplicationName is required when scope is Application and can be omitted when scope is Global.
/// ApplicationVersion is optional.
/// </summary>
public record FlagRequestHeaders(
		[FromHeader(Name = "X-Scope")] string Scope,
		[FromHeader(Name = "X-Application-Name")] string? ApplicationName,
		[FromHeader(Name = "X-Application-Version")] string? ApplicationVersion
	);