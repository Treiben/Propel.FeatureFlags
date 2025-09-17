using Microsoft.AspNetCore.Mvc;

namespace Propel.FlagsManagement.Api.Endpoints.Dto;

public record FlagRequestHeaders(
		[FromHeader(Name = "X-Scope")] string Scope,
		[FromHeader(Name = "X-Application-Name")] string? ApplicationName,
		[FromHeader(Name = "X-Application-Version")] string? ApplicationVersion
	);