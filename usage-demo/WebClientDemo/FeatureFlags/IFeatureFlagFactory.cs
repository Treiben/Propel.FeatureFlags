using Propel.FeatureFlags.Domain;

namespace ApiFlagUsageDemo.FeatureFlags;

public interface IFeatureFlagFactory
{
	IFeatureFlag? GetFlagByKey(string key);
	IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag;
	IEnumerable<IFeatureFlag> GetAllFlags();
}

public class DemoFeatureFlagFactory : IFeatureFlagFactory
{
	private readonly HashSet<IFeatureFlag> _allFlags;

	public DemoFeatureFlagFactory(IEnumerable<IFeatureFlag> allFlags)
	{
		_allFlags = allFlags.ToHashSet();
	}

	public IFeatureFlag? GetFlagByKey(string key) => _allFlags.FirstOrDefault(f => f.Key == key);

	public IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag => _allFlags.OfType<T>().FirstOrDefault();

	public IEnumerable<IFeatureFlag> GetAllFlags() => [.. _allFlags];

	public void AddFlag(IFeatureFlag flag) => _allFlags.Add(flag);
}
