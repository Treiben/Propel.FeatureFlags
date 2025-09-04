using Castle.Core.Internal;
using Castle.DynamicProxy;
using Microsoft.AspNetCore.Http;
using Propel.FeatureFlags.AspNetCore.Extensions;
using System.Reflection;

namespace Propel.FeatureFlags.Attributes
{
	public sealed class HttpFeatureFlagInterceptor : IInterceptor
	{
		private readonly IHttpContextAccessor _httpContextAccessor;

		public HttpFeatureFlagInterceptor(IHttpContextAccessor httpContextAccessor)
		{
			_httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
		}

		public void Intercept(IInvocation invocation)
		{
			var method = invocation.GetConcreteMethod();

			method = invocation.InvocationTarget.GetType().
			   GetMethod(method.Name);

			var flagAttribute = method.GetAttribute<FeatureFlaggedAttribute>() ??
							method.GetCustomAttribute<FeatureFlaggedAttribute>() ??
							method.DeclaringType?.GetAttribute<FeatureFlaggedAttribute>() ??
							method.DeclaringType?.GetCustomAttribute<FeatureFlaggedAttribute>();

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

		private void InterceptAsync(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
		{
			if (invocation.Method.ReturnType.IsGenericType)
			{
				var resultType = invocation.Method.ReturnType.GetGenericArguments()[0];
				var method = typeof(HttpFeatureFlagInterceptor).GetMethod(nameof(InterceptAsyncGeneric), BindingFlags.NonPublic | BindingFlags.Instance)!
					.MakeGenericMethod(resultType);
				invocation.ReturnValue = method.Invoke(this, new object[] { invocation, flagAttribute });
			}
			else
			{
				invocation.ReturnValue = InterceptAsyncVoid(invocation, flagAttribute);
			}
		}

		private void Intercept(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
		{
			if (IsEnabled(flagAttribute.FlagKey))
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

		private async Task InterceptAsyncVoid(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
		{
			if (IsEnabled(flagAttribute.FlagKey))
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

		private async Task<T> InterceptAsyncGeneric<T>(IInvocation invocation, FeatureFlaggedAttribute flagAttribute)
		{
			if (IsEnabled(flagAttribute.FlagKey))
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

		private bool IsEnabled(string flagKey)
		{
			var context = _httpContextAccessor.HttpContext!;
			return context.FeatureFlags()?.IsEnabledAsync(flagKey).Result ?? false;
		}

		private static object? GetDefaultValue(Type type)
		{
			return type.IsValueType ? Activator.CreateInstance(type) : null;
		}
	}
}
