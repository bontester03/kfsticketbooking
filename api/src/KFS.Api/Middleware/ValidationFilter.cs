using FluentValidation;
using Microsoft.AspNetCore.Mvc.Filters;

namespace KFS.Api.Middleware;

public class ValidationFilter : IAsyncActionFilter
{
    private readonly IServiceProvider _services;
    public ValidationFilter(IServiceProvider services) => _services = services;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is null) continue;
            var validatorType = typeof(IValidator<>).MakeGenericType(arg.GetType());
            if (_services.GetService(validatorType) is not IValidator validator) continue;
            var ctx = new ValidationContext<object>(arg);
            var result = await validator.ValidateAsync(ctx);
            if (!result.IsValid) throw new ValidationException(result.Errors);
        }
        await next();
    }
}
