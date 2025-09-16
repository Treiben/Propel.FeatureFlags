using Npgsql;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using Propel.FeatureFlags.Services.ApplicationScope;

namespace FeatureFlags.IntegrationTests.Support;

public class TestApplicationFeatureFlag(
	string key,
	string? name = null,
	string? description = null,
	Dictionary<string, string>? tags = null,
	EvaluationMode defaultMode = EvaluationMode.Disabled) : RegisteredFeatureFlag(key, name, description, tags, defaultMode)
{
}

public static class TestHelpers
{
	public static (FeatureFlag, IRegisteredFeatureFlag) SetupTestCases(string key, 
        EvaluationMode ffMode, EvaluationMode afMode = EvaluationMode.Disabled)
	{
		var ff = new FeatureFlag
		{
			Key = new FlagKey(key, Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version),
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			ActiveEvaluationModes = new EvaluationModes([ffMode]),
			Created = new AuditTrail(timestamp: DateTime.UtcNow, actor: "integration-test"),
		};

        var rf = new TestApplicationFeatureFlag(
			key: key,
			name: $"App Flag {key}",
			description: "Application flag for integration tests",
			defaultMode: afMode);

        return (ff, rf);
	}

    public static CacheKey CreateCacheKey(string key)
    {
		var applicationName = ApplicationInfo.Name;
		var applicationVersion = ApplicationInfo.Version;

		return new CacheKey(key, [applicationName, applicationVersion]);
	}

    public static CacheKey CreateGlobalCacheKey(string key)
    {
        return new CacheKey(key, ["global"]);
	}
}
