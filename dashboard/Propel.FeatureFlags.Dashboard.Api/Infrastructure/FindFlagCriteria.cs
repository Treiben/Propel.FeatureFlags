namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public record class FindFlagCriteria(string? Key, string? Name, string? Description);