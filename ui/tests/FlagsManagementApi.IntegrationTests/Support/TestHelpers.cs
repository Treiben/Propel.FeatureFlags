using Propel.FeatureFlags.Core;

namespace FlagsManagementApi.IntegrationTests.Support;

public static class TestHelpers
{
	public static FeatureFlag CreateTestFlag(string key, FlagEvaluationMode evaluationMode)
	{
		return new FeatureFlag
		{
			Key = key,
			Name = $"Test Flag {key}",
			Description = "Test flag for integration tests",
			EvaluationModeSet = new FlagEvaluationModeSet([evaluationMode]),
			AuditRecord = new FlagAuditRecord(createdAt: DateTime.UtcNow, createdBy: "integration-test"),
		};
	}
}
