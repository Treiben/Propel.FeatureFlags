using Knara.UtcStrict;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public record RetentionPolicy(UtcDateTime ExpirationDate)
{
	public static RetentionPolicy OneMonthRetentionPolicy => new(ExpirationDate: DateTimeOffset.UtcNow.AddDays(30));

	public static RetentionPolicy GlobalPolicy => new(UtcDateTime.MaxValue);

	public bool IsPermanent => ExpirationDate == UtcDateTime.MaxValue;

	public bool CanBeDeleted => !IsPermanent && ExpirationDate <= DateTime.UtcNow;
}
