using Castle.Core.Internal;
using Castle.DynamicProxy;
using Propel.FeatureFlags.Evaluation.ApplicationScope;
using System.Reflection;

namespace Propel.FeatureFlags.Attributes
{
	public class FeatureFlagV2Interceptor : IInterceptor
	{
		private readonly IFeatureFlagClient _featureFlags;

		public FeatureFlagV2Interceptor(IFeatureFlagClient featureFlags)
		{
			_featureFlags = featureFlags;
		}

		public void Intercept(IInvocation invocation)
		{
			var method = invocation.GetConcreteMethod();

			method = invocation.InvocationTarget.GetType().
			   GetMethod(method.Name);

			var flagAttribute = method.GetAttribute<FeatureFlaggedV2Attribute>() ??
							method.GetCustomAttribute<FeatureFlaggedV2Attribute>() ??
							method.DeclaringType?.GetAttribute<FeatureFlaggedV2Attribute>() ??
							method.DeclaringType?.GetCustomAttribute<FeatureFlaggedV2Attribute>();

			if (flagAttribute == null)
			{
				invocation.Proceed();
				return;
			}

			// Handle async methods
			if (invocation.Method.ReturnType == typeof(Task) || 
			    invocation.Method.ReturnType.IsGenericType && invocation.Method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>))
			{
				InterceptAsync(invocation, flagAttribute);
			}
			else
			{
				Intercept(invocation, flagAttribute);
			}
		}

		private void InterceptAsync(IInvocation invocation, FeatureFlaggedV2Attribute flagAttribute)
		{
			if (invocation.Method.ReturnType.IsGenericType)
			{
				var resultType = invocation.Method.ReturnType.GetGenericArguments()[0];
				var method = typeof(FeatureFlagV2Interceptor).GetMethod(nameof(InterceptAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
					.MakeGenericMethod(resultType);
				invocation.ReturnValue = method.Invoke(this, new object[] { invocation, flagAttribute });
			}
			else
			{
				invocation.ReturnValue = InterceptAsyncVoid(invocation, flagAttribute);
			}
		}

		private void Intercept(IInvocation invocation, FeatureFlaggedV2Attribute flagAttribute)
		{
			var featureFlag = GetFeatureFlagInstance(flagAttribute.FlagType);
			if (featureFlag == null)
			{
				invocation.Proceed();
				return;
			}

			var isEnabled = _featureFlags.IsEnabledAsync(flag: featureFlag).Result;

			if (isEnabled)
			{
				invocation.Proceed();
			}
			else if (!string.IsNullOrEmpty(flagAttribute.FallbackMethod))
			{
				// Call fallback method
				var fallbackMethod = invocation.TargetType.GetMethod(flagAttribute.FallbackMethod);
				invocation.ReturnValue = fallbackMethod?.Invoke(invocation.InvocationTarget, invocation.Arguments);
			}
			else
			{
				// Return default value or throw exception
				invocation.ReturnValue = GetDefaultValue(invocation.Method.ReturnType);
			}
		}

		private async Task InterceptAsyncVoid(IInvocation invocation, FeatureFlaggedV2Attribute flagAttribute)
		{
			var featureFlag = GetFeatureFlagInstance(flagAttribute.FlagType);
			if (featureFlag == null)
			{
				var result = invocation.Method.Invoke(invocation.InvocationTarget, invocation.Arguments);
				if (result is Task task)
				{
					await task;
				}
				return;
			}

			var isEnabled = await _featureFlags.IsEnabledAsync(flag: featureFlag);

			if (isEnabled)
			{
				var result = invocation.Method.Invoke(invocation.InvocationTarget, invocation.Arguments);
				if (result is Task task)
				{
					await task;
				}
			}
			else if (!string.IsNullOrEmpty(flagAttribute.FallbackMethod))
			{
				var fallbackMethod = invocation.TargetType.GetMethod(flagAttribute.FallbackMethod);
				if (fallbackMethod != null)
				{
					var result = fallbackMethod.Invoke(invocation.InvocationTarget, invocation.Arguments);
					if (result is Task task)
					{
						await task;
					}
				}
			}
		}

		private async Task<T> InterceptAsyncGeneric<T>(IInvocation invocation, FeatureFlaggedV2Attribute flagAttribute)
		{
			var featureFlag = GetFeatureFlagInstance(flagAttribute.FlagType);
			if (featureFlag == null)
			{
				var result = invocation.Method.Invoke(invocation.InvocationTarget, invocation.Arguments);
				if (result is Task<T> task)
				{
					return await task;
				}
				return (T)result!;
			}

			var isEnabled = await _featureFlags.IsEnabledAsync(flag: featureFlag);

			if (isEnabled)
			{
				// Call the actual target method directly, not through the proxy
				var result = invocation.Method.Invoke(invocation.InvocationTarget, invocation.Arguments);
				if (result is Task<T> task)
				{
					return await task;
				}
				return (T)result!;
			}
			else if (!string.IsNullOrEmpty(flagAttribute.FallbackMethod))
			{
				var fallbackMethod = invocation.TargetType.GetMethod(flagAttribute.FallbackMethod);
				if (fallbackMethod != null)
				{
					var result = fallbackMethod.Invoke(invocation.InvocationTarget, invocation.Arguments);
					if (result is Task<T> task)
					{
						return await task;
					}
				}
			}

			return (T)GetDefaultValue(typeof(T))!;
		}

		private static IApplicationFeatureFlag? GetFeatureFlagInstance(Type flagType)
		{
			try
			{
				// Validate that the type implements IApplicationFeatureFlag
				if (!typeof(IApplicationFeatureFlag).IsAssignableFrom(flagType))
				{
					return null;
				}

				// Look for a static Create method first
				var createMethod = flagType.GetMethod("Create", BindingFlags.Public | BindingFlags.Static);
				if (createMethod != null && typeof(IApplicationFeatureFlag).IsAssignableFrom(createMethod.ReturnType))
				{
					return (IApplicationFeatureFlag?)createMethod.Invoke(null, null);
				}

				// Fall back to parameterless constructor
				var constructor = flagType.GetConstructor(Type.EmptyTypes);
				if (constructor != null)
				{
					return (IApplicationFeatureFlag?)Activator.CreateInstance(flagType);
				}

				return null;
			}
			catch
			{
				// If we can't create the feature flag instance, return null
				// This will cause the method to proceed normally
				return null;
			}
		}

		private static object? GetDefaultValue(Type type)
		{
			return type.IsValueType ? Activator.CreateInstance(type) : null;
		}
	}
}