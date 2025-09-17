using FlagsManagementApi.IntegrationTests.Support;
using Propel.FeatureFlags.Domain;
using Propel.FlagsManagement.Api.Endpoints;
using Propel.FlagsManagement.Api.Endpoints.Dto;

namespace FlagsManagementApi.IntegrationTests.Handlers;

public class FlagEvaluationHandler_SingleFlag(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_EnabledFlagExists_ThenReturnsEnabledResult()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("eval-enabled-flag", EvaluationMode.Enabled);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 100);

		await fixture.ManagementRepository.CreateAsync(flag);

		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		// Act
		var result = await fixture.GetHandler<FlagEvaluationHandler>().HandleAsync(
			flag.Key.Key,
			headers,
			tenantId: null,
			userId: "test-user");

		// Assert
		var evaluationResult = result.ShouldBeOfType<Ok<EvaluationResult>>();
		evaluationResult.Value.ShouldNotBeNull();
		evaluationResult.Value.IsEnabled.ShouldBeTrue();

	}
}

public class FlagEvaluationHandler_WithAttributes(FlagsManagementApiFixture fixture) : IClassFixture<FlagsManagementApiFixture>
{
	[Fact]
	public async Task If_AttributesProvided_ThenEvaluatesWithContext()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("attr-flag", EvaluationMode.UserTargeted);
		flag.TargetingRules =
		[
			TargetingRuleFactory
			.CreateTargetingRule(attribute: "region",
			op: TargetingOperator.In,
			values: ["US", "CA"],
			variation: "region-specific")
		];

		await fixture.ManagementRepository.CreateAsync(flag);

		var attributes = new Dictionary<string, object> { { "region", "US" }, { "plan", "premium" } };
		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		// Act
		var result = await fixture.GetHandler<FlagEvaluationHandler>().HandleAsync(
								flag.Key.Key,
								headers,
								tenantId: null,
								userId: "test-user",
								attributes: attributes);
		// Assert
		var evaluationResult = result.ShouldBeOfType<Ok<EvaluationResult>>();
		evaluationResult.Value.ShouldNotBeNull();
		evaluationResult.Value.IsEnabled.ShouldBeTrue();
	}

	[Fact]
	public async Task If_JsonAttributesProvided_ThenParsesAndEvaluates()
	{
		// Arrange
		await fixture.ClearAllData();

		var flag = TestHelpers.CreateApplicationFlag("json-attr-flag", EvaluationMode.Enabled);
		flag.UserAccessControl = new AccessControl(rolloutPercentage: 100);

		await fixture.ManagementRepository.CreateAsync(flag);

		var jsonAttributes = """{"country":"US","plan":"premium"}""";
		var headers = new FlagRequestHeaders(Scope.Application.ToString(), flag.Key.ApplicationName, flag.Key.ApplicationVersion);
		// Act
		var result = await fixture.GetHandler<FlagEvaluationHandler>().HandleAsync(
						flag.Key.Key,
						headers,
						tenantId: null,
						userId: "test-user",
						kvAttributes: jsonAttributes);

		// Assert
		var evaluationResult = result.ShouldBeOfType<Ok<EvaluationResult>>();
		evaluationResult.Value.ShouldNotBeNull();
		evaluationResult.Value.IsEnabled.ShouldBeTrue();
	}
}