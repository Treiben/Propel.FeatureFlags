using Propel.FeatureFlags.Domain;

namespace FlagsManagementApi.IntegrationTests.Support;

public static class TestHelpers
{
	public static FeatureFlag CreateTestFlag(string key, EvaluationMode evaluationMode)
	{
		return new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			ActiveEvaluationModes = new EvaluationModes([evaluationMode]),
			Created = Audit.FlagCreated("integration-test"),
		};
	}
}
