using Aikido.Zen.Server.Mock.Services;

namespace Aikido.Zen.Server.Mock.Filters;

public class AuthFilter : IEndpointFilter
{
    private readonly AppService _appService;

    public AuthFilter(AppService appService)
    {
        _appService = appService;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var token = context.HttpContext.Request.Headers.Authorization.ToString();

        if (string.IsNullOrEmpty(token))
        {
            return Results.Unauthorized();
        }

        var app = _appService.GetByToken(token);

        if (app == null)
        {
            return Results.Unauthorized();
        }

        context.HttpContext.Items["app"] = app;
        return await next(context);
    }
} 