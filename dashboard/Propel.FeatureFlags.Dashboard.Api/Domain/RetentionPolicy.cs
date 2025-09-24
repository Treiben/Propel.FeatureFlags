using Propel.FeatureFlags.Helpers;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public class RetentionPolicy(bool isPermanent, DateTime expirationDate)
{
	public DateTime ExpirationDate { get; } = 
		isPermanent ? DateTime.MaxValue.ToUniversalTime() 
					: DateTimeHelpers.NormalizeToUtc(expirationDate, DateTime.UtcNow.AddDays(30));
	public bool IsPermanent { get; } = isPermanent;

	public static RetentionPolicy ApplicationDefault => new(isPermanent: false,
		expirationDate: DateTime.UtcNow.AddDays(30));

	public static RetentionPolicy Global => new(isPermanent: true, DateTime.MaxValue.ToUniversalTime());

	public bool CanBeDeleted => !IsPermanent && ExpirationDate <= DateTime.UtcNow;
}
