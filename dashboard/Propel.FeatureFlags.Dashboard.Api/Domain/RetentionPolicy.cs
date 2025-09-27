using Knara.UtcStrict;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public class RetentionPolicy
{
	public DateTime ExpirationDate { get; }
	public bool IsPermanent { get; }

	public RetentionPolicy(UtcDateTime expirationDate)
	{
		ExpirationDate = expirationDate;
		IsPermanent = expirationDate == UtcDateTime.MaxValue;
	}

	public static RetentionPolicy OneMonthRetentionPolicy => new(expirationDate: DateTimeOffset.UtcNow.AddDays(30));

	public static RetentionPolicy GlobalPolicy => new(UtcDateTime.MaxValue);

	public bool CanBeDeleted => !IsPermanent && ExpirationDate <= DateTime.UtcNow;
}
