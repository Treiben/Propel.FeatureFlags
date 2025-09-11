namespace Propel.FeatureFlags.Core;

public class Lifecycle(DateTime? expirationDate, bool isPermanent)
{
	public DateTime? ExpirationDate { get; } = NormalizeToUtc(expirationDate, isPermanent);

	public bool IsPermanent { get; } = isPermanent;

	public static Lifecycle DefaultLifecycle => new(DateTime.UtcNow.AddDays(30), false);

	public static Lifecycle Permanent => new(null, true);

	public bool CanBeDeleted => !IsPermanent && (ExpirationDate == null || ExpirationDate <= DateTime.UtcNow);

	private static DateTime NormalizeToUtc(DateTime? dateTime, bool isPermanent)
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
