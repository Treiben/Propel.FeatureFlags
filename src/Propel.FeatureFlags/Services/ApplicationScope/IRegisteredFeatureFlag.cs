using Microsoft.Extensions.DependencyInjection;
using Propel.FeatureFlags.Domain;
using Propel.FeatureFlags.Infrastructure;
using Propel.FeatureFlags.Infrastructure.Cache;
using System.Reflection;

namespace Propel.FeatureFlags.Services.ApplicationScope;

public interface IRegisteredFeatureFlag
{
	/// <summary>
	/// Unique identifier for this feature flag across all environments
	/// </summary>
	string Key { get;}
	/// <summary>
	/// Human-readable name displayed in the management UI
	/// </summary>
	string? Name { get; }
	/// <summary>
	/// Detailed description explaining the flag's purpose and impact
	/// If left empty, the description will be auto-assigned to "No description provided"
	/// and will have to be updated later in management tools
	/// </summary>
	string? Description { get; }
	/// <summary>
	/// Metadata tags for categorization and filtering in management tools
	/// </summary>
	Dictionary<string, string>? Tags { get; }
	/// <summary>
	/// Determines the initial state when the flag is auto-created.
	/// EvaluationMode.Enabled = Flag starts enabled when first created
	/// EvaluationMode.Disabled = Flag starts disabled when first created (safer default)
	/// </summary>
	EvaluationMode DefaultMode { get; }
}

public abstract class RegisteredFeatureFlag : IRegisteredFeatureFlag
{
	public string Key { get; }
	public string? Name { get; }
	public string? Description { get; }
	public Dictionary<string, string>? Tags { get; }
	public EvaluationMode DefaultMode { get; }

	public RegisteredFeatureFlag(
			string key,
			string? name = null,
			string? description = null,
			Dictionary<string, string>? tags = null,
			EvaluationMode defaultMode = EvaluationMode.Disabled)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Feature flag key cannot be null or empty.", nameof(key));
		}

		if (defaultMode != EvaluationMode.Enabled && defaultMode != EvaluationMode.Disabled)
		{
			throw new ArgumentException("Default mode must be either Enabled or Disabled.", nameof(defaultMode));
		}

		Key = key.Trim();
		Name = name ?? Key;
		Description = string.IsNullOrWhiteSpace(description) ? "No description provided" : description!.Trim();
		Tags = tags;
		DefaultMode = defaultMode;
	}
}

public static class RegisteredFeatureFlagExtensions
{
	//Ensure feature flags in database
	public static async Task EnsureFeatureFlagsInDatabaseAsync(this IRegisteredFeatureFlag flag, 
		IFlagEvaluationRepository repository, 
		CancellationToken cancellationToken = default)
	{
		var defaultFlag = FeatureFlag.Create(
			key: new FlagKey(flag.Key, Scope.Application, ApplicationInfo.Name, ApplicationInfo.Version),
			name: flag.Name ?? flag.Key,
			description: flag.Description ?? $"Auto-created flag for {flag.Key} in application {ApplicationInfo.Name}");

		if (flag.DefaultMode == EvaluationMode.Enabled)
		{
			defaultFlag.ActiveEvaluationModes.AddMode(EvaluationMode.Enabled);
		}

		try
		{
			// Save to repository (if flag already exists, this will do nothing)
			await repository.CreateAsync(defaultFlag);
		}
		catch (Exception ex)
		{
			throw new Exception("Unable to ensure feature flag from the application. You can use a management tool option to create this flag in the database.");
		}
	}
}
