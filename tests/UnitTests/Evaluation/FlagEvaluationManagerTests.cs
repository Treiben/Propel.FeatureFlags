using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.FlagEvaluationServices;
using Propel.FeatureFlags.FlagEvaluators;

namespace FeatureFlags.UnitTests.Evaluation;

public class FlagEvaluationManager_Constructor
{
	[Fact]
	public void If_HandlersIsNull_ThenThrowsArgumentNullException()
	{
		// Arrange & Act & Assert
		var exception = Should.Throw<ArgumentNullException>(() => new FlagEvaluationManager(null!));
		exception.ParamName.ShouldBe("handlers");
	}

	[Fact]
	public void If_HandlersProvided_ThenOrdersByEvaluationOrder()
	{
		// Arrange
		var terminalEvaluator = new Mock<IOptionsEvaluator>();
		terminalEvaluator.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.Terminal);
		
		var tenantEvaluator = new Mock<IOptionsEvaluator>();
		tenantEvaluator.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.TenantRollout);
		
		var userEvaluator = new Mock<IOptionsEvaluator>();
		userEvaluator.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.UserRollout);

		var handlers = new HashSet<IOptionsEvaluator>
		{
			terminalEvaluator.Object,
			tenantEvaluator.Object,
			userEvaluator.Object
		};

		// Act
		var manager = new FlagEvaluationManager(handlers);

		// Assert - Should not throw and should create manager
		manager.ShouldNotBeNull();
	}

	[Fact]
	public void If_EmptyHandlersSet_ThenCreatesManagerWithNoHandlers()
	{
		// Arrange
		var handlers = new HashSet<IOptionsEvaluator>();

		// Act
		var manager = new FlagEvaluationManager(handlers);

		// Assert
		manager.ShouldNotBeNull();
	}
}

public class FlagEvaluationManager_ProcessEvaluation_SingleHandler
{
	[Fact]
	public async Task If_HandlerCanProcessAndReturnsDisabled_ThenReturnsDisabledResult()
	{
		// Arrange
		var mockHandler = new Mock<IOptionsEvaluator>();
		mockHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.Terminal);
		mockHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		mockHandler.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ReturnsAsync(new EvaluationResult(isEnabled: false, variation: "disabled", reason: "Handler disabled"));

		var manager = new FlagEvaluationManager(new HashSet<IOptionsEvaluator> { mockHandler.Object });
		
		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				variations: new Variations { DefaultVariation = "default" }
			);

		var context = new EvaluationContext();

		// Act
		var result = await manager.ProcessEvaluation(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled");
		result.Reason.ShouldBe("Handler disabled");
	}

	[Fact]
	public async Task If_HandlerCannotProcess_ThenSkipsHandler()
	{
		// Arrange
		var mockHandler = new Mock<IOptionsEvaluator>();
		mockHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.Terminal);
		mockHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(false);

		var manager = new FlagEvaluationManager(new HashSet<IOptionsEvaluator> { mockHandler.Object });

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				variations: new Variations { DefaultVariation = "no-handlers" }
			); 
		var context = new EvaluationContext();

		// Act
		var result = await manager.ProcessEvaluation(flagConfig, context);

		// Assert
		result.ShouldBeNull();
		
		// Verify ProcessEvaluation was never called since CanProcess returned false
		mockHandler.Verify(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()), Times.Never);
	}
}

public class FlagEvaluationManager_ProcessEvaluation_MultipleHandlers
{
	[Fact]
	public async Task If_FirstHandlerReturnsDisabled_ThenStopsProcessingAndReturnsDisabled()
	{
		// Arrange
		var firstHandler = new Mock<IOptionsEvaluator>();
		firstHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.TenantRollout);
		firstHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		firstHandler.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ReturnsAsync(new EvaluationResult(isEnabled: false, variation: "blocked", reason: "First handler blocked"));

		var secondHandler = new Mock<IOptionsEvaluator>();
		secondHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.UserRollout);
		secondHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);

		var manager = new FlagEvaluationManager(new HashSet<IOptionsEvaluator> { firstHandler.Object, secondHandler.Object });

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				variations: new Variations { DefaultVariation = "default" }
			); 

		var context = new EvaluationContext();

		// Act
		var result = await manager.ProcessEvaluation(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
		result.Reason.ShouldBe("First handler blocked");

		// Verify second handler was never called
		secondHandler.Verify(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()), Times.Never);
	}

	[Fact]
	public async Task If_AllHandlersReturnEnabled_ThenReturnsEnabledWithDefaultMessage()
	{
		// Arrange
		var firstEvaluator = new Mock<IOptionsEvaluator>();
		firstEvaluator.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.TenantRollout);
		firstEvaluator.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		firstEvaluator.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ReturnsAsync(new EvaluationResult(isEnabled: true, variation: "tenant-ok", reason: "Tenant allowed"));

		var secondEvaluator = new Mock<IOptionsEvaluator>();
		secondEvaluator.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.UserRollout);
		secondEvaluator.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		secondEvaluator.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ReturnsAsync(new EvaluationResult(isEnabled: true, variation: "user-ok", reason: "User allowed"));

		var manager = new FlagEvaluationManager([firstEvaluator.Object, secondEvaluator.Object]);

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				variations: new Variations { DefaultVariation = "all-passed" }
			);

		var context = new EvaluationContext(tenantId: "tenant1", userId: "user1");

		// Act
		var result = await manager.ProcessEvaluation(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeTrue();
		result.Variation.ShouldBe("user-ok");
		result.Reason.ShouldBe($"All configured conditions met for feature flag activation");

		// Verify both handlers were called
		firstEvaluator.Verify(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()), Times.Once);
		secondEvaluator.Verify(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()), Times.Once);
	}

	[Fact]
	public async Task If_HandlersProcessedInCorrectOrder_ThenEvaluatesInEvaluationOrderSequence()
	{
		// Arrange
		var evaluationOrder = new List<EvaluationOrder>();
		
		var terminalHandler = new Mock<IOptionsEvaluator>();
		terminalHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.Terminal);
		terminalHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		terminalHandler.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.Callback(() => evaluationOrder.Add(EvaluationOrder.Terminal))
			.ReturnsAsync(new EvaluationResult(isEnabled: true, variation: "terminal", reason: "Terminal"));

		var tenantHandler = new Mock<IOptionsEvaluator>();
		tenantHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.TenantRollout);
		tenantHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		tenantHandler.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.Callback(() => evaluationOrder.Add(EvaluationOrder.TenantRollout))
			.ReturnsAsync(new EvaluationResult(isEnabled: true, variation: "tenant", reason: "Tenant"));

		var userHandler = new Mock<IOptionsEvaluator>();
		userHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.UserRollout);
		userHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		userHandler.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.Callback(() => evaluationOrder.Add(EvaluationOrder.UserRollout))
			.ReturnsAsync(new EvaluationResult(isEnabled: true, variation: "user", reason: "User"));

		var manager = new FlagEvaluationManager(new HashSet<IOptionsEvaluator> { terminalHandler.Object, tenantHandler.Object, userHandler.Object });

		var identifier = new FlagIdentifier("test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				variations: new Variations { DefaultVariation = "ordered" }
			); 

		var context = new EvaluationContext();

		// Act
		await manager.ProcessEvaluation(flagConfig, context);

		// Assert - Should be processed in ascending order of EvaluationOrder enum values
		evaluationOrder.ShouldBe(new[] 
		{ 
			EvaluationOrder.TenantRollout,    // 1
			EvaluationOrder.UserRollout,      // 2  
			EvaluationOrder.Terminal          // 99
		});
	}
}

public class FlagEvaluationManager_ProcessEvaluation_EdgeCases
{
	[Fact]
	public async Task If_NoHandlers_ThenReturnsNullAsResult()
	{
		// Arrange
		var manager = new FlagEvaluationManager(new HashSet<IOptionsEvaluator>());

		var identifier = new FlagIdentifier("no-evalators-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				variations: new Variations { DefaultVariation = "no-evaluators" }
			); 

		var context = new EvaluationContext();

		// Act
		var result = await manager.ProcessEvaluation(flagConfig, context);

		// Assert
		result.ShouldBeNull();
	}

	[Fact]
	public async Task If_HandlerReturnsNull_ThenContinuesProcessing()
	{
		// Arrange
		var firstHandler = new Mock<IOptionsEvaluator>();
		firstHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.TenantRollout);
		firstHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		firstHandler.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ReturnsAsync((EvaluationResult?)null);

		var secondHandler = new Mock<IOptionsEvaluator>();
		secondHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.UserRollout);
		secondHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		secondHandler.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ReturnsAsync(new EvaluationResult(isEnabled: false, variation: "blocked", reason: "Second handler blocked"));

		var manager = new FlagEvaluationManager([firstHandler.Object, secondHandler.Object]);

		var identifier = new FlagIdentifier("null-result-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				variations: new Variations { DefaultVariation = "default" }
			); 

		var context = new EvaluationContext();

		// Act
		var result = await manager.ProcessEvaluation(flagConfig, context);

		// Assert
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("blocked");
		result.Reason.ShouldBe("Second handler blocked");

		// Verify both handlers were called
		firstHandler.Verify(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()), Times.Once);
		secondHandler.Verify(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()), Times.Once);
	}

	[Fact]
	public async Task If_HandlerThrowsException_ThenExceptionPropagates()
	{
		// Arrange
		var faultyHandler = new Mock<IOptionsEvaluator>();
		faultyHandler.Setup(x => x.EvaluationOrder).Returns(EvaluationOrder.Terminal);
		faultyHandler.Setup(x => x.CanProcess(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>())).Returns(true);
		faultyHandler.Setup(x => x.Evaluate(It.IsAny<EvaluationOptions>(), It.IsAny<EvaluationContext>()))
			.ThrowsAsync(new InvalidOperationException("Handler failed"));

		var manager = new FlagEvaluationManager(new HashSet<IOptionsEvaluator> { faultyHandler.Object });

		var identifier = new FlagIdentifier("faulty-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				variations: new Variations { DefaultVariation = "default" }
			); 

		var context = new EvaluationContext();

		// Act & Assert
		var exception = await Should.ThrowAsync<InvalidOperationException>(() => manager.ProcessEvaluation(flagConfig, context));
		exception.Message.ShouldBe("Handler failed");
	}
}

public class FlagEvaluationManager_ProcessEvaluation_RealWorldScenarios
{
	[Fact]
	public async Task If_ConfiguredLikeServiceCollection_ThenProcessesInCorrectOrder()
	{
		// Arrange - Simulate the actual configuration from ServiceCollectionExtensions
		var handlers = new HashSet<IOptionsEvaluator>
		{
			new ActivationScheduleEvaluator(),
			new OperationalWindowEvaluator(), 
			new TargetingRulesEvaluator(),
			new TenantRolloutEvaluator(),
			new TerminalStateEvaluator(),
			new UserRolloutEvaluator()
		};

		var manager = new FlagEvaluationManager(handlers);

		// Create a flag that will be processed by TerminalStateEvaluator (disabled flag)

		var identifier = new FlagIdentifier("realistic-test-flag", Scope.Global);
		var flagConfig = new EvaluationOptions(
				key: identifier.Key,
				modeSet: EvaluationMode.Off,
				variations: new Variations { DefaultVariation = "disabled-state" }
			);
		
		var context = new EvaluationContext(userId: "test-user", tenantId: "test-tenant");

		// Act
		var result = await manager.ProcessEvaluation(flagConfig, context);

		// Assert - Should be disabled by TerminalStateEvaluator
		result.ShouldNotBeNull();
		result.IsEnabled.ShouldBeFalse();
		result.Variation.ShouldBe("disabled-state");
		result.Reason.ShouldBe("Feature flag 'realistic-test-flag' is explicitly disabled");
	}
}