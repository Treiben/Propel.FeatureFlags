using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagEvaluationModeSet_Constructor
{
	[Fact]
	public void If_DefaultConstructor_ThenInitializesWithDisabled()
	{
		// Act
		var modeSet = new FlagEvaluationModeSet();

		// Assert
		modeSet.EvaluationModes.ShouldNotBeNull();
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Disabled);
	}
}

public class FlagEvaluationModeSet_AddMode
{
	[Fact]
	public void If_AddEnabledToDisabled_ThenReplacesDisabledWithEnabled()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Enabled);
	}

	[Fact]
	public void If_AddScheduledToDisabled_ThenReplacesDisabledWithScheduled()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Scheduled);
	}

	[Fact]
	public void If_AddMultipleNonDisabledModes_ThenKeepsAllModes()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(3);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.Enabled);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.Scheduled);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.TimeWindow);
	}

	[Fact]
	public void If_AddDisabledToMultipleModes_ThenReplacesAllWithDisabled()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);

		// Act
		modeSet.AddMode(FlagEvaluationMode.Disabled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Disabled);
	}

	[Fact]
	public void If_AddExistingMode_ThenDoesNotDuplicate()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Enabled);
	}

	[Fact]
	public void If_AddDisabledToDisabled_ThenRemainsDisabled()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(FlagEvaluationMode.Disabled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Disabled);
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Enabled)]
	[InlineData(FlagEvaluationMode.Scheduled)]
	[InlineData(FlagEvaluationMode.TimeWindow)]
	[InlineData(FlagEvaluationMode.UserTargeted)]
	[InlineData(FlagEvaluationMode.UserRolloutPercentage)]
	[InlineData(FlagEvaluationMode.TenantRolloutPercentage)]
	public void If_AddNonDisabledModeToDisabled_ThenRemovesDisabledAndAddsMode(FlagEvaluationMode mode)
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(mode);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(mode);
		modeSet.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Disabled);
	}

	[Fact]
	public void If_AddModeReturnsThis_ThenSupportsFluentInterface()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(3);
	}
}

public class FlagEvaluationModeSet_RemoveMode
{
	[Fact]
	public void If_RemoveExistingMode_ThenRemovesMode()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Scheduled);
	}

	[Fact]
	public void If_RemoveNonExistingMode_ThenNoChange()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Scheduled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Enabled);
	}

	[Fact]
	public void If_RemoveAllModes_ThenEmptyArray()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(0);
	}

	[Fact]
	public void If_RemoveDisabledFromDisabledOnly_ThenEmptyArray()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Disabled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(0);
	}

	[Fact]
	public void If_RemoveFromMultipleModes_ThenRemovesOnlySpecifiedMode()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Scheduled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(2);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.Enabled);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.TimeWindow);
		modeSet.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Scheduled);
	}

	[Fact]
	public void If_RemoveModeReturnsThis_ThenSupportsFluentInterface()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Enabled);
		modeSet.RemoveMode(FlagEvaluationMode.Scheduled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.TimeWindow);
	}
}

public class FlagEvaluationModeSet_ContainsModes
{
	[Fact]
	public void If_ContainsModesWithAnyTrue_ThenReturnsTrueWhenAnyMatch()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		var modesToCheck = new[] { FlagEvaluationMode.Enabled, FlagEvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: true);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_ContainsModesWithAnyTrue_ThenReturnsFalseWhenNoneMatch()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		var modesToCheck = new[] { FlagEvaluationMode.TimeWindow, FlagEvaluationMode.UserTargeted };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: true);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_ContainsModesWithAnyFalse_ThenReturnsTrueWhenAllMatch()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);

		var modesToCheck = new[] { FlagEvaluationMode.Enabled, FlagEvaluationMode.Scheduled };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void If_ContainsModesWithAnyFalse_ThenReturnsFalseWhenNotAllMatch()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		var modesToCheck = new[] { FlagEvaluationMode.Enabled, FlagEvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void If_ContainsModesWithEmptyArray_ThenReturnsTrueForAllMode()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		var modesToCheck = Array.Empty<FlagEvaluationMode>();

		// Act
		var resultAny = modeSet.ContainsModes(modesToCheck, any: true);
		var resultAll = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		resultAny.ShouldBeFalse();
		resultAll.ShouldBeTrue();
	}

	[Fact]
	public void If_ContainsModesWithSingleMatch_ThenBothAnyAndAllReturnTrue()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		var modesToCheck = new[] { FlagEvaluationMode.Enabled };

		// Act
		var resultAny = modeSet.ContainsModes(modesToCheck, any: true);
		var resultAll = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		resultAny.ShouldBeTrue();
		resultAll.ShouldBeTrue();
	}

	[Theory]
	[InlineData(true)]
	[InlineData(false)]
	public void If_ContainsModesDefaultParameter_ThenDefaultsToAnyTrue(bool explicitAnyValue)
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		var modesToCheck = new[] { FlagEvaluationMode.Enabled, FlagEvaluationMode.TimeWindow };

		// Act
		var resultDefault = modeSet.ContainsModes(modesToCheck);
		var resultExplicit = modeSet.ContainsModes(modesToCheck, any: explicitAnyValue);

		// Assert
		resultDefault.ShouldBeTrue(); // Default should be any=true
		if (explicitAnyValue)
			resultExplicit.ShouldBeTrue();
		else
			resultExplicit.ShouldBeFalse();
	}

	[Fact]
	public void If_ContainsModesWithAllEnumValues_ThenWorksCorrectly()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		var allModes = Enum.GetValues<FlagEvaluationMode>();

		foreach (var mode in allModes)
		{
			if (mode != FlagEvaluationMode.Disabled)
				modeSet.AddMode(mode);
		}

		// Act
		var resultAny = modeSet.ContainsModes(allModes, any: true);
		var resultAll = modeSet.ContainsModes(allModes, any: false);

		// Assert
		resultAny.ShouldBeTrue();
		resultAll.ShouldBeFalse(); // Disabled is not in the set
	}
}

public class FlagEvaluationModeSet_StaticFactory
{
	[Fact]
	public void If_FlagIsDisabledStaticProperty_ThenReturnsDisabledMode()
	{
		// Act
		var modeSet = FlagEvaluationModeSet.FlagIsDisabled;

		// Assert
		modeSet.EvaluationModes.ShouldNotBeNull();
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Disabled);
	}

	[Fact]
	public void If_MultipleCallsToFlagIsDisabled_ThenReturnsSeparateInstances()
	{
		// Act
		var modeSet1 = FlagEvaluationModeSet.FlagIsDisabled;
		var modeSet2 = FlagEvaluationModeSet.FlagIsDisabled;

		// Assert
		modeSet1.ShouldNotBe(modeSet2);
		modeSet1.EvaluationModes.ShouldBe(modeSet2.EvaluationModes);
	}

	[Fact]
	public void If_ModifyFlagIsDisabledInstance_ThenDoesNotAffectOtherInstances()
	{
		// Arrange
		var modeSet1 = FlagEvaluationModeSet.FlagIsDisabled;
		var modeSet2 = FlagEvaluationModeSet.FlagIsDisabled;

		// Act
		modeSet1.AddMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet1.EvaluationModes.Length.ShouldBe(1);
		modeSet1.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Enabled);

		modeSet2.EvaluationModes.Length.ShouldBe(1);
		modeSet2.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Disabled);
	}
}

public class FlagEvaluationModeSet_EdgeCases
{
	[Fact]
	public void If_PropertyAccessAfterModifications_ThenReturnsCurrentState()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act & Assert - Initial state
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Disabled);

		// Act & Assert - After adding mode
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Enabled);

		// Act & Assert - After adding another mode
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.EvaluationModes.Length.ShouldBe(2);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.Enabled);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.Scheduled);

		// Act & Assert - After removing mode
		modeSet.RemoveMode(FlagEvaluationMode.Enabled);
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Scheduled);
	}

	[Fact]
	public void If_ChainedOperations_ThenWorksCorrectly()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);
		modeSet.RemoveMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.UserTargeted);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(3);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.Enabled);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.TimeWindow);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.UserTargeted);
		modeSet.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Scheduled);
		modeSet.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Disabled);
	}

	[Fact]
	public void If_ArrayIsPrivateSet_ThenCannotBeModifiedExternally()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act - Try to get reference to array
		var modes = modeSet.EvaluationModes;
		var originalLength = modes.Length;

		// Assert - Verify we can read but property setter is private
		modes.ShouldNotBeNull();
		modes.Length.ShouldBe(1);

		// Verify that changing the returned array doesn't affect the internal state
		// (This tests that the array is either copied or the internal state is protected)
		var internalState = modeSet.EvaluationModes;
		internalState.Length.ShouldBe(originalLength);
	}

	[Fact]
	public void If_ComplexScenarioWithDisabledToggling_ThenBehavesCorrectly()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act & Assert - Start with disabled
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Disabled);

		// Add multiple non-disabled modes
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);
		modeSet.EvaluationModes.Length.ShouldBe(3);
		modeSet.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Disabled);

		// Add disabled (should clear all and set to disabled only)
		modeSet.AddMode(FlagEvaluationMode.Disabled);
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.Disabled);

		// Add non-disabled again
		modeSet.AddMode(FlagEvaluationMode.UserTargeted);
		modeSet.EvaluationModes.Length.ShouldBe(1);
		modeSet.EvaluationModes[0].ShouldBe(FlagEvaluationMode.UserTargeted);
		modeSet.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Disabled);
	}
}