using Propel.FeatureFlags.Domain;

namespace Propel.FeatureFlags.Clients;

public interface IFeatureFlagFactory
{
	IFeatureFlag? GetFlagByKey(string key);
	IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag;
	IEnumerable<IFeatureFlag> GetAllFlags();
}

public class FeatureFlagFactory : IFeatureFlagFactory
{
	private readonly HashSet<IFeatureFlag> _allFlags;

	public FeatureFlagFactory(IEnumerable<IFeatureFlag> allFlags)
	{
		_allFlags = [.. allFlags];
	}

	public IFeatureFlag? GetFlagByKey(string key) => _allFlags.FirstOrDefault(f => f.Key == key);

	public IFeatureFlag? GetFlagByType<T>() where T : IFeatureFlag => _allFlags.OfType<T>().FirstOrDefault();

	public IEnumerable<IFeatureFlag> GetAllFlags() => [.. _allFlags];

	public void AddFlag(IFeatureFlag flag) => _allFlags.Add(flag);
}
