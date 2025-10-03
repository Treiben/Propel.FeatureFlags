namespace Propel.FeatureFlags.Dashboard.Api.Infrastructure;

public record class FindFlagCriteria(string? Key = null, string? Name = null, string? Description = null);