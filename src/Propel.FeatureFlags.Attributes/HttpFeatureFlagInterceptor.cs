using Castle.DynamicProxy;
using Microsoft.AspNetCore.Http;
using Propel.FeatureFlags.AspNetCore.Extensions;
using Propel.FeatureFlags.Domain;
using System.Collections.Concurrent;
using System.Reflection;

namespace Propel.FeatureFlags.Attributes;

public sealed class HttpFeatureFlagInterceptor(IHttpContextAccessor httpContextAccessor) : IInterceptor
{
	private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));

	// Caching for performance
	private static readonly ConcurrentDictionary<MethodInfo, FeatureFlaggedAttribute?> _attributeCache = new();
	private static readonly ConcurrentDictionary<Type, IFeatureFlag?> _flagInstanceCache = new();

	public void Intercept(IInvocation invocation)
	{
		var flagAttribute = GetFeatureFlagAttribute(invocation.Method);

		if (flagAttribute == null)
		{
			invocation.Proceed();
			return;
		}

		// Check if method is async
		if (IsAsyncMethod(invocation.Method))
		{
			HandleAsyncInterception(invocation, flagAttribute);
		}
		else
		{
			HandleSyncInterception(invocation, flagAttribute);
		}
	}

	private static bool IsAsyncMethod(MethodInfo method)
	{
		return method.ReturnType == typeof(Task) ||
			   (method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>));
	}

	private void HandleSyncInterception(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		var featureFlag = GetFeatureFlagInstance(flagAttribute.FlagType);
		if (featureFlag == null)
		{
			invocation.Proceed();
			return;
		}

		// Use async method but block on result for sync methods
		var isEnabled = IsEnabledAsync(featureFlag).GetAwaiter().GetResult();

		if (isEnabled)
		{
			invocation.Proceed();
		}
		else
		{
			HandleFallback(invocation, flagAttribute);
		}
	}

	private void HandleAsyncInterception(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (invocation.Method.ReturnType.IsGenericType)
		{
			// Task<T>
			var resultType = invocation.Method.ReturnType.GetGenericArguments()[0];
			var method = typeof(HttpFeatureFlagInterceptor)
				.GetMethod(nameof(HandleAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
				.MakeGenericMethod(resultType);
			invocation.ReturnValue = method.Invoke(this, [invocation, flagAttribute]);
		}
		else
		{
			// Task (void)
			invocation.ReturnValue = HandleAsyncVoid(invocation, flagAttribute);
		}
	}

	private async Task HandleAsyncVoid(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		var featureFlag = GetFeatureFlagInstance(flagAttribute.FlagType);
		if (featureFlag == null)
		{
			invocation.Proceed();
			if (invocation.ReturnValue is Task task)
				await task;
			return;
		}

		if (await IsEnabledAsync(featureFlag))
		{
			invocation.Proceed();
			if (invocation.ReturnValue is Task task)
				await task;
		}
		else
		{
			await HandleAsyncFallback(invocation, flagAttribute);
		}
	}

	private async Task<T> HandleAsyncGeneric<T>(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		var featureFlag = GetFeatureFlagInstance(flagAttribute.FlagType);
		if (featureFlag == null)
		{
			invocation.Proceed();
			if (invocation.ReturnValue is Task<T> task)
				return await task;
			return (T)invocation.ReturnValue!;
		}

		if (await IsEnabledAsync(featureFlag))
		{
			invocation.Proceed();
			if (invocation.ReturnValue is Task<T> task)
				return await task;
			return (T)invocation.ReturnValue!;
		}
		else
		{
			return await HandleAsyncFallbackGeneric<T>(invocation, flagAttribute);
		}
	}

	private void HandleFallback(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (!string.IsNullOrEmpty(flagAttribute.FallbackMethod))
		{
			var fallbackMethod = invocation.TargetType.GetMethod(flagAttribute.FallbackMethod);
			if (fallbackMethod != null)
			{
				invocation.ReturnValue = fallbackMethod.Invoke(invocation.InvocationTarget, invocation.Arguments);
				return;
			}
		}

		invocation.ReturnValue = GetDefaultValue(invocation.Method.ReturnType);
	}

	private async Task HandleAsyncFallback(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (!string.IsNullOrEmpty(flagAttribute.FallbackMethod))
		{
			var fallbackMethod = invocation.TargetType.GetMethod(flagAttribute.FallbackMethod);
			if (fallbackMethod != null)
			{
				var result = fallbackMethod.Invoke(invocation.InvocationTarget, invocation.Arguments);
				if (result is Task task)
					await task;
				return;
			}
		}
		// For async void methods with no fallback, we just don't execute anything
	}

	private async Task<T> HandleAsyncFallbackGeneric<T>(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
	{
		if (!string.IsNullOrEmpty(flagAttribute.FallbackMethod))
		{
			var fallbackMethod = invocation.TargetType.GetMethod(flagAttribute.FallbackMethod);
			if (fallbackMethod != null)
			{
				var result = fallbackMethod.Invoke(invocation.InvocationTarget, invocation.Arguments);
				if (result is Task<T> task)
					return await task;
				if (result is T directResult)
					return directResult;
			}
		}

		return (T)GetDefaultValue(typeof(T))!;
	}

	private FeatureFlaggedAttribute? GetFeatureFlagAttribute(MethodInfo method)
	{
		return _attributeCache.GetOrAdd(method, m =>
		{
			// Check method first, then declaring type
			return m.GetCustomAttribute<FeatureFlaggedAttribute>() ??
				   m.DeclaringType?.GetCustomAttribute<FeatureFlaggedAttribute>();
		});
	}

	private static IFeatureFlag? GetFeatureFlagInstance(Type flagType)
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

	private async Task<bool> IsEnabledAsync(IFeatureFlag flag)
	{
		var context = _httpContextAccessor.HttpContext;
		if (context == null)
			return false;

		var evaluator = context.FeatureFlags();
		if (evaluator == null)
			return false;

		return await evaluator.IsEnabledAsync(flag);
	}

	private static object? GetDefaultValue(Type type)
	{
		return type.IsValueType ? Activator.CreateInstance(type) : null;
	}
}