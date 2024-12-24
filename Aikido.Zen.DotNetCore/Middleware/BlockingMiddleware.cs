using Aikido.Zen.Core;
using Aikido.Zen.Core.Exceptions;
using Aikido.Zen.Core.Helpers;
using Aikido.Zen.Core.Models;
using Microsoft.AspNetCore.Http;

namespace Aikido.Zen.DotNetCore.Middleware
{
    internal class BlockingMiddleware : IMiddleware
    {
        public Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            var user = context.Items["Aikido.Zen.CurrentUser"] as User;
            if (user != null)
            {
                Agent.Instance.Context.AddUser(user, ipAddress: context.Connection.RemoteIpAddress?.ToString());
            }
            // block the request if the user is blocked
            if (Agent.Instance.Context.IsBlocked(user, context.Connection.RemoteIpAddress?.ToString(), $"{context.Request.Method}|{context.Request.Path}"))
            {
                Agent.Instance.Context.AddAbortedRequest();
                context.Response.StatusCode = 403;
            }

            return next(context);
        }
    }
}
