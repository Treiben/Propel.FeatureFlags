using Propel.FeatureFlags.Core;

namespace FeatureFlags.UnitTests.Core;

public class FlagEvaluationModeSet_DisabledMutualExclusion
{
	[Fact]
	public void Constructor_DefaultsToDisabled()
	{
		// Act
		var modeSet = new FlagEvaluationModeSet();

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Disabled });
	}

	[Fact]
	public void AddMode_NonDisabledToDisabled_ReplacesDisabledWithMode()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Enabled });
	}

	[Fact]
	public void AddMode_DisabledToMultipleModes_ReplacesAllWithDisabled()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		// Act
		modeSet.AddMode(FlagEvaluationMode.Disabled);

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Disabled });
	}

	[Theory]
	[InlineData(FlagEvaluationMode.Enabled)]
	[InlineData(FlagEvaluationMode.Scheduled)]
	[InlineData(FlagEvaluationMode.TimeWindow)]
	[InlineData(FlagEvaluationMode.UserTargeted)]
	public void AddMode_AnyNonDisabledMode_RemovesDisabled(FlagEvaluationMode mode)
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(mode);

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { mode });
	}
}

public class FlagEvaluationModeSet_AddRemoveOperations
{
	[Fact]
	public void AddMode_MultipleModes_KeepsAllNonDisabled()
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
	public void AddMode_ExistingMode_DoesNotDuplicate()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Enabled });
	}

	[Fact]
	public void RemoveMode_ExistingMode_RemovesMode()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Scheduled });
	}

	[Fact]
	public void RemoveMode_NonExistingMode_NoChange()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Scheduled);

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Enabled });
	}

	[Fact]
	public void RemoveMode_AllModes_DefaultsToDisabled()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		// Act
		modeSet.RemoveMode(FlagEvaluationMode.Enabled);

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Disabled });
	}
}

public class FlagEvaluationModeSet_ContainsModes
{
	[Fact]
	public void ContainsModes_AnyTrue_ReturnsTrueWhenAnyMatch()
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
	public void ContainsModes_AnyTrue_ReturnsFalseWhenNoneMatch()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		var modesToCheck = new[] { FlagEvaluationMode.TimeWindow, FlagEvaluationMode.UserTargeted };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: true);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainsModes_AnyFalse_ReturnsTrueWhenAllMatch()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);

		var modesToCheck = new[] { FlagEvaluationMode.Enabled, FlagEvaluationMode.Scheduled };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsModes_AnyFalse_ReturnsFalseWhenNotAllMatch()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		var modesToCheck = new[] { FlagEvaluationMode.Enabled, FlagEvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainsModes_DefaultParameter_DefaultsToAnyTrue()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();
		modeSet.AddMode(FlagEvaluationMode.Enabled);

		var modesToCheck = new[] { FlagEvaluationMode.Enabled, FlagEvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck);

		// Assert
		result.ShouldBeTrue(); // Should default to any=true
	}
}

public class FlagEvaluationModeSet_StaticFactory
{
	[Fact]
	public void FlagIsDisabled_ReturnsDisabledMode()
	{
		// Act
		var modeSet = FlagEvaluationModeSet.FlagIsDisabled;

		// Assert
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Disabled });
	}
}

public class FlagEvaluationModeSet_ComplexScenarios
{
	[Fact]
	public void ComplexScenario_DisabledToggling_BehavesCorrectly()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Start with disabled
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Disabled });

		// Add multiple non-disabled modes
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Disabled);

		// Add disabled (should clear all)
		modeSet.AddMode(FlagEvaluationMode.Disabled);
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.Disabled });

		// Add non-disabled again
		modeSet.AddMode(FlagEvaluationMode.UserTargeted);
		modeSet.EvaluationModes.ShouldBe(new[] { FlagEvaluationMode.UserTargeted });
	}

	[Fact]
	public void ChainedOperations_WorksCorrectly()
	{
		// Arrange
		var modeSet = new FlagEvaluationModeSet();

		// Act
		modeSet.AddMode(FlagEvaluationMode.Enabled);
		modeSet.AddMode(FlagEvaluationMode.Scheduled);
		modeSet.AddMode(FlagEvaluationMode.TimeWindow);
		modeSet.RemoveMode(FlagEvaluationMode.Scheduled);

		// Assert
		modeSet.EvaluationModes.Length.ShouldBe(2);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.Enabled);
		modeSet.EvaluationModes.ShouldContain(FlagEvaluationMode.TimeWindow);
		modeSet.EvaluationModes.ShouldNotContain(FlagEvaluationMode.Scheduled);
	}
}