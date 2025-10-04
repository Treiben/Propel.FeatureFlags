using FeatureFlags.IntegrationTests.SqlServer.EvaluationTests;
using FeatureFlags.IntegrationTests.SqlServer.Support;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace FeatureFlags.IntegrationTests.EvaluationTests.EvaluationTests;

public class IsEnabledAsync_WithEnabledFlag(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task If_FlagDoesNotExist_CreateItDuringEvaluation()
	{
		// Arrange
		await fixture.ClearAllData();

		var featureFlag = new ApplicationFeatureFlag(
			key: "client-enabled",
			name: "Client Enabled Flag",
			description: "Flag created during client evaluation",
			defaultMode: EvaluationMode.On);

		// Act
		var result = await fixture.Client.IsEnabledAsync(featureFlag, userId: "user123");

		// Assert
		result.ShouldBeTrue();

		// Verify the flag was created in the repository

		//A Arrange
		var storedFlag = await fixture.FeatureFlagRepository.GetEvaluationOptionsAsync(new FlagIdentifier(
			key: "client-enabled",
			scope: Scope.Application,
			applicationName: ApplicationInfo.Name,
			applicationVersion: ApplicationInfo.Version));

		// Assert	
		storedFlag.ShouldNotBeNull();
		storedFlag.ModeSet.Modes.ShouldContain(EvaluationMode.On);
	}
}