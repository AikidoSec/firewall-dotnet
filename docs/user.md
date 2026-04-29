# Setting the current user

## .NET Core

To set the current user, you can use the `Zen.SetUser` method in your middleware:

> [!WARNING]
> Do not call `SetUser` with a shared user ID for unauthenticated or anonymous users (e.g. `SetUser("unauthenticated", "Anonymous")`). When a user is set, rate limiting is applied per user ID instead of per IP address. This means all anonymous users would share a single rate limit bucket and be blocked as a group. For unauthenticated users, simply don't call `SetUser` — rate limiting will automatically fall back to per-IP limiting.

```csharp
using Aikido.Zen.DotNetCore;
using Microsoft.AspNet.Identity;

// ...
    .UseRouting()
    .Use((context, next) =>
    {
        // Get the user from your authentication middleware
        var id = context.User?.Identity?.GetUserId();
        var name = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(id))
            Zen.SetUser(id, name, context);
        return next();
    })
    .UseZenFirewall()
```

## .NET Framework

In your `Global.asax.cs` file:

```csharp
public void Application_Start()
{
    // other code
    Zen.SetUser(context => new User(context.User.Identity.GetUserId(), context.User.Identity.Name));
    Zen.Start();
}
```

Or if you are using OWIN, in your `Startup.cs` file:

```csharp
using Aikido.Zen.DotNetFramework;
using Aikido.Zen.Core;
using Microsoft.AspNet.Identity;

public void Configuration(IAppBuilder app)
{
    // other code
    Zen.SetUser(context => new User(context.User.Identity.GetUserId(), context.User.Identity.Name));
    Zen.Start();
}
```

## Benefits

Using `SetUser` has the following benefits:

- The user ID is used for more accurate rate limiting (you can change IP addresses, but you can't change your user ID).
- Whenever attacks are detected, the user will be included in the report to Aikido.
- The dashboard will show all your users, where you can also block them.
- Passing the user's name is optional, but it can help you identify the user in the dashboard. You will be required to list Aikido Security as a subprocessor if you choose to share personal identifiable information (PII).

# Rate limiting groups

To limit the number of requests for a group of users, set a rate limit group for the current request. When a rate limit group is set, configured rate limits are applied to the group instead of individual users or IP addresses.

In the examples below, `GetTeamId(context)` is a placeholder that returns a team ID for the current request.

## .NET Core

Call `Zen.SetRateLimitGroup` in middleware before `UseZenFirewall()`:

```csharp
using Aikido.Zen.DotNetCore;

// ...
    .UseRouting()
    .Use((context, next) =>
    {
        var teamId = GetTeamId(context);
        if (!string.IsNullOrEmpty(teamId))
            Zen.SetRateLimitGroup(teamId, context);
        return next();
    })
    .UseZenFirewall()
```

## .NET Framework

In your `Global.asax.cs` file:

```csharp
public void Application_Start()
{
    // other code
    Zen.SetRateLimitGroup(context => GetTeamId(context));
    Zen.Start();
}
```
