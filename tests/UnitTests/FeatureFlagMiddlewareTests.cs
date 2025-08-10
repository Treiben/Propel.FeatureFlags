using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.AspNetCore;
using Propel.FeatureFlags.AspNetCore.Middleware;
using Propel.FeatureFlags.Client;
using System.Net;
using System.Security.Claims;

namespace FeatureFlags.UnitTests.AspNetCore;

public class FeatureFlagMiddleware_CompleteWorkflow
{
	private readonly FeatureFlagMiddlewareIntegrationTests _tests = new();

	[Fact]
	public async Task If_AllChecksPass_ThenCompleteWorkflow()
	{
		// Arrange
		var context = _tests.CreateHttpContext();
		// Add both ClaimTypes.Name and "sub" claim to ensure UserIdExtractor works
		var identity = new ClaimsIdentity(new[] { 
			new Claim(ClaimTypes.Name, "test-user"),
			new Claim("sub", "test-user") // This is what UserIdExtractor looks for
		}, "test");
		context.User = new ClaimsPrincipal(identity);
		context.Request.Headers["User-Agent"] = "Test/1.0";
		context.Request.Path = "/api/users";
		context.Request.Method = "GET";
		context.Connection.RemoteIpAddress = IPAddress.Parse("127.0.0.1");

		// For the workflow to complete successfully:
		// 1. Maintenance mode should be disabled (false) so it doesn't return 503
		// 2. Global flags should be enabled (true) so they don't return their error status codes
		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"maintenance-mode",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(false); // Maintenance mode disabled

		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"api-v3-enabled",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(true); // Global flag enabled

		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"premium-features",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(true); // Global flag enabled

		// Act
		await _tests._middleware.InvokeAsync(context);

		// Assert
		context.Items.ShouldContainKey("FeatureFlagEvaluator");
		var evaluator = context.Items["FeatureFlagEvaluator"] as HttpContextFeatureFlagEvaluator;
		evaluator.ShouldNotBeNull();

		_tests._mockNext.Verify(x => x(context), Times.Once);

		// Verify maintenance mode was checked
		_tests._mockFeatureFlags.Verify(x => x.IsEnabledAsync(
			"maintenance-mode",
			null,
			"test-user",
			It.IsAny<Dictionary<string, object>?>()), Times.Once);

		// Verify global flags were checked
		_tests._mockFeatureFlags.Verify(x => x.IsEnabledAsync(
			"api-v3-enabled",
			null,
			"test-user",
			It.IsAny<Dictionary<string, object>?>()), Times.Once);

		_tests._mockFeatureFlags.Verify(x => x.IsEnabledAsync(
			"premium-features",
			null,
			"test-user",
			It.IsAny<Dictionary<string, object>?>()), Times.Once);
	}

	[Fact]
	public async Task If_MaintenanceEnabled_ThenStopsBeforeGlobalFlags()
	{
		// Arrange
		var context = _tests.CreateHttpContext();
		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"maintenance-mode",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(true);

		// Act
		await _tests._middleware.InvokeAsync(context);

		// Assert
		context.Response.StatusCode.ShouldBe(503);
		_tests._mockNext.Verify(x => x(It.IsAny<HttpContext>()), Times.Never);

		// Should not check global flags
		_tests._mockFeatureFlags.Verify(x => x.IsEnabledAsync(
			"api-v3-enabled",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()), Times.Never);
	}
}

public class FeatureFlagMiddleware_AttributeExtractorIntegration
{
	private readonly FeatureFlagMiddlewareIntegrationTests _tests = new FeatureFlagMiddlewareIntegrationTests();

	[Fact]
	public async Task If_MultipleAttributeExtractors_ThenCombinesAllAttributes()
	{
		// Arrange
		var context = _tests.CreateHttpContext();
		context.Request.Headers["X-Tenant-ID"] = "tenant-123";
		context.Request.Headers["X-API-Version"] = "v2";
		var identity = new ClaimsIdentity(new[] { new Claim("tier", "premium") }, "test");
		context.User = new ClaimsPrincipal(identity);

		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			It.IsAny<string>(),
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(false);

		// Act
		await _tests._middleware.InvokeAsync(context);

		// Assert
		_tests._mockFeatureFlags.Verify(x => x.IsEnabledAsync(
			"maintenance-mode",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.Is<Dictionary<string, object>>(attrs =>
				// Base attributes
				attrs.ContainsKey("userAgent") &&
				attrs.ContainsKey("ipAddress") &&
				attrs.ContainsKey("path") &&
				attrs.ContainsKey("method") &&
				// Custom attributes from first extractor
				attrs.ContainsKey("tenantId") &&
				attrs["tenantId"].ToString() == "tenant-123" &&
				attrs.ContainsKey("apiVersion") &&
				attrs["apiVersion"].ToString() == "v2" &&
				// Custom attributes from second extractor
				attrs.ContainsKey("userTier") &&
				attrs["userTier"].ToString() == "premium")), Times.Once);
	}

	[Fact]
	public async Task If_AttributeExtractorOverridesBaseAttribute_ThenUsesCustomValue()
	{
		// Arrange
		var context = _tests.CreateHttpContext();
		context.Request.Path = "/original/path";

		// The attribute extractor will override the path
		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			It.IsAny<string>(),
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(false);

		// Act
		await _tests._middleware.InvokeAsync(context);

		// Assert
		_tests._mockFeatureFlags.Verify(x => x.IsEnabledAsync(
			"maintenance-mode",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.Is<Dictionary<string, object>>(attrs =>
				attrs["path"].ToString() == "/custom/override/path")), Times.Once);
	}
}

public class FeatureFlagMiddleware_HttpContextEvaluatorUsage
{
	private readonly FeatureFlagMiddlewareIntegrationTests _tests = new FeatureFlagMiddlewareIntegrationTests();

	[Fact]
	public async Task If_EvaluatorAddedToContext_ThenCanBeUsedDownstream()
	{
		// Arrange
		var context = _tests.CreateHttpContext();
		// Add both ClaimTypes.Name and "sub" claim to ensure UserIdExtractor works
		var identity = new ClaimsIdentity(new[] { 
			new Claim(ClaimTypes.Name, "test-user"),
			new Claim("sub", "test-user") // This is what UserIdExtractor looks for
		}, "test");
		context.User = new ClaimsPrincipal(identity);

		// Setup mocks for the successful path (maintenance mode disabled, global flags enabled)
		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"maintenance-mode",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(false); // Maintenance mode disabled

		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"api-v3-enabled",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(true); // Global flag enabled

		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"premium-features",
			It.IsAny<string?>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(true); // Global flag enabled

		// Mock calls that would be made by the HttpContextFeatureFlagEvaluator
		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"downstream-flag",
			null,
			"test-user",
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(true);

		_tests._mockFeatureFlags.Setup(x => x.GetVariationAsync(
			"config-flag",
			"default-config",
			null,
			"test-user",
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync("custom-config");

		// Act
		await _tests._middleware.InvokeAsync(context);

		// Assert
		var evaluator = context.Items["FeatureFlagEvaluator"] as HttpContextFeatureFlagEvaluator;
		evaluator.ShouldNotBeNull();

		// Test the evaluator works correctly
		var flagResult = await evaluator.IsEnabledAsync("downstream-flag");
		flagResult.ShouldBeTrue();

		var variationResult = await evaluator.GetVariationAsync("config-flag", "default-config");
		variationResult.ShouldBe("custom-config");

		// Verify the calls were made with the correct user and attributes
		_tests._mockFeatureFlags.Verify(x => x.IsEnabledAsync(
			"downstream-flag",
			null,
			"test-user",
			It.IsAny<Dictionary<string, object>?>()), Times.Once);

		_tests._mockFeatureFlags.Verify(x => x.GetVariationAsync(
			"config-flag",
			"default-config",
			null,
			"test-user",
			It.IsAny<Dictionary<string, object>?>()), Times.Once);
	}
}

public class FeatureFlagMiddleware_ErrorScenarios
{
	private readonly FeatureFlagMiddlewareIntegrationTests _tests = new FeatureFlagMiddlewareIntegrationTests();

	[Fact]
	public async Task If_FeatureFlagServiceDown_ThenPropagatesException()
	{
		// Arrange
		var context = _tests.CreateHttpContext();
		var serviceException = new HttpRequestException("Feature flag service is down");

		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"maintenance-mode",
			It.IsAny<string>(),
			It.IsAny<string?>(),
			It.IsAny<Dictionary<string, object>?>()))
			.ThrowsAsync(serviceException);

		// Act & Assert
		var thrownException = await Should.ThrowAsync<HttpRequestException>(
			() => _tests._middleware.InvokeAsync(context));

		thrownException.Message.ShouldBe("Feature flag service is down");
		_tests._mockNext.Verify(x => x(It.IsAny<HttpContext>()), Times.Never);
	}

	[Fact]
	public async Task If_UserIdExtractorThrowsButGlobalFlagCheckSucceeds_ThenContinues()
	{
		// Arrange
		var context = _tests.CreateHttpContext();
		// Set the header that will force the UserIdExtractor to throw
		context.Request.Headers["X-Force-Error"] = "true";
		// Set the fallback header that should be used after the exception
		context.Request.Headers["X-User-ID"] = "fallback-user";

		// Setup mocks for the successful path (maintenance mode disabled, global flags enabled)
		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"maintenance-mode",
			null,
			"fallback-user",
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(false); // Maintenance mode disabled

		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"api-v3-enabled",
			null,
			"fallback-user",
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(true); // Global flag enabled

		_tests._mockFeatureFlags.Setup(x => x.IsEnabledAsync(
			"premium-features",
			null,
			"fallback-user",
			It.IsAny<Dictionary<string, object>?>()))
			.ReturnsAsync(true); // Global flag enabled

		// Act
		await _tests._middleware.InvokeAsync(context);

		// Assert
		_tests._mockNext.Verify(x => x(context), Times.Once);
		context.Items.ShouldContainKey("FeatureFlagEvaluator");
	}
}

public class FeatureFlagMiddlewareIntegrationTests
{
	public readonly Mock<RequestDelegate> _mockNext;
	public readonly Mock<IFeatureFlagClient> _mockFeatureFlags;
	public readonly Mock<ILogger<FeatureFlagMiddleware>> _mockLogger;
	public readonly FeatureFlagMiddleware _middleware;

	public FeatureFlagMiddlewareIntegrationTests()
	{
		_mockNext = new Mock<RequestDelegate>();
		_mockFeatureFlags = new Mock<IFeatureFlagClient>();
		_mockLogger = new Mock<ILogger<FeatureFlagMiddleware>>();

		var options = new FeatureFlagMiddlewareOptions
		{
			EnableMaintenanceMode = true,
			MaintenanceFlagKey = "maintenance-mode",
			GlobalFlags =
			[
				new() {
					FlagKey = "api-v3-enabled",
					StatusCode = 410,
					Response = new { error = "API v3 is deprecated" }
				},
				new() {
					FlagKey = "premium-features",
					StatusCode = 402,
					Response = new { error = "Premium subscription required" }
				}
			],
			AttributeExtractors =
			[
				ctx => new Dictionary<string, object>
				{
					{ "tenantId", ctx.Request.Headers["X-Tenant-ID"].FirstOrDefault() ?? "unknown" },
					{ "apiVersion", ctx.Request.Headers["X-API-Version"].FirstOrDefault() ?? "v1" }
				},
				ctx => new Dictionary<string, object>
				{
					{ "userTier", ctx.User.FindFirst("tier")?.Value ?? "basic" },
					{ "path", "/custom/override/path" } // This will override the base path
				}
			],
			UserIdExtractor = ctx =>
			{
				// This will throw for the error test, but fall back to default extraction
				if (ctx.Request.Headers.ContainsKey("X-Force-Error"))
					throw new InvalidOperationException("Forced error");

				return ctx.User.FindFirst("sub")?.Value;
			}
		};

		_middleware = new FeatureFlagMiddleware(
			_mockNext.Object,
			_mockFeatureFlags.Object,
			_mockLogger.Object,
			options);
	}

	public HttpContext CreateHttpContext()
	{
		var context = new DefaultHttpContext();
		context.Response.Body = new MemoryStream();
		return context;
	}
}
