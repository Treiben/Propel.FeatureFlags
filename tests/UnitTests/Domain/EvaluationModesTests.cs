using Propel.FeatureFlags.Domain;

namespace FeatureFlags.UnitTests.Domain;

public class EvaluationModes_DisabledMutualExclusion
{
	[Fact]
	public void AddMode_DisabledToMultipleModes_ReplacesAllWithDisabled()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On, EvaluationMode.Scheduled]);

		// Act
		modeSet.AddMode(EvaluationMode.Off);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Off });
	}

	[Theory]
	[InlineData(EvaluationMode.On)]
	[InlineData(EvaluationMode.Scheduled)]
	[InlineData(EvaluationMode.TimeWindow)]
	[InlineData(EvaluationMode.UserTargeted)]
	public void AddMode_AnyNonDisabledMode_RemovesDisabled(EvaluationMode mode)
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.Off]);

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
		var modeSet = new EvaluationModes([EvaluationMode.On, EvaluationMode.Scheduled, EvaluationMode.TimeWindow]);

		// Assert
		modeSet.Modes.Count.ShouldBe(3);
		modeSet.Modes.ShouldContain(EvaluationMode.On);
		modeSet.Modes.ShouldContain(EvaluationMode.Scheduled);
		modeSet.Modes.ShouldContain(EvaluationMode.TimeWindow);
	}

	[Fact]
	public void AddMode_ExistingMode_DoesNotDuplicate()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On]);

		// Act
		modeSet.AddMode(EvaluationMode.On);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.On });
	}

	[Fact]
	public void RemoveMode_ExistingMode_RemovesMode()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On, EvaluationMode.Scheduled]);

		// Act
		modeSet.RemoveMode(EvaluationMode.On);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Scheduled });
	}

	[Fact]
	public void RemoveMode_NonExistingMode_NoChange()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On]);

		// Act
		modeSet.RemoveMode(EvaluationMode.Scheduled);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.On });
	}

	[Fact]
	public void RemoveMode_AllModes_DefaultsToDisabled()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On]);

		// Act
		modeSet.RemoveMode(EvaluationMode.On);

		// Assert
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Off });
	}
}

public class EvaluationModes_ContainsModes
{
	[Fact]
	public void ContainsModes_AnyTrue_ReturnsTrueWhenAnyMatch()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On, EvaluationMode.Scheduled]);
		var modesToCheck = new[] { EvaluationMode.On, EvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: true);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsModes_AnyTrue_ReturnsFalseWhenNoneMatch()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On]);
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
		var modeSet = new EvaluationModes([EvaluationMode.On, EvaluationMode.Scheduled]);
		var modesToCheck = new[] { EvaluationMode.On, EvaluationMode.Scheduled };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		result.ShouldBeTrue();
	}

	[Fact]
	public void ContainsModes_AnyFalse_ReturnsFalseWhenNotAllMatch()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On]);
		var modesToCheck = new[] { EvaluationMode.On, EvaluationMode.TimeWindow };

		// Act
		var result = modeSet.ContainsModes(modesToCheck, any: false);

		// Assert
		result.ShouldBeFalse();
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
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Off });
	}
}

public class EvaluationModes_ComplexScenarios
{
	[Fact]
	public void ComplexScenario_DisabledToggling_BehavesCorrectly()
	{
		// Arrange
		var modeSet = new EvaluationModes([]);

		// Add multiple non-disabled modes
		modeSet.AddMode(EvaluationMode.On);
		modeSet.AddMode(EvaluationMode.Scheduled);

		modeSet.Modes.ShouldNotContain(EvaluationMode.Off);

		// Add disabled (should clear all)
		modeSet.AddMode(EvaluationMode.Off);

		modeSet.Modes.ShouldBe(new[] { EvaluationMode.Off });

		// Add non-disabled again
		modeSet.AddMode(EvaluationMode.UserTargeted);
		modeSet.Modes.ShouldBe(new[] { EvaluationMode.UserTargeted });
	}

	[Fact]
	public void ChainedOperations_WorksCorrectly()
	{
		// Arrange
		var modeSet = new EvaluationModes([EvaluationMode.On, EvaluationMode.Scheduled, EvaluationMode.TimeWindow]);

		// Act
		modeSet.RemoveMode(EvaluationMode.Scheduled);

		// Assert
		modeSet.Modes.Count.ShouldBe(2);
		modeSet.Modes.ShouldContain(EvaluationMode.On);
		modeSet.Modes.ShouldContain(EvaluationMode.TimeWindow);
		modeSet.Modes.ShouldNotContain(EvaluationMode.Scheduled);
	}
}