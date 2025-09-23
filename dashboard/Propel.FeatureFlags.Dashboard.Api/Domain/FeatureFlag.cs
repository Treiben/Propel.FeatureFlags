using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Dashboard.Api.Domain;

public record FeatureFlag(
	FlagIdentifier Identifier,
	Metadata Metadata,
	FlagEvaluationConfiguration Configuration);
