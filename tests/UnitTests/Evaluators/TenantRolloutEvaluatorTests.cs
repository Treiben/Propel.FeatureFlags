using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace UnitTests.Evaluators;

public class TenantRolloutEvaluatorTests
{
    private readonly TenantRolloutEvaluator _evaluator = new();

    [Theory]
    [InlineData(EvaluationMode.TenantRolloutPercentage)]
    [InlineData(EvaluationMode.TenantTargeted)]
    public void CanProcess_ShouldReturnTrue_WhenTenantRolloutModesArePresent(EvaluationMode mode)
    {
        // Arrange
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { mode }));
        var context = new EvaluationContext(tenantId: "tenant123");

        // Act
        var canProcess = _evaluator.CanProcess(options, context);

        // Assert
        canProcess.ShouldBeTrue();
    }

    [Fact]
    public void CanProcess_ShouldReturnTrue_WhenTenantAccessControlHasRestrictions()
    {
        // Arrange
        var tenantAccessControl = new AccessControl(
            allowed: new List<string> { "tenant1" },
            rolloutPercentage: 50);
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.On }),
            tenantAccessControl: tenantAccessControl);
        var context = new EvaluationContext(tenantId: "tenant1");

        // Act
        var canProcess = _evaluator.CanProcess(options, context);

        // Assert
        canProcess.ShouldBeTrue();
    }

    [Fact]
    public async Task Evaluate_ShouldThrowException_WhenTenantIdIsNullOrEmpty()
    {
        // Arrange
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.TenantRolloutPercentage }));
        var context = new EvaluationContext(tenantId: null);

        // Act & Assert
        await Should.ThrowAsync<EvaluationOptionsArgumentException>(
            async () => await _evaluator.Evaluate(options, context));
    }

    [Fact]
    public async Task Evaluate_ShouldReturnAllowedResult_WhenTenantIsInAllowedList()
    {
        // Arrange
        var tenantAccessControl = new AccessControl(
            allowed: new List<string> { "tenant123" },
            rolloutPercentage: 0);
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.TenantTargeted }),
            tenantAccessControl: tenantAccessControl);
        var context = new EvaluationContext(tenantId: "tenant123");

        // Act
        var result = await _evaluator.Evaluate(options, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Reason.ShouldBe("Access is allowed");
    }

    [Fact]
    public async Task Evaluate_ShouldReturnDeniedResult_WhenTenantIsInBlockedList()
    {
        // Arrange
        var tenantAccessControl = new AccessControl(
            blocked: new List<string> { "blocked-tenant" },
            rolloutPercentage: 100);
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.TenantRolloutPercentage }),
            tenantAccessControl: tenantAccessControl);
        var context = new EvaluationContext(tenantId: "blocked-tenant");

        // Act
        var result = await _evaluator.Evaluate(options, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeFalse();
        result.Reason.ShouldBe("Access is blocked");
    }
}