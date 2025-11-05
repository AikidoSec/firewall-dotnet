# Troubleshooting

## Check logs for errors

ASP.NET Core (container or service):
- Docker: `docker logs <your-app-container>`
- systemd: `journalctl -u <your-app-service> --since "1 hour ago"`

Increase verbosity to see Zen messages by setting the log level for the Aikido namespace:

**appsettings.json**
{
  "Logging": {
    "LogLevel": {
      "Aikido.Zen": "Information",
      "Aikido.Zen.Core": "Information"
    }
  }
}

Zen writes through Microsoft.Extensions.Logging; look for entries from `Aikido.Zen` and `Aikido.Zen.Core`.  [oai_citation:0‡GitHub](https://github.com/AikidoSec/firewall-dotnet)

## Check if Zen is enabled

1) Confirm the package is installed:

`dotnet list package | grep Aikido.Zen`

Expected: `Aikido.Zen.DotNetCore` (for ASP.NET Core) or `Aikido.Zen.DotNetFramework` (for .NET Framework).  [oai_citation:1‡GitHub](https://github.com/AikidoSec/firewall-dotnet)

2) Confirm middleware/startup is wired:

- ASP.NET Core: `UseZenFirewall()` is in the pipeline, ideally high enough to catch all requests.
  Search your codebase: `grep -R "UseZenFirewall" -n .`  [oai_citation:2‡GitHub](https://github.com/AikidoSec/firewall-dotnet)

- .NET Framework: `Zen.Start()` is called (e.g., in `Global.asax.cs` or OWIN `Startup.cs`).
  Search your codebase: `grep -R "Zen.Start" -n .`  [oai_citation:3‡GitHub](https://github.com/AikidoSec/firewall-dotnet)

3) Confirm the Aikido token is configured:

- Environment variable: `echo $AIKIDO_TOKEN` (Linux/macOS) or `echo $Env:AIKIDO_TOKEN` (PowerShell)
- ASP.NET Core `appsettings.json` key: `"Aikido": { "AikidoToken": "your-api-key" }`
- .NET Framework `Web.config`: `<add key="Aikido:AikidoToken" value="your-api-key" />`  [oai_citation:4‡GitHub](https://github.com/AikidoSec/firewall-dotnet)

## Contact support

If you still can’t resolve the issue:

- Use the in-app chat to reach our support team directly.
- Or create an issue on [GitHub](../../issues) with details about your setup, framework, and logs.

Include as much context as possible (framework, logs, and how Aikido was added) so we can help you quickly.
