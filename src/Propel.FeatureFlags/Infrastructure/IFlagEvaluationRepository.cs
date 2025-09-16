using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Infrastructure;

public interface IFlagEvaluationRepository
{
	Task<EvaluationCriteria?> GetAsync(FlagKey flagKey, CancellationToken cancellationToken = default);

	Task CreateAsync(FeatureFlag flag, CancellationToken cancellationToken = default);
}
