using Propel.FeatureFlags.Domain;

namespace FeatureFlags.UnitTests.Domain;

public class EvaluationModes_DisabledMutualExclusion
{
	[Fact]
	public void Constructor_DefaultsToDisabled()
	{
		// Act
		var modeSet = new EvaluationModes();

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Disabled });
	}

	[Fact]
	public void AddMode_NonDisabledToDisabled_ReplacesDisabledWithMode()
	{
		// Arrange
		var modeSet = new EvaluationModes();

		// Act
		modeSet.AddMode(EvaluationMode.Enabled);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Enabled });
	}

	[Fact]
	public void AddMode_DisabledToMultipleModes_ReplacesAllWithDisabled()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);
		modeSet.AddMode(EvaluationMode.Scheduled);

		// Act
		modeSet.AddMode(EvaluationMode.Disabled);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Disabled });
	}

	[Theory]
	[InlineData(EvaluationMode.Enabled)]
	[InlineData(EvaluationMode.Scheduled)]
	[InlineData(EvaluationMode.TimeWindow)]
	[InlineData(EvaluationMode.UserTargeted)]
	public void AddMode_AnyNonDisabledMode_RemovesDisabled(EvaluationMode mode)
	{
		// Arrange
		var modeSet = new EvaluationModes();

		// Act
		modeSet.AddMode(mode);

		// Assert
		modeSet.Modes.ShouldBe(new[] { mode });
	}
}

public class EvaluationModes_AddRemoveOperations
{
	[Fact]
	public void AddMode_MultipleModes_KeepsAllNonDisabled()
	{
		// Arrange
		var modeSet = new EvaluationModes();

		// Act
		modeSet.AddMode(EvaluationMode.Enabled);
		modeSet.AddMode(EvaluationMode.Scheduled);
		modeSet.AddMode(EvaluationMode.TimeWindow);

		// Assert
		modeSet.Modes.Count.ShouldBe(3);
		modeSet.Modes.ShouldContain(EvaluationMode.Enabled);
		modeSet.Modes.ShouldContain(EvaluationMode.Scheduled);
		modeSet.Modes.ShouldContain(EvaluationMode.TimeWindow);
	}

	[Fact]
	public void AddMode_ExistingMode_DoesNotDuplicate()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);

		// Act
		modeSet.AddMode(EvaluationMode.Enabled);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Enabled });
	}

	[Fact]
	public void RemoveMode_ExistingMode_RemovesMode()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);
		modeSet.AddMode(EvaluationMode.Scheduled);

		// Act
		modeSet.RemoveMode(EvaluationMode.Enabled);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Scheduled });
	}

	[Fact]
	public void RemoveMode_NonExistingMode_NoChange()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);

		// Act
		modeSet.RemoveMode(EvaluationMode.Scheduled);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Enabled });
	}

	[Fact]
	public void RemoveMode_AllModes_DefaultsToDisabled()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);

		// Act
		modeSet.RemoveMode(EvaluationMode.Enabled);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Disabled });
	}
}

public class EvaluationModes_ContainsModes
{
	[Fact]
	public void ContainsModes_AnyTrue_ReturnsTrueWhenAnyMatch()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);
		modeSet.AddMode(EvaluationMode.Scheduled);

		var modesToCheck = new[] { EvaluationMode.Enabled, EvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: true);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsModes_AnyTrue_ReturnsFalseWhenNoneMatch()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);

		var modesToCheck = new[] { EvaluationMode.TimeWindow, EvaluationMode.UserTargeted };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: true);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainsModes_AnyFalse_ReturnsTrueWhenAllMatch()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);
		modeSet.AddMode(EvaluationMode.Scheduled);

		var modesToCheck = new[] { EvaluationMode.Enabled, EvaluationMode.Scheduled };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsModes_AnyFalse_ReturnsFalseWhenNotAllMatch()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);

		var modesToCheck = new[] { EvaluationMode.Enabled, EvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		result.ShouldBeFalse();
	}

	[Fact]
	public void ContainsModes_DefaultParameter_DefaultsToAnyTrue()
	{
		// Arrange
		var modeSet = new EvaluationModes();
		modeSet.AddMode(EvaluationMode.Enabled);

		var modesToCheck = new[] { EvaluationMode.Enabled, EvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck);

		// Assert
		result.ShouldBeTrue(); // Should default to any=true
	}
}

public class EvaluationModes_StaticFactory
{
	[Fact]
	public void FlagIsDisabled_ReturnsDisabledMode()
	{
		// Act
		var modeSet = EvaluationModes.FlagIsDisabled;

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Disabled });
	}
}

public class EvaluationModes_ComplexScenarios
{
	[Fact]
	public void ComplexScenario_DisabledToggling_BehavesCorrectly()
	{
		// Arrange
		var modeSet = new EvaluationModes();

		// Start with disabled
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Disabled });

		// Add multiple non-disabled modes
		modeSet.AddMode(EvaluationMode.Enabled);
		modeSet.AddMode(EvaluationMode.Scheduled);
		modeSet.Modes.ShouldNotContain(EvaluationMode.Disabled);

		// Add disabled (should clear all)
		modeSet.AddMode(EvaluationMode.Disabled);
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Disabled });

		// Add non-disabled again
		modeSet.AddMode(EvaluationMode.UserTargeted);
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.UserTargeted });
	}

	[Fact]
	public void ChainedOperations_WorksCorrectly()
	{
		// Arrange
		var modeSet = new EvaluationModes();

		// Act
		modeSet.AddMode(EvaluationMode.Enabled);
		modeSet.AddMode(EvaluationMode.Scheduled);
		modeSet.AddMode(EvaluationMode.TimeWindow);
		modeSet.RemoveMode(EvaluationMode.Scheduled);

		// Assert
		modeSet.Modes.Count.ShouldBe(2);
		modeSet.Modes.ShouldContain(EvaluationMode.Enabled);
		modeSet.Modes.ShouldContain(EvaluationMode.TimeWindow);
		modeSet.Modes.ShouldNotContain(EvaluationMode.Scheduled);
	}
}