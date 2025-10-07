using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Propel.FeatureFlags.AspNetCore;
using Propel.FeatureFlags.AspNetCore.Middleware;
using Propel.FeatureFlags.Clients;
using System.Security.Claims;

namespace UnitTests.AspNetCore;

public class FeatureFlagMiddlewareTests
{
    private readonly Mock<RequestDelegate> _mockNext;
    private readonly Mock<IGlobalFlagClient> _mockGlobalFlags;
    private readonly Mock<IApplicationFlagClient> _mockApplicationFlags;
    private readonly Mock<ILogger<FeatureFlagMiddleware>> _mockLogger;
    private readonly DefaultHttpContext _httpContext;

    public FeatureFlagMiddlewareTests()
    {
        _mockNext = new Mock<RequestDelegate>();
        _mockGlobalFlags = new Mock<IGlobalFlagClient>();
        _mockApplicationFlags = new Mock<IApplicationFlagClient>();
        _mockLogger = new Mock<ILogger<FeatureFlagMiddleware>>();
        _httpContext = new DefaultHttpContext();
    }

    #region InvokeAsync - Maintenance Mode Tests

    [Fact]
    public async Task InvokeAsync_ShouldReturn503_WhenMaintenanceModeIsEnabled()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            EnableMaintenanceMode = true,
            MaintenanceFlagKey = "maintenance-mode"
        };

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "maintenance-mode",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.ShouldBe(503);
        _mockNext.Verify(n => n(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ShouldProceed_WhenMaintenanceModeIsDisabled()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            EnableMaintenanceMode = false
        };

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.ShouldNotBe(503);
        _mockNext.Verify(n => n(_httpContext), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseCustomMaintenanceResponse_WhenProvided()
    {
        // Arrange
        var customResponse = new { message = "Custom maintenance message" };
        var options = new FeatureFlagMiddlewareOptions
        {
            EnableMaintenanceMode = true,
            MaintenanceFlagKey = "maintenance-mode",
            MaintenanceResponse = customResponse
        };

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        _httpContext.Response.Body = new MemoryStream();

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.ShouldBe(503);
        _httpContext.Response.ContentType.ShouldBe("application/json");
    }

    #endregion

    #region InvokeAsync - Global Flags Tests

    [Fact]
    public async Task InvokeAsync_ShouldReturnConfiguredStatusCode_WhenGlobalFlagIsDisabled()
    {
        // Arrange
        var globalFlag = new GlobalFlag
        {
            Key = "api-enabled",
            StatusCode = 404,
            Response = new { error = "Feature not available" }
        };

        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [globalFlag]
        };

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "api-enabled",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(false);

        _httpContext.Response.Body = new MemoryStream();

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.ShouldBe(404);
        _mockNext.Verify(n => n(It.IsAny<HttpContext>()), Times.Never);
    }

    [Fact]
    public async Task InvokeAsync_ShouldProceed_WhenAllGlobalFlagsAreEnabled()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags =
            [
                new GlobalFlag { Key = "flag1", StatusCode = 403 },
                new GlobalFlag { Key = "flag2", StatusCode = 404 }
            ]
        };

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            It.IsAny<string>(),
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync(
            "flag1",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()), Times.Once);
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync(
            "flag2",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldStopAtFirstDisabledGlobalFlag()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags =
            [
                new GlobalFlag { Key = "flag1", StatusCode = 403 },
                new GlobalFlag { Key = "flag2", StatusCode = 404 },
                new GlobalFlag { Key = "flag3", StatusCode = 500 }
            ]
        };

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync("flag1", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);
        _mockGlobalFlags.Setup(g => g.IsEnabledAsync("flag2", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(false);

        _httpContext.Response.Body = new MemoryStream();

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Response.StatusCode.ShouldBe(404);
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync("flag3", It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<Dictionary<string, object>?>()), Times.Never);
        _mockNext.Verify(n => n(It.IsAny<HttpContext>()), Times.Never);
    }

    #endregion

    #region InvokeAsync - Context Items Tests

    [Fact]
    public async Task InvokeAsync_ShouldAddFeatureFlagEvaluatorToContextItems()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions();

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _httpContext.Items.ShouldContainKey("FeatureFlagEvaluator");
        _httpContext.Items["FeatureFlagEvaluator"].ShouldBeOfType<HttpContextFeatureFlagEvaluator>();
    }

    [Fact]
    public async Task InvokeAsync_ShouldCallNextMiddleware_WhenNoFlagsConfigured()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions();

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockNext.Verify(n => n(_httpContext), Times.Once);
    }

    #endregion

    #region Tenant ID Extraction Tests

    [Fact]
    public async Task InvokeAsync_ShouldExtractTenantIdFromHeader()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }]
        };

        _httpContext.Request.Headers["X-Tenant-ID"] = "tenant-123";

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            "tenant-123",
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync(
            "test-flag",
            "tenant-123",
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldExtractTenantIdFromQueryString()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }]
        };

        _httpContext.Request.QueryString = new QueryString("?tenantId=tenant-456");

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            "tenant-456",
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync(
            "test-flag",
            "tenant-456",
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseCustomTenantIdExtractor()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }],
            TenantIdExtractor = ctx => "custom-tenant-789"
        };

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            "custom-tenant-789",
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync(
            "test-flag",
            "custom-tenant-789",
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()), Times.Once);
    }

    #endregion

    #region User ID Extraction Tests

    [Fact]
    public async Task InvokeAsync_ShouldExtractUserIdFromIdentityName()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }]
        };

        var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "user-123") }, "TestAuth");
        _httpContext.User = new ClaimsPrincipal(identity);

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            "user-123",
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            "user-123",
            It.IsAny<Dictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldExtractUserIdFromHeader()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }]
        };

        _httpContext.Request.Headers["X-User-ID"] = "user-456";

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            "user-456",
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            "user-456",
            It.IsAny<Dictionary<string, object>?>()), Times.Once);
    }

    [Fact]
    public async Task InvokeAsync_ShouldUseCustomUserIdExtractor()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }],
            UserIdExtractor = ctx => "custom-user-789"
        };

        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            "custom-user-789",
            It.IsAny<Dictionary<string, object>?>()))
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        _mockGlobalFlags.Verify(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            "custom-user-789",
            It.IsAny<Dictionary<string, object>?>()), Times.Once);
    }

    #endregion

    #region Attribute Extraction Tests

    [Fact]
    public async Task InvokeAsync_ShouldExtractBaseAttributes()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }]
        };

        _httpContext.Request.Headers["User-Agent"] = "TestAgent/1.0";
        _httpContext.Request.Path = "/api/test";
        _httpContext.Request.Method = "GET";
        _httpContext.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("192.168.1.1");

        Dictionary<string, object>? capturedAttributes = null;
        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string?, string?, Dictionary<string, object>?>((_, _, _, attrs) => capturedAttributes = attrs)
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        capturedAttributes.ShouldNotBeNull();
        capturedAttributes.ShouldContainKey("userAgent");
        capturedAttributes.ShouldContainKey("ipAddress");
        capturedAttributes.ShouldContainKey("path");
        capturedAttributes.ShouldContainKey("method");
        capturedAttributes["method"].ShouldBe("GET");

        var value = (PathString)capturedAttributes["path"];
		value.ToString().ShouldBe("/api/test");
    }

    [Fact]
    public async Task InvokeAsync_ShouldApplyCustomAttributeExtractors()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }],
            AttributeExtractors =
            [
                ctx => new Dictionary<string, object> { ["customAttribute"] = "customValue" },
                ctx => new Dictionary<string, object> { ["anotherAttribute"] = 42 }
            ]
        };

        Dictionary<string, object>? capturedAttributes = null;
        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string?, string?, Dictionary<string, object>?>((_, _, _, attrs) => capturedAttributes = attrs)
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        capturedAttributes.ShouldNotBeNull();
        capturedAttributes.ShouldContainKey("customAttribute");
        capturedAttributes.ShouldContainKey("anotherAttribute");
        capturedAttributes["customAttribute"].ShouldBe("customValue");
        capturedAttributes["anotherAttribute"].ShouldBe(42);
    }

    [Fact]
    public async Task InvokeAsync_ShouldContinueOnAttributeExtractorException()
    {
        // Arrange
        var options = new FeatureFlagMiddlewareOptions
        {
            GlobalFlags = [new GlobalFlag { Key = "test-flag" }],
            AttributeExtractors =
            [
                ctx => throw new Exception("Extractor error"),
                ctx => new Dictionary<string, object> { ["validAttribute"] = "value" }
            ]
        };

        Dictionary<string, object>? capturedAttributes = null;
        _mockGlobalFlags.Setup(g => g.IsEnabledAsync(
            "test-flag",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<Dictionary<string, object>?>()))
            .Callback<string, string?, string?, Dictionary<string, object>?>((_, _, _, attrs) => capturedAttributes = attrs)
            .ReturnsAsync(true);

        var middleware = new FeatureFlagMiddleware(
            _mockNext.Object,
            _mockGlobalFlags.Object,
            _mockApplicationFlags.Object,
            _mockLogger.Object,
            options);

        // Act
        await middleware.InvokeAsync(_httpContext);

        // Assert
        capturedAttributes.ShouldNotBeNull();
        capturedAttributes.ShouldContainKey("validAttribute");
        _mockNext.Verify(n => n(_httpContext), Times.Once);
    }

    #endregion
}