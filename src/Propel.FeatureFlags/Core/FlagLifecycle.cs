namespace Propel.FeatureFlags.Core;

public class FlagLifecycle(DateTime? expirationDate, bool isPermanent)
{
	public DateTime? ExpirationDate { get; } = NormalizeToUtc(expirationDate, isPermanent);

	public bool IsPermanent { get; } = isPermanent;

	public static FlagLifecycle DefaultLifecycle => new(DateTime.UtcNow.AddDays(30), false);

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
