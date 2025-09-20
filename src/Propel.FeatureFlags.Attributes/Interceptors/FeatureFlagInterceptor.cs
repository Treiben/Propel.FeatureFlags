using Castle.DynamicProxy;
using System.Reflection;

namespace Propel.FeatureFlags.Attributes.Interceptors;

internal sealed class FeatureFlagInterceptor(IFeatureFlagEvaluator evaluator) : IInterceptor
{
	public void Intercept(IInvocation invocation)
	{
		var flagAttribute = FeatureFlagCache.GetFeatureFlagAttribute(invocation);

		if (flagAttribute == null)
		{
			invocation.Proceed();
			return;
		}

		// Check if method is async
		if (IsAsyncMethod(invocation.Method))
		{
			new AsyncInterceptionHandler(evaluator).Handle(invocation, flagAttribute);
		}
		else
		{
			new SyncInterceptionHandler(evaluator).Handle(invocation, flagAttribute);
		}
	}

	private static bool IsAsyncMethod(MethodInfo method)
	{
		return method.ReturnType == typeof(Task) ||
			   method.ReturnType.IsGenericType && method.ReturnType.GetGenericTypeDefinition() == typeof(Task<>);
	}
}