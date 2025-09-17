using Propel.FeatureFlags.Domain;

namespace FlagsManagementApi.IntegrationTests.Support;

public static class TestHelpers
{
	public static FeatureFlag CreateGlobalFlag(string key, EvaluationMode evaluationMode)
	{
		var flag = FeatureFlag.Create(key: new FlagKey(key, Scope.Global),
			name: $"Test Flag {key}", description: "Test flag for integration tests");
		flag.ActiveEvaluationModes = new EvaluationModes([evaluationMode]);
		return flag;
	}

	public static FeatureFlag CreateApplicationFlag(string key, EvaluationMode evaluationMode)
	{
		var flag = FeatureFlag.Create(key: new FlagKey(key, Scope.Application, "Propel.ClientApi", "v1.0.0"),
			name: $"Test Flag {key}", description: "Test flag for integration tests");
		flag.ActiveEvaluationModes = new EvaluationModes([evaluationMode]);
		return flag;
	}
}
