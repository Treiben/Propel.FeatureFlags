using Castle.DynamicProxy;
using System.Reflection;

namespace Propel.FeatureFlags.Attributes.Interceptors;

internal sealed class SyncInterceptionHandler(IFeatureFlagEvaluator evaluator)
{
	public void Handle(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (invocation.Method.ReturnType == typeof(void))
		{
			// void
			HandleVoid(invocation, flagAttribute);
		}
		else
		{
			// <T>
			var resultType = invocation.Method.ReturnType;
			var method = typeof(SyncInterceptionHandler)
				.GetMethod(nameof(HandleResult), BindingFlags.NonPublic | BindingFlags.Instance)!
				.MakeGenericMethod(resultType);
			invocation.ReturnValue = method.Invoke(this, [invocation, flagAttribute]);
		}
	}

	private void HandleVoid(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		var flag = FeatureFlagCache.GetFeatureFlagInstance(flagAttribute.FlagType);
		if (flag is not null && evaluator.IsEnabledAsync(flag).GetAwaiter().GetResult())
		{
			// Fixed to call the method directly on the target, not through the proxy because that would cause infinite recursion
			// Call the actual target method directly, not through the proxy
			invocation.Method.Invoke(invocation.InvocationTarget, invocation.Arguments);
		}
		else if (!string.IsNullOrWhiteSpace(flagAttribute.FallbackMethod))
		{
			HandleFallback(invocation, flagAttribute);
		}
	}

	private T HandleResult<T>(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		var flag = FeatureFlagCache.GetFeatureFlagInstance(flagAttribute.FlagType);
		if (flag is not null && evaluator.IsEnabledAsync(flag).GetAwaiter().GetResult())
		{
			// Fixed to call the method directly on the target, not through the proxy because that would cause infinite recursion
			// Call the actual target method directly, not through the proxy
			var result = invocation.Method.Invoke(invocation.InvocationTarget, invocation.Arguments);
			return (T)result!;
		}
		else if (!string.IsNullOrWhiteSpace(flagAttribute.FallbackMethod))
		{
			return HandleFallbackResult<T>(invocation, flagAttribute);
		}
		return (T)FeatureFlagCache.GetDefaultValue(typeof(T))!;
	}

	private static void HandleFallback(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (string.IsNullOrWhiteSpace(flagAttribute.FallbackMethod)) return;

		var fallbackMethod = invocation.TargetType!.GetMethod(flagAttribute.FallbackMethod);
		fallbackMethod?.Invoke(invocation.InvocationTarget, invocation.Arguments);
	}

	private static T HandleFallbackResult<T>(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (!string.IsNullOrWhiteSpace(flagAttribute.FallbackMethod))
		{
			var fallbackMethod = invocation.TargetType!.GetMethod(flagAttribute.FallbackMethod);
			if (fallbackMethod != null)
			{
				var result = fallbackMethod.Invoke(invocation.InvocationTarget, invocation.Arguments);
				if (result is T directResult)
				{
					return directResult;
				}
			}
		}

		return (T)FeatureFlagCache.GetDefaultValue(typeof(T))!;
	}
}
