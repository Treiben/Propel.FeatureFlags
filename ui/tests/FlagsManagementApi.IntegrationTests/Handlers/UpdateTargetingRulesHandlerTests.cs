using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class UpdateTargetingRulesHandler_Success(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ValidStringTargetingRules_ThenUpdatesTargetingRules()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateApplicationFlag("string-targeting-flag", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var targetingRules = new List<TargetingRuleRequest>
		{
			new("userId", TargetingOperator.Equals, ["user123", "user456"], "premium"),
			new("role", TargetingOperator.Contains, ["admin"], "admin-features")
		};

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateTargetingRulesRequest(targetingRules, false);

		// Act
		var result = await fixture.GetHandler<UpdateTargetingRulesHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Modes.ShouldContain(EvaluationMode.TargetingRules);
		okResult.Value.Modes.ShouldNotContain(EvaluationMode.Enabled);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that targeting rules are set correctly
		updatedFlag.TargetingRules.Count.ShouldBe(2);

		// Check that modes are set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TargetingRules]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();

		// Check that audit trail is added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");

		// Check rule details
		var userRule = updatedFlag.TargetingRules.First(r => r.Attribute == "userId");
		userRule.Operator.ShouldBe(TargetingOperator.Equals);
		userRule.Variation.ShouldBe("premium");
	}

	[Fact]
	public async Task If_ValidNumericTargetingRules_ThenUpdatesCorrectly()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateGlobalFlag("numeric-targeting-flag", EvaluationMode.Disabled);
		await fixture.ManagementRepository.CreateAsync(flag);

		var targetingRules = new List<TargetingRuleRequest>
		{
			new("age", TargetingOperator.GreaterThan, ["18"], "adult"),
			new("score", TargetingOperator.LessThan, ["100"], "intermediate")
		};

		var headers = new FlagRequestHeaders(Scope.Global.ToString(), null, null);
		var request = new UpdateTargetingRulesRequest(targetingRules, false);

		// Act
		var result = await fixture.GetHandler<UpdateTargetingRulesHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Modes.ShouldContain(EvaluationMode.TargetingRules);

		// Verify in repository - numeric rules should be created as NumericTargetingRule
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that targeting rules are set correctly
		updatedFlag.TargetingRules.Count.ShouldBe(2);

		// Check that modes are set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TargetingRules]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();

		// Check that audit trail is added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");

		var ageRule = updatedFlag.TargetingRules.First(r => r.Attribute == "age");
		ageRule.ShouldBeOfType<NumericTargetingRule>();
		ageRule.Operator.ShouldBe(TargetingOperator.GreaterThan);
		ageRule.Variation.ShouldBe("adult");
	}
}

public class UpdateTargetingRulesHandler_ReplaceExisting(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_ExistingRules_ThenReplacesWithNewRules()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateApplicationFlag("replace-rules-flag", EvaluationMode.TargetingRules);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule("oldAttribute", TargetingOperator.Equals, ["oldValue"], "old")
		];
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.TargetingRules);
		await fixture.ManagementRepository.CreateAsync(flag);

		var newTargetingRules = new List<TargetingRuleRequest>
		{
			new("newAttribute", TargetingOperator.Contains, ["newValue"], "new")
		};

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateTargetingRulesRequest(newTargetingRules, false);

		// Act
		var result = await fixture.GetHandler<UpdateTargetingRulesHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Modes.ShouldContain(EvaluationMode.TargetingRules);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that targeting rules are set correctly
		updatedFlag.TargetingRules.Count.ShouldBe(1);
		updatedFlag.TargetingRules.First().Attribute.ShouldBe("newAttribute");
		updatedFlag.TargetingRules.First().Variation.ShouldBe("new");

		// Check that modes are set correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TargetingRules]).ShouldBeTrue();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Enabled]).ShouldBeFalse();

		// Check that audit trail is added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");

		// Check rule details
		var userRule = updatedFlag.TargetingRules.First(r => r.Attribute == "newAttribute");
		userRule.Operator.ShouldBe(TargetingOperator.Contains);
		userRule.Variation.ShouldBe("new");
	}

	[Fact]
	public async Task If_EmptyRulesList_ThenClearsTargetingRules()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateApplicationFlag("clear-rules-flag", EvaluationMode.TargetingRules);
		flag.TargetingRules = [
			TargetingRuleFactory.CreateTargetingRule("existingRule", TargetingOperator.Equals, ["value"], "variation")
		];
		flag.ActiveEvaluationModes.AddMode(EvaluationMode.TargetingRules);
		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		var request = new UpdateTargetingRulesRequest([], false);

		// Act
		var result = await fixture.GetHandler<UpdateTargetingRulesHandler>().HandleAsync(flag.Key.Key, headers, request, CancellationToken.None);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<FeatureFlagResponse>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Modes.ShouldNotContain(EvaluationMode.TargetingRules);

		// Verify in repository
		var updatedFlag = await fixture.ManagementRepository.GetAsync(flag.Key);

		updatedFlag.ShouldNotBeNull();

		// Check that targeting rules are cleared
		updatedFlag.TargetingRules.ShouldBeEmpty();

		// Check that modes are updated correctly
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.TargetingRules]).ShouldBeFalse();
		updatedFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();

		// Check that audit trail is added
		updatedFlag.LastModified.ShouldNotBeNull();
		updatedFlag.LastModified.Actor.ShouldBe("test-user");
	}
}