namespace Propel.FeatureFlags.Domain;

public class RetentionPolicy
{
	public DateTime ExpirationDate { get; }
	public bool IsPermanent { get; }
	public Scope Scope { get; set; } = Scope.Application;
	public string? ApplicationName { get; set; } = string.Empty;
	public string? ApplicationVersion { get; set; } = string.Empty;

	public static RetentionPolicy DefaultLifecycle => new(isPermanent: false,expirationDate: DateTime.UtcNow.AddDays(30),  applicationName: "Propel.FeatureFlags");

	public static RetentionPolicy Permanent => new(isPermanent: true, DateTime.MaxValue.ToUniversalTime());

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

		ExpirationDate = NormalizeToUtc(expirationDate, isPermanent);
		IsPermanent = isPermanent;
		Scope = scope;
		ApplicationName = applicationName;
		ApplicationVersion = applicationVersion;
	}

	public static DateTime NormalizeToUtc(DateTime? dateTime, bool isPermanent)
	{
		if (isPermanent)
		{
			return DateTime.MaxValue.ToUniversalTime();
		}

		if (dateTime == null)
		{
			return DateTime.UtcNow.AddDays(30);
		}

		if (dateTime.Value.Kind == DateTimeKind.Utc)
		{
			return dateTime.Value;
		}
		return dateTime.Value.ToUniversalTime();
	}
}

public enum Scope
{
	Global,
	Application,
	Feature
}
