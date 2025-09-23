using FluentValidation;

namespace Propel.FeatureFlags.Dashboard.Api.Endpoints.Shared;

// Validation Filter for automatic validation
public class ValidationFilter<T> : IEndpointFilter where T : class
{
	public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
	{
		var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
		if (validator is not null)
		{
			var entity = context.Arguments.OfType<T>().FirstOrDefault();
			if (entity is not null)
			{
				var validation = await validator.ValidateAsync(entity);
				if (!validation.IsValid)
				{
					var failureDictionary = new Dictionary<string, string[]>();
					foreach (var error in validation.Errors)
					{
						if (!failureDictionary.TryGetValue(error.PropertyName, out string[]? value))
						{
							failureDictionary[error.PropertyName] = [error.ErrorMessage];
						}
						else
						{
							var existingErrors = value.ToList();
							existingErrors.Add(error.ErrorMessage);
							failureDictionary[error.PropertyName] = [.. existingErrors];
						}
					}
					return Results.ValidationProblem(failureDictionary);
				}
			}
		}

		return await next(context);
	}
}
