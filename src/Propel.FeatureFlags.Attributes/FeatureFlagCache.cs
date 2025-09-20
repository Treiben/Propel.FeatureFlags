using Castle.Core.Internal;
using Castle.DynamicProxy;
using Propel.FeatureFlags.Domain;
using System.Collections.Concurrent;
using System.Reflection;

namespace Propel.FeatureFlags.Attributes;

internal static class FeatureFlagCache
{
	// Caching for performance
	private static readonly ConcurrentDictionary<MethodInfo, FeatureFlaggedAttribute?> _attributeCache = new();
	private static readonly ConcurrentDictionary<Type, IFeatureFlag?> _flagInstanceCache = new();

	public static FeatureFlaggedAttribute? GetFeatureFlagAttribute(IInvocation invocation) //(MethodInfo method)
	{
		var concreteMethod = invocation.GetConcreteMethod();
		var attributeMethod = invocation.InvocationTarget.GetType().GetMethod(concreteMethod.Name);
		return _attributeCache.GetOrAdd(attributeMethod, m =>
		{
			var attribute = attributeMethod.GetAttribute<FeatureFlaggedAttribute>() ??
									attributeMethod.GetCustomAttribute<FeatureFlaggedAttribute>() ??
									attributeMethod.DeclaringType?.GetAttribute<FeatureFlaggedAttribute>() ??
									attributeMethod.DeclaringType?.GetCustomAttribute<FeatureFlaggedAttribute>();
			// Check method first, then declaring type
			return attribute;
		});
	}

	public static IFeatureFlag? GetFeatureFlagInstance(Type flagType)
	{
		return _flagInstanceCache.GetOrAdd(flagType, type =>
		{
			try
			{
				// Validate that the type implements IApplicationFeatureFlag
				if (!typeof(IFeatureFlag).IsAssignableFrom(type))
					return null;

				// Look for a static Create method first
				var createMethod = type.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
				if (createMethod != null && typeof(IFeatureFlag).IsAssignableFrom(createMethod.ReturnType))
				{
					return (IFeatureFlag?)createMethod.Invoke(null, null);
				}

				// Fall back to parameterless constructor
				var constructor = type.GetConstructor(Type.EmptyTypes);
				if (constructor != null)
				{
					return (IFeatureFlag?)Activator.CreateInstance(type);
				}

				return null;
			}
			catch
			{
				return null;
			}
		});
	}

	public static object? GetDefaultValue(Type type)
	{
		return type.IsValueType ? Activator.CreateInstance(type) : null;
	}
}
