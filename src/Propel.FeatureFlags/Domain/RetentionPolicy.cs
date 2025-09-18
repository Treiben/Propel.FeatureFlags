using Propel.FeatureFlags.Helpers;

namespace Propel.FeatureFlags.Domain;

public class RetentionPolicy
{
	public DateTime ExpirationDate { get; }
	public bool IsPermanent { get; }

	public static RetentionPolicy ApplicationDefault => new(isPermanent: false,
		expirationDate: DateTime.UtcNow.AddDays(30));

	public static RetentionPolicy Global => new(isPermanent: true, DateTime.MaxValue.ToUniversalTime());

	public bool CanBeDeleted => !IsPermanent && (ExpirationDate == null || ExpirationDate <= DateTime.UtcNow);

	public RetentionPolicy(bool isPermanent, DateTime expirationDate)
	{
		ExpirationDate = isPermanent ? DateTime.MaxValue.ToUniversalTime() : DateTimeHelpers.NormalizeToUtc(expirationDate, DateTime.UtcNow.AddDays(30));
		IsPermanent = isPermanent;
	}
}
