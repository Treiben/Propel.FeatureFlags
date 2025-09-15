using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure;

public class FlagKey
{
	public string Key { get; }
	public string? ApplicationName { get; }
	public string? ApplicationVersion { get; }
	public Scope Scope { get; }

	public FlagKey(string key, Scope scope, string? applicationName = null, string? applicationVersion = null)
	{
		if (string.IsNullOrWhiteSpace(key))
		{
			throw new ArgumentException("Feature flag key cannot be null or empty.", nameof(key));
		}

		if (scope == Scope.Application && string.IsNullOrWhiteSpace(applicationName))
		{
			throw new ArgumentException("Application name must be provided when scope is Application.", nameof(applicationName));
		}

		Key = key.Trim();
		Scope = scope;

		ApplicationName = string.IsNullOrWhiteSpace(applicationName) ? null : applicationName!.Trim();
		ApplicationVersion = string.IsNullOrWhiteSpace(applicationVersion) ? null : applicationVersion!.Trim();
	}
}

public static class FeatureFlagExtensions
{
	public static FlagKey ToFlagKey(this FeatureFlag flag)
	{
		if (flag == null)
			throw new ArgumentNullException(nameof(flag));
		return new FlagKey(
			key: flag.Key,
			scope: flag.Retention.Scope,
			applicationName: flag.Retention.ApplicationName,
			applicationVersion: flag.Retention.ApplicationVersion
		);
	}
}

public class FeatureFlagFilter
{
	public Dictionary<string, string>? Tags { get; set; }
	public EvaluationMode[]? EvaluationModes { get; set; }
	public int? ExpiringInDays { get; set; }
	public string ApplicationName { get; set; } = string.Empty;
	public string? ApplicationVersion { get; set; }
	public Scope? Scope { get; set; }
}

public class PagedResult<T>
{
	public List<T> Items { get; set; } = [];
	public int TotalCount { get; set; }
	public int Page { get; set; }
	public int PageSize { get; set; }
	public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);
	public bool HasNextPage => Page < TotalPages;
	public bool HasPreviousPage => Page > 1;
}

public interface IFeatureFlagRepository
{
	Task<FeatureFlag?> GetAsync(FlagKey key, CancellationToken cancellationToken = default);
	Task<List<FeatureFlag>> GetAllAsync(CancellationToken cancellationToken = default);
	Task<PagedResult<FeatureFlag>> GetPagedAsync(int page, int pageSize, FeatureFlagFilter? filter = null, CancellationToken cancellationToken = default);
	Task<FeatureFlag> CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<FeatureFlag> UpdateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
	Task<bool> DeleteAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
}

public class DuplicatedFeatureFlagException : Exception
{
	public string Key { get; }

	public Scope Scope { get; }

	public string? ApplicationName { get; }

	public string? ApplicationVersion { get; }

	public DuplicatedFeatureFlagException(string key,
		Scope scope,
		string? applicationName = null,
		string? applicationVersion = null) : base("Cannot create a duplicated feature flag.")
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}
}

public class FailedFlagCreationException : Exception
{
	public string Key { get; }

	public Scope Scope { get; }

	public string? ApplicationName { get; }

	public string? ApplicationVersion { get; }

	public FailedFlagCreationException(string message, Exception? innerException, 
		string key, Scope scope, string? applicationName = null, string? applicationVersion = null)
		: base(message, innerException)
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}

	public FailedFlagCreationException(
		string message, 
		string key,
		Scope scope, 
		string? applicationName = null,
		string? applicationVersion = null)
	: base(message)
	{
		Key = key;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}
}
