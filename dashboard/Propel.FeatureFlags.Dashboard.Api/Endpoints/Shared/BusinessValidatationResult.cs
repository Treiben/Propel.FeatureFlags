namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

public record BusinessValidationResult
{
	public bool IsValid { get; init; }
	public Dictionary<string, string[]> Errors { get; init; } = [];
}
