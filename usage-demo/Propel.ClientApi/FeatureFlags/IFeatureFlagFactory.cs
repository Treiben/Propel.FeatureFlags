using Propel.FeatureFlags.Services.ApplicationScope;

namespace Propel.ClientApi.FeatureFlags;

public interface IFeatureFlagFactory
{
	IRegisteredFeatureFlag? GetFlagByKey(string key);
	IRegisteredFeatureFlag? GetFlagByType<T>() where T : IRegisteredFeatureFlag;
	IEnumerable<IRegisteredFeatureFlag> GetAllFlags();
}

public class DemoFeatureFlagFactory : IFeatureFlagFactory
{
	private readonly HashSet<IRegisteredFeatureFlag> _allFlags;

	public DemoFeatureFlagFactory(IEnumerable<IRegisteredFeatureFlag> allFlags)
	{
		_allFlags = allFlags.ToHashSet();
	}

	public IRegisteredFeatureFlag? GetFlagByKey(string key) => _allFlags.FirstOrDefault(f => f.Key == key);

	public IRegisteredFeatureFlag? GetFlagByType<T>() where T : IRegisteredFeatureFlag => _allFlags.OfType<T>().FirstOrDefault();

	public IEnumerable<IRegisteredFeatureFlag> GetAllFlags() => [.. _allFlags];

	public void AddFlag(IRegisteredFeatureFlag flag) => _allFlags.Add(flag);
}
