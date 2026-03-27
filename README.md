![Zen by Aikido for .NET](./docs/banner.svg)


# Zen, in-app firewall for .NET | by Aikido
[![Codecov](https://img.shields.io/codecov/c/github/AikidoSec/firewall-dotnet?style=flat-square&token=J74AVIU17K)](https://app.codecov.io/gh/aikidosec/firewall-dotnet)
[![PRs Welcome](https://img.shields.io/badge/PRs-welcome-brightgreen.svg?style=flat-square)](http://makeapullrequest.com)
[![Unit tests](https://github.com/AikidoSec/firewall-dotnet/actions/workflows/unit-test.yml/badge.svg)](https://github.com/AikidoSec/firewall-dotnet/actions/workflows/unit-test.yml)
[![NuGet version (Aikido.Zen.DotNetCore)](https://img.shields.io/nuget/v/Aikido.Zen.DotNetCore.svg?style=flat-square&label=Aikido.Zen.DotNetCore)](https://www.nuget.org/packages/Aikido.Zen.DotNetCore/)
[![NuGet version (Aikido.Zen.DotNetFramework)](https://img.shields.io/nuget/v/Aikido.Zen.DotNetFramework.svg?style=flat-square&label=Aikido.Zen.DotNetFramework)](https://www.nuget.org/packages/Aikido.Zen.DotNetFramework/)

Zen, your in-app firewall for peace of mind– at runtime.

Zen is an embedded Web Application Firewall that autonomously protects your .NET apps against common and critical attacks.

Zen protects your .NET apps by preventing user input containing dangerous strings, which allow SQL injections. It runs on the same server as your .NET app for easy installation and zero maintenance.

Zen for .NET currently supports onwards of .NET 4.6. The latest tested version is .NET 10.0.

## Features

Zen will autonomously protect your .NET applications from the inside against:

* 🛡️ [SQL injection attacks](https://www.aikido.dev/blog/the-state-of-sql-injections)
* 🛡️ [Path traversal attacks](https://www.aikido.dev/blog/path-traversal-in-2024-the-year-unpacked)
* 🛡️ [Command injection attacks](https://www.aikido.dev/blog/command-injection-in-2024-unpacked)
* 🚧 [Server-side request forgery (SSRF)](https://owasp.org/www-community/attacks/Server_Side_Request_Forgery)
* 🚧 [NoSQL injection attacks](https://www.aikido.dev/blog/web-application-security-vulnerabilities)

Zen operates autonomously on the same server as your .NET app to:

* ✅ Secure your app like a classic web application firewall (WAF), but with none of the infrastructure or cost.
* ✅ Rate limit specific API endpoints by IP or by user
* ✅ Allow you to block specific users manually
* ✅ Allow you to block bots and AI scrapers
* ✅ Allow you to allow traffic by ip per endpoint
* ✅ Allow you to bypass the firewall by ip
* ✅ Geo-fencing to block or allow a selection of countries


## Supported libraries and frameworks

### Web frameworks
* ✅ ASP.NET Core 6.0
* ✅ ASP.NET Core 7.0
* ✅ ASP.NET Core 8.0
* ✅ ASP.NET Core 9.0
* ✅ ASP.NET Core 10.0
* ✅ ASP.NET Framework 4.6.x
* ✅ ASP.NET Framework 4.7.x
* ✅ ASP.NET Framework 4.8.x

### Database drivers
* ✅ Microsoft.Data.SqlClient
* ✅ System.Data.SqlClient
* ✅ System.Data.SqlServerCE (.NET Framework)
* ✅ Microsoft.Data.Sqlite
* ✅ MySql.Data.MySqlClient
* ✅ MySqlConnector
* ✅ Npgsql
* ✅ MySqlX

### Supported ORM frameworks
* ✅ NPoco
* ✅ EF Core

## Installation

### .NET Core

Ensure that your project runs on .NET Core 6, 7, 8, 9 or 10. Additionally, your application must use endpoint routing (`UseRouting`) so Zen Firewall can resolve route information correctly. Legacy routing middleware such as `UseMvc` is not supported. See the ASP.NET Core migration guide [here](https://learn.microsoft.com/en-us/aspnet/core/migration/22-to-30).

- Install the package from NuGet:

``` shell
dotnet add package Aikido.Zen.DotNetCore
```

- Configure your Aikido token with secure configuration providers (recommended):

For local development, use .NET Secret Manager:

``` shell
dotnet user-secrets init
dotnet user-secrets set "Aikido:AikidoToken" "<YOUR-TOKEN-HERE>"
```

For deployment, use environment variables:

``` shell
AIKIDO_TOKEN=<YOUR-TOKEN-HERE>
```

Avoid storing real tokens in `appsettings.json` or `appsettings.Development.json` (especially in source control). If you use a cloud secret store, see:
- [Azure Key Vault](docs/azure-key-vault.md)
- [AWS Secrets Manager](docs/aws-secrets-manager.md)

If you are using a startup class, you can add the following to your `Startup.cs` file:

``` csharp
public void ConfigureServices(IServiceCollection services)
{
    // other services
    services.AddZenFirewall();
    // other services
}

public void Configure(IApplicationBuilder app)
{
    // other middleware
    app.UseZenFirewall(); // place this after UseRouting, or after authorization, but high enough in the pipeline to catch all requests
    // other middleware
}
```

You can also set the user in your custom middleware, if you would like to block users by their identity.

``` csharp
// ...
using Aikido.Zen.DotNetCore;
using Microsoft.AspNet.Identity;
// ...
// add routing
    .UseRouting()
    // authorize users
    .Use((context, next) =>
    {
        // unique id for the user
        var id = context.User?.Identity?.GetUserId();
        // name for the user, can be same as id
        var name = context.User?.Identity?.Name;
        if (!string.IsNullOrEmpty(id))
            Zen.SetUser(id, name, context);
        return next();
    })
    // add Zen middleware
    .UseZenFirewall()
```

### .NET Framework

Ensure that your project runs on .NET Framework 4.6 or higher.

- Install the package from NuGet:

``` shell
dotnet add package Aikido.Zen.DotNetFramework
```

or

``` shell
Install-Package Zen.Aikido.DotNetFramework
```

- To add the Aikido token in the Web.config file, follow these steps:

1. Open your `Web.config` file.
2. Locate the `<appSettings>` section.
3. Add the following key-value pair within the `<appSettings>` section:

``` xml
<add key="Aikido:AikidoToken" value="your-api-key" />
```

- in your Global.asax.cs file, add the following:

``` csharp
protected void Application_Start()
{
    // other code
    Zen.Start();
}
```

if you are using OWIN, you can add the following to your `Startup.cs` file:

``` csharp
public void Configuration(IAppBuilder app)
{
    // other code
    Zen.Start();
}
```

- Zen needs to run for all requests to properly detect attacks. This can be enforced in `Web.config` as such:

``` xml
<system.webServer>
  <modules runAllManagedModulesForAllRequests="true" />
</system.webServer>
```

- If you would like to block users by their identity, you can pass in a function to set the user, in your Global.asax.cs file.

``` csharp
public void Application_Start()
{
    // other code
    // userId should be unique
    // userName is optional
    // context.User.Identity.GetUserId() and .Name are available to use when authentication is implemented
    Zen.SetUser(context => new User(userId, userName));
    Zen.Start();
}
```

- If using OWIN, you can add the following to your `Startup.cs` file:

``` csharp
// ...
using Aikido.Zen.DotNetFramework;
using Aikido.Zen.Core;
using Microsoft.AspNet.Identity;
// ...
public void Configuration(IAppBuilder app)
{
    // other code
    // set the user:
    // userId should be unique eg. User.Identity.GetUserId()
    // userName is optional eg. context.User.Identity.Name
    Zen.SetUser(context => new User(context.User.Identity.GetUserId(), context.User.Identity.Name));
    Zen.Start();
}
```

## Guides

- [Troubleshooting](docs/troubleshooting.md) — common issues and how to debug Zen
- [Azure Key Vault](docs/azure-key-vault.md) — using Azure Key Vault with Zen
- [AWS Secrets Manager](docs/aws-secrets-manager.md) — using AWS Secrets Manager with Zen
- [Set the current user](docs/user.md) — identify users for rate limiting, blocking, and attack reports

## Reporting to your Aikido Security dashboard

> Aikido is your no nonsense application security platform. One central system that scans your source code & cloud, shows you what vulnerabilities matter, and how to fix them - fast. So you can get back to building.

Zen is a new product by Aikido. Built for developers to level up their security. While Aikido scans, get Zen for always-on protection.

You can use some of Zen’s features without Aikido, of course. Peace of mind is just a few lines of code away.

But you will get the most value by reporting your data to Aikido.

You will need an Aikido account and a token to report events to Aikido. If you don't have an account, you can [sign up for free](https://app.aikido.dev/login).

Here's how:
* [Log in to your Aikido account](https://app.aikido.dev/login).
* Go to [Zen](https://app.aikido.dev/runtime/services).
* Go to apps.
* Click on **Add app**.
* Choose a name for your app.
* Click **Generate token**.
* Copy the token.
* Set the token as the environment variable `AIKIDO_TOKEN`


## Running in production (blocking) mode

By default, Zen will only detect and report attacks to Aikido.

To block requests, set the `AIKIDO_BLOCK` environment variable to `true`.

See [Reporting to Aikido](#reporting-to-your-aikido-security-dashboard) to learn how to send events to Aikido.

## Additional configuration

[Configure Zen using environment variables for authentication, mode settings, debugging, and more.](https://help.aikido.dev/doc/configuration-via-env-vars/docrSItUkeR9)

## License

This program is offered under a commercial and under the AGPL license.
You can be released from the requirements of the AGPL license by purchasing
a commercial license. Buying such a license is mandatory as soon as you
develop commercial activities involving the Zen software without
disclosing the source code of your own applications.

For more information, please contact Aikido Security at this
address: support@aikido.dev or create an account at https://app.aikido.dev.

## Performance

![Under construction](https://img.icons8.com/emoji/20/000000/construction-emoji.png) Under construction ![Under construction](https://img.icons8.com/emoji/20/000000/construction-emoji.png)

## Code of Conduct

See [CODE_OF_CONDUCT.md](.github/CODE_OF_CONDUCT.md) for more information.

## Security

Our bug bounty program is public and can be found by all registered Intigriti users at: https://app.intigriti.com/researcher/programs/aikido/aikidoruntime

See [SECURITY.md](.github/SECURITY.md) for more information.
