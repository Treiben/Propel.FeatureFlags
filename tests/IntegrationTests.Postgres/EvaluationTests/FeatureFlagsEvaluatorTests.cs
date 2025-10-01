using FeatureFlags.IntegrationTests.Postgres.Support;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;

namespace FeatureFlags.IntegrationTests.Postgres.EvaluationTests;

public class Evaluate(FlagEvaluationTestsFixture fixture) : IClassFixture<FlagEvaluationTestsFixture>
{
	[Fact]
	public async Task IfFlagExists_And_Enabled_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var identifier = new FlagIdentifier("enabled-flag", Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version);
		await fixture.FeatureFlagRepository.CreateApplicationFlagAsync(
				identifier, EvaluationMode.On,
				"Created Before Evaluation",
				"Flag must be created in enabled mode prior to evaluation");

		var context = new EvaluationContext(userId: "user123");
		var flag = new ApplicationFeatureFlag(
				key: "enabled-flag",
				name: "Created Before Evaluation",
				description: "Flag must be created in enabled mode prior to evaluation",
				defaultMode: EvaluationMode.Off);

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task IfFlagNotExists_ThenReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var context = new EvaluationContext(userId: "user123");
		var flag = new ApplicationFeatureFlag(
			key: "flag-dont-exist-as-disabled",
			name: "Disabled On Create Flag",
			description: "Flag must be created in disabled mode",
			defaultMode: EvaluationMode.Off);

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public async Task IfFlagNotExists_ButRequestedInEnabledMode_ThenReturnsEnabled()
	{
		// Arrange
		await fixture.ClearAllData();

		var context = new EvaluationContext(userId: "user123");
		var flag = new ApplicationFeatureFlag(
			key: "flag-dont-exist-as-enabled",
			name: "Enabled On Create Flag",
			description: "Flag must be created in enabled mode",
			defaultMode: EvaluationMode.On);

		// Act
		var result = await fixture.Evaluator.Evaluate(flag, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
	}
}
