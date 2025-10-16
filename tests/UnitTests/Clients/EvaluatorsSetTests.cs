using Propel.FeatureFlags.Clients;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluators;

namespace UnitTests.Clients;

public class EvaluatorsSetTests
{
    [Fact]
    public async Task Evaluate_ShouldReturnDisabledResult_WhenModeSetContainsOff()
    {
        // Arrange
        var evaluator = new Mock<IEvaluator>();
        var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { evaluator.Object });
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.Off }));
        var context = new EvaluationContext();

        // Act
        var result = await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeFalse();
        result.Reason.ShouldContain("explicitly disabled");
        evaluator.Verify(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()), Times.Never);
    }

    [Fact]
    public async Task Evaluate_ShouldReturnEnabledResult_WhenModeSetContainsOn()
    {
        // Arrange
        var evaluator = new Mock<IEvaluator>();
        var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { evaluator.Object });
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.On }));
        var context = new EvaluationContext();

        // Act
        var result = await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Reason.ShouldContain("explicitly enabled");
        evaluator.Verify(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()), Times.Never);
    }

    [Fact]
    public async Task Evaluate_ShouldReturnNull_WhenNoEvaluatorCanProcess()
    {
        // Arrange
        var evaluator = new Mock<IEvaluator>();
        evaluator.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(false);
        
        var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { evaluator.Object });
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.Scheduled }));
        var context = new EvaluationContext();

        // Act
        var result = await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task Evaluate_ShouldReturnEvaluatorResult_WhenSingleEvaluatorProcesses()
    {
        // Arrange
        var evaluator = new Mock<IEvaluator>();
        var expectedResult = new EvaluationResult(isEnabled: true, variation: "v1", reason: "Single evaluator result");
        
        evaluator.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.UserRollout);
        evaluator.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        evaluator.Setup(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .ReturnsAsync(expectedResult);
        
        var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { evaluator.Object });
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.UserRolloutPercentage }));
        var context = new EvaluationContext();

        // Act
        var result = await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Variation.ShouldBe("v1");
        result.Reason.ShouldBe("Single evaluator result");
    }

    [Fact]
    public async Task Evaluate_ShouldReturnCombinedResult_WhenMultipleEvaluatorsProcessAndAllReturnEnabled_ScheduleBeforeAccessControl()
    {
        // Arrange
        var userRollout = new Mock<IEvaluator>();
        var schedule = new Mock<IEvaluator>();
        
        userRollout.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.UserRollout);
        userRollout.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        userRollout.Setup(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .ReturnsAsync(new EvaluationResult(isEnabled: true, variation: "userRolloutVariation", reason: "First evaluator"));
        
        schedule.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.ActivationSchedule);
        schedule.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        schedule.Setup(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .ReturnsAsync(new EvaluationResult(isEnabled: true, variation: "scheduleVariation", reason: "Second evaluator"));
        
        var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { userRollout.Object, schedule.Object });
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.UserRolloutPercentage, EvaluationMode.Scheduled }));
        var context = new EvaluationContext();

        // Act
        var result = await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Variation.ShouldBe("userRolloutVariation"); // Last evaluator's variation
        result.Reason.ShouldBe("All configured conditions met for feature flag activation");
    }

    [Fact]
    public async Task Evaluate_ShouldReturnFirstDisabledResult_WhenAnyEvaluatorReturnsDisabled()
    {
        // Arrange
        var userRollout = new Mock<IEvaluator>();
        var schedule = new Mock<IEvaluator>();
        var disabledResult = new EvaluationResult(isEnabled: false, reason: "User rollout evaluator disabled");
        
        userRollout.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.UserRollout);
        userRollout.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        userRollout.Setup(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .ReturnsAsync(disabledResult);
        
        schedule.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.ActivationSchedule);
        schedule.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        
        var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { userRollout.Object, schedule.Object });
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.Scheduled, EvaluationMode.UserRolloutPercentage }));
        var context = new EvaluationContext();

        // Act
        var result = await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeFalse();
        result.Reason.ShouldBe("User rollout evaluator disabled");
    }

    [Fact]
    public async Task Evaluate_ShouldEvaluateInCorrectOrder_WhenMultipleEvaluatorsCanProcess_TimeWindowBeforeAccessControl()
    {
        // Arrange
        var callOrder = new List<string>();
        
        var timeWindowEvaluator = new Mock<IEvaluator>();
        timeWindowEvaluator.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.OperationalWindow);
        timeWindowEvaluator.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        timeWindowEvaluator.Setup(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Callback(() => callOrder.Add("time-window-evaluator"))
            .ReturnsAsync(new EvaluationResult(isEnabled: true));
        
        var tenantRolloutEvaluator = new Mock<IEvaluator>();
        tenantRolloutEvaluator.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.TenantRollout);
        tenantRolloutEvaluator.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        tenantRolloutEvaluator.Setup(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Callback(() => callOrder.Add("tenant-rollout-evaluator"))
            .ReturnsAsync(new EvaluationResult(isEnabled: true));
        
        // Create with unordered set
        var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { tenantRolloutEvaluator.Object, timeWindowEvaluator.Object });
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.Scheduled }));
        var context = new EvaluationContext();

        // Act
        await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        callOrder.Count.ShouldBe(2);
        callOrder[0].ShouldBe("time-window-evaluator"); // TenantRollout (1) comes before OperationalWindow (4)
        callOrder[1].ShouldBe("tenant-rollout-evaluator");
    }

    [Fact]
    public async Task Evaluate_ShouldSkipEvaluatorsReturningNull_AndContinueToNext()
    {
        // Arrange
        var userRollout = new Mock<IEvaluator>();
        var schedule = new Mock<IEvaluator>();
        var expectedResult = new EvaluationResult(isEnabled: true, reason: "Second evaluator result");
        
        userRollout.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.UserRollout);
        userRollout.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        userRollout.Setup(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ReturnsAsync(expectedResult);
        
        schedule.Setup(e => e.EvaluationOrder).Returns(EvaluationOrder.ActivationSchedule);
        schedule.Setup(e => e.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
            .Returns(true);
        schedule.Setup(e => e.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ReturnsAsync((EvaluationResult?)null);

		var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { userRollout.Object, schedule.Object });
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.Scheduled }));
        var context = new EvaluationContext();

        // Act
        var result = await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Reason.ShouldBe("All configured conditions met for feature flag activation");
    }

    [Fact]
    public async Task Evaluate_ShouldIncludeDefaultVariation_WhenTerminalStateIsEvaluated()
    {
        // Arrange
        var evaluator = new Mock<IEvaluator>();
        var evaluatorsSet = new EvaluatorsSet(new HashSet<IEvaluator> { evaluator.Object });
        
        var variations = new Variations
        {
            Values = new Dictionary<string, object>
            {
                { "control", "value1" },
                { "treatment", "value2" }
            },
            DefaultVariation = "control"
        };
        
        var evaluationOptions = new EvaluationOptions(
            "test-flag",
            new ModeSet(new HashSet<EvaluationMode> { EvaluationMode.On }),
            variations: variations);
        var context = new EvaluationContext();

        // Act
        var result = await evaluatorsSet.Evaluate(evaluationOptions, context);

        // Assert
        result.ShouldNotBeNull();
        result.IsEnabled.ShouldBeTrue();
        result.Variation.ShouldBe("control");
    }
}