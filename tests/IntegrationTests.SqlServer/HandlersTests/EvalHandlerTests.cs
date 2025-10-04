using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Dashboard.Api.Domain;
using Propel.FeatureFlags.Dashboard.Api.Endpoints;
using Propel.FeatureFlags.Dashboard.Api.Endpoints.Dto;
using Propel.FeatureFlags.Domain;

namespace IntegrationTests.SqlServer.HandlersTests;

public class FlagEvaluationHandlerTests(HandlersTestsFixture fixture)
	: IClassFixture<HandlersTestsFixture>, IAsyncLifetime
{
	[Fact]
	public async Task Should_evaluate_global_flag_successfully()
	{
		// Arrange
		var identifier = new FlagIdentifier("eval-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Eval Flag",
						Description: "For evaluation",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<FlagEvaluationHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// Act
		var result = await handler.HandleAsync("eval-flag", headers, null, null, null, null, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
	}

	[Fact]
	public async Task Should_evaluate_flag_with_tenant_context()
	{
		// Arrange
		var identifier = new FlagIdentifier("tenant-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Tenant Flag",
						Description: "For tenant evaluation",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<FlagEvaluationHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// Act
		var result = await handler.HandleAsync("tenant-flag", headers, "tenant-123", null, null, null, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
	}

	[Fact]
	public async Task Should_evaluate_flag_with_user_context()
	{
		// Arrange
		var identifier = new FlagIdentifier("user-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "User Flag",
						Description: "For user evaluation",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<FlagEvaluationHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// Act
		var result = await handler.HandleAsync("user-flag", headers, null, "user-456", null, null, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
	}

	[Fact]
	public async Task Should_evaluate_flag_with_kv_attributes()
	{
		// Arrange
		var identifier = new FlagIdentifier("attrs-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Attributes Flag",
						Description: "With attributes",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<FlagEvaluationHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var kvAttributes = "{\"country\":\"US\",\"plan\":\"premium\"}";

		// Act
		var result = await handler.HandleAsync("attrs-flag", headers, null, null, kvAttributes, null, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<Ok<EvaluationResult>>();
	}

	[Fact]
	public async Task Should_return_bad_request_for_invalid_attributes_format()
	{
		// Arrange
		var identifier = new FlagIdentifier("invalid-attrs-flag", Scope.Global, applicationName: "global", applicationVersion: "0.0.0.0");
		var flag = new FeatureFlag(identifier,
			new FlagAdministration(Name: "Invalid Attrs",
						Description: "Test invalid format",
						RetentionPolicy: RetentionPolicy.GlobalPolicy,
						Tags: [],
						ChangeHistory: [AuditTrail.FlagCreated("test-user", null)]),
			FlagEvaluationOptions.DefaultOptions);

		await fixture.DashboardRepository.CreateAsync(flag, CancellationToken.None);

		var handler = fixture.Services.GetRequiredService<FlagEvaluationHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);
		var invalidKvAttributes = "not-valid-json";

		// Act
		var result = await handler.HandleAsync("invalid-attrs-flag", headers, null, null, invalidKvAttributes, null, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)result;
		problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
	}

	[Fact]
	public async Task Should_return_404_when_flag_not_found()
	{
		// Arrange
		var handler = fixture.Services.GetRequiredService<FlagEvaluationHandler>();
		var headers = new FlagRequestHeaders("Global", null, null);

		// Act
		var result = await handler.HandleAsync("non-existent-flag", headers, null, null, null, null, CancellationToken.None);

		// Assert
		result.ShouldBeOfType<ProblemHttpResult>();
		var problem = (ProblemHttpResult)result;
		problem.StatusCode.ShouldBe(StatusCodes.Status404NotFound);
	}

	public Task InitializeAsync() => Task.CompletedTask;
	public Task DisposeAsync() => fixture.ClearAllData();
}