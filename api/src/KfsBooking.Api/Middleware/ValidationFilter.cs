using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KfsBooking.Api.Middleware;

public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;

    public ValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            var validator = _services.GetService(validatorType) as IValidator;
            if (validator is null) continue;

            var result = await validator.ValidateAsync(new ValidationContext<object>(argument));
            if (!result.IsValid) throw new ValidationException(result.Errors);
        }

        await next();
    }
}
