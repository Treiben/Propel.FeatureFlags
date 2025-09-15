using Propel.FeatureFlags.Helpers;
using Propel.FeatureFlags.Infrastructure;

namespace Propel.FeatureFlags.Domain;

public class RetentionPolicy
{
	public DateTime ExpirationDate { get; }
	public bool IsPermanent { get; }
	public Scope Scope { get; set; } = Scope.Application;
	public string? ApplicationName { get; set; } = string.Empty;
	public string? ApplicationVersion { get; set; } = string.Empty;

	public static RetentionPolicy ApplicationDefault => new(isPermanent: false,
		expirationDate: DateTime.UtcNow.AddDays(30), 
		applicationName: ApplicationInfo.Name,
		applicationVersion: ApplicationInfo.Version);

	public static RetentionPolicy Global => new(isPermanent: true, DateTime.MaxValue.ToUniversalTime(), Scope.Global);

	public bool CanBeDeleted => !IsPermanent && (ExpirationDate == null || ExpirationDate <= DateTime.UtcNow);

	public RetentionPolicy(bool isPermanent,
		DateTime expirationDate,
		Scope scope = Scope.Application, 
		string? applicationName = null, 
		string? applicationVersion = null)
	{
		if (scope == Scope.Global && isPermanent == false)
		{
			throw new ArgumentException("Global lifecycle must be permanent.");
		}

		if (scope == Scope.Application && string.IsNullOrWhiteSpace(applicationName))
		{
			throw new ArgumentException("Application scope requires a valid application name.");
		}

		ExpirationDate = isPermanent ? DateTime.MaxValue.ToUniversalTime() : DateTimeHelpers.NormalizeToUtc(expirationDate, DateTime.UtcNow.AddDays(30));
		IsPermanent = isPermanent;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}
}

public enum Scope
{
	Global,
	Feature,
	Application,
}
