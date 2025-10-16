using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace UnitTests.Evaluators;

public class UserRolloutEvaluatorTests
{
    private readonly UserRolloutEvaluator _evaluator = new();

    [Theory]
    [InlineData(EvaluationMode.UserRolloutPercentage)]
    [InlineData(EvaluationMode.UserTargeted)]
    public void CanProcess_ShouldReturnTrue_WhenUserRolloutModesArePresent(EvaluationMode mode)
    {
        // Arrange
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet([mode]));
        var context = new EvaluationContext(userId: "user123");

        // Act
        var canProcess = _evaluator.CanProcess(options, context);

        // Assert
        canProcess.ShouldBeTrue();
    }

    [Fact]
    public void CanProcess_ShouldReturnTrue_WhenUserAccessControlHasRestrictions()
    {
        // Arrange
        var userAccessControl = new AccessControl(
            allowed: ["user1"],
            rolloutPercentage: 50);
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet([EvaluationMode.On]),
            userAccessControl: userAccessControl);
        var context = new EvaluationContext(userId: "user1");

        // Act
        var canProcess = _evaluator.CanProcess(options, context);

        // Assert
        canProcess.ShouldBeTrue();
    }

    [Fact]
    public async Task Evaluate_ShouldThrowException_WhenUserIdIsNullOrEmpty()
    {
        // Arrange
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet([EvaluationMode.UserRolloutPercentage]));
        var context = new EvaluationContext(userId: null);

        // Act & Assert
        await Should.ThrowAsync<EvaluationOptionsArgumentException>(
            async () => await _evaluator.Evaluate(options, context));
    }

    [Fact]
    public async Task Evaluate_ShouldReturnAllowedResult_WhenUserIsInAllowedList()
    {
        // Arrange
        var userAccessControl = new AccessControl(
            allowed: ["user123"],
            rolloutPercentage: 0);
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet([EvaluationMode.UserTargeted]),
            userAccessControl: userAccessControl);
        var context = new EvaluationContext(userId: "user123");

        // Act
        var result = await _evaluator.Evaluate(options, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Reason.ShouldBe("Access is allowed");
    }

    [Fact]
    public async Task Evaluate_ShouldReturnDeniedResult_WhenUserIsInBlockedList()
    {
        // Arrange
        var userAccessControl = new AccessControl(
            blocked: ["blocked-user"],
            rolloutPercentage: 100);
        var options = new EvaluationOptions(
            "test-flag",
            new ModeSet([EvaluationMode.UserRolloutPercentage]),
            userAccessControl: userAccessControl);
        var context = new EvaluationContext(userId: "blocked-user");

        // Act
        var result = await _evaluator.Evaluate(options, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeFalse();
        result.Reason.ShouldBe("Access is blocked");
    }
}