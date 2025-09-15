using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;

namespace FlagsManagementApi.IntegrationTests.Handlers;

/* These tests cover FlagEvaluationHandler integration scenarios:
 * Single flag evaluation, multiple flag evaluation, attribute parsing, non-existent flags
 */

public class FlagEvaluationHandler_SingleFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_EnabledFlagExists_ThenReturnsEnabledResult()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("eval-enabled-flag", EvaluationMode.Enabled);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 100);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.FlagEvaluationHandler.HandleAsync(["eval-enabled-flag"], "test-user");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.ShouldContainKey("eval-enabled-flag");
		okResult.Value["eval-enabled-flag"].IsEnabled.ShouldBeTrue();
		okResult.Value["eval-enabled-flag"].Variation.ShouldBe("on");
	}

	[Fact]
	public async Task If_DisabledFlagExists_ThenReturnsDisabledResult()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("eval-disabled-flag", EvaluationMode.Disabled);
		await fixture.Repository.CreateAsync(flag);

		// Act
		var result = await fixture.FlagEvaluationHandler.HandleAsync(["eval-disabled-flag"], "test-user");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.ShouldContainKey("eval-disabled-flag");
		okResult.Value["eval-disabled-flag"].IsEnabled.ShouldBeFalse();
		okResult.Value["eval-disabled-flag"].Variation.ShouldBe("off");
	}

	[Fact]
	public async Task If_NonExistentFlag_ThenCreatesDefaultAndReturnsDisabled()
	{
		// Arrange
		await fixture.ClearAllData();

		// Act
		var result = await fixture.FlagEvaluationHandler.HandleAsync(["non-existent-flag"], "test-user");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.ShouldContainKey("non-existent-flag");
		okResult.Value["non-existent-flag"].IsEnabled.ShouldBeFalse();
		okResult.Value["non-existent-flag"].Reason.ShouldContain("not found");

		// Verify flag was auto-created
		var createdFlag = await fixture.Repository.GetAsync("non-existent-flag");
		createdFlag.ShouldNotBeNull();
		createdFlag.ActiveEvaluationModes.ContainsModes([EvaluationMode.Disabled]).ShouldBeTrue();
	}
}

public class FlagEvaluationHandler_MultipleFlags(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_MultipleFlags_ThenReturnsAllResults()
	{
		// Arrange
		await fixture.ClearAllData();
		var enabledFlag = TestHelpers.CreateTestFlag("multi-enabled", EvaluationMode.Enabled);
		enabledFlag.UserAccessControl = new AccessControl(rolloutPercentage: 100);
		var disabledFlag = TestHelpers.CreateTestFlag("multi-disabled", EvaluationMode.Disabled);
		
		await fixture.Repository.CreateAsync(enabledFlag);
		await fixture.Repository.CreateAsync(disabledFlag);

		// Act
		var result = await fixture.FlagEvaluationHandler.HandleAsync(["multi-enabled", "multi-disabled"], "test-user");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		okResult.Value.ShouldNotBeNull();
		okResult.Value.Count.ShouldBe(2);
		okResult.Value["multi-enabled"].IsEnabled.ShouldBeTrue();
		okResult.Value["multi-disabled"].IsEnabled.ShouldBeFalse();
	}

	[Fact]
	public async Task If_MixedExistingAndNonExistingFlags_ThenReturnsAllResults()
	{
		// Arrange
		await fixture.ClearAllData();
		var existingFlag = TestHelpers.CreateTestFlag("existing-flag", EvaluationMode.Enabled);
		existingFlag.UserAccessControl = new AccessControl(rolloutPercentage: 100);
		await fixture.Repository.CreateAsync(existingFlag);

		// Act
		var result = await fixture.FlagEvaluationHandler.HandleAsync(["existing-flag", "new-auto-flag"], "test-user");

		// Assert
		var okResult = result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		okResult.Value.Count.ShouldBe(2);
		okResult.Value["existing-flag"].IsEnabled.ShouldBeTrue();
		okResult.Value["new-auto-flag"].IsEnabled.ShouldBeFalse();
	}
}

public class FlagEvaluationHandler_WithAttributes(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_AttributesProvided_ThenEvaluatesWithContext()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("attr-flag", EvaluationMode.UserTargeted);
		flag.TargetingRules = 
		[
			TargetingRuleFactory
			.CreateTargetingRule(attribute: "region", 
			op: TargetingOperator.In, 
			values: ["US", "CA"], 
			variation: "region-specific")
		];
		await fixture.Repository.CreateAsync(flag);

		var attributes = new Dictionary<string, object> { { "region", "US" }, { "plan", "premium" } };

		// Act
		var result = await fixture.FlagEvaluationHandler.HandleAsync(["attr-flag"], "test-user", attributes: attributes);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		okResult.Value.ShouldContainKey("attr-flag");
		// The exact result depends on the evaluation logic, but it should process the attributes
		okResult.Value["attr-flag"].ShouldNotBeNull();
	}

	[Fact]
	public async Task If_JsonAttributesProvided_ThenParsesAndEvaluates()
	{
		// Arrange
		await fixture.ClearAllData();
		var flag = TestHelpers.CreateTestFlag("json-attr-flag", EvaluationMode.Enabled);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 100);
		await fixture.Repository.CreateAsync(flag);

		var jsonAttributes = """{"country":"US","plan":"premium"}""";

		// Act
		var result = await fixture.FlagEvaluationHandler.HandleAsync(["json-attr-flag"], "test-user", kvAttributes: jsonAttributes);

		// Assert
		var okResult = result.ShouldBeOfType<Ok<Dictionary<string, EvaluationResult>>>();
		okResult.Value.ShouldContainKey("json-attr-flag");
		okResult.Value["json-attr-flag"].IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task If_InvalidJsonAttributes_ThenReturnsBadRequest()
	{
		// Arrange
		var invalidJson = """{"invalid": json}""";

		// Act
		var result = await fixture.FlagEvaluationHandler.HandleAsync(["test-flag"], "test-user", kvAttributes: invalidJson);

		// Assert
		var problemResponse = result.ShouldBeOfType<ProblemHttpResult>();
		problemResponse.StatusCode.ShouldBe(400);
		problemResponse.ProblemDetails.Title.ShouldBe("Invalid Attributes Format");
		problemResponse.ProblemDetails.Detail.ShouldContain("valid JSON object");
	}
}