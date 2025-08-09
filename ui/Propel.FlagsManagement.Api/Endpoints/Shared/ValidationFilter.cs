using FluentValidation;

namespace FeatureRabbit.Management.Api.Endpoints.Shared;

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
						if (!failureDictionary.ContainsKey(error.PropertyName))
						{
							failureDictionary[error.PropertyName] = new string[] { error.ErrorMessage };
						}
						else
						{
							var existingErrors = failureDictionary[error.PropertyName].ToList();
							existingErrors.Add(error.ErrorMessage);
							failureDictionary[error.PropertyName] = existingErrors.ToArray();
						}
					}
					return (IResult)Results.ValidationProblem(failureDictionary);
				}
			}
		}

		return await next(context);
	}
}
