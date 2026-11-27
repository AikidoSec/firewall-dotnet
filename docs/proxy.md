# Proxy settings

`AIKIDO_TRUST_PROXY` controls how Zen determines the client IP in both ASP.NET Core and ASP.NET Framework:

- **`true` (default when unset or empty):** Zen selects the rightmost valid public IP from `X-Forwarded-For`, falling back to the request's current remote IP if none is found. Configure your proxy to supply trustworthy client IPs.
- **A positive integer:** Zen selects that header entry counting from the right. For example, `AIKIDO_TRUST_PROXY=2` selects the second entry from the right. The selected IP can be public or private. If it is missing, empty, or invalid, Zen falls back to the request's current remote IP.
- **`false`:** Zen uses the request's current remote IP directly and ignores the client IP header.

Boolean values are case-insensitive: `true`, `yes`, `y`, and `on` enable header parsing; `false`, `no`, `n`, `off`, and `0` disable it.

Use a hop count only when the proxy chain has a fixed length. Empty and invalid header entries retain their position in the count. `1` selects the rightmost entry; use `true` to select the rightmost valid public IP instead.

Set `AIKIDO_CLIENT_IP_HEADER=your-custom-header` if your proxy uses a different header. This setting is used only when `AIKIDO_TRUST_PROXY` is enabled.

If your application is directly accessible without a proxy, set `AIKIDO_TRUST_PROXY=false` so client-supplied headers cannot determine the client IP.

## ASP.NET Core

If you use [ASP.NET Core's `UseForwardedHeaders`](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/proxy-load-balancer) to resolve client IPs, set `AIKIDO_TRUST_PROXY=false`. Configure it to process the client IP header from your trusted proxies, and run it before `UseZenFirewall`.

Forwarded Headers Middleware updates the request's current remote IP and removes the entries it processes from `X-Forwarded-For`. Disabling Zen's header parsing makes Zen use that resolved IP instead of interpreting any remaining header entries.

Out-of-process IIS hosting can enable forwarding automatically, so check the host's forwarding configuration as well.

The request's current remote IP is `HttpContext.Connection.RemoteIpAddress`. With Zen's header parsing disabled, Zen uses its value when `UseZenFirewall` runs, including any changes made by earlier middleware.

## ASP.NET Framework

ASP.NET Framework does not have ASP.NET Core's `UseForwardedHeaders` middleware. Zen uses the same header selection described above, reading the configured header from IIS server variables.

The request's current remote IP is the `REMOTE_ADDR` server variable. Zen uses it directly when `AIKIDO_TRUST_PROXY=false`, or as a fallback when header parsing yields no client IP.

Behind a proxy, `REMOTE_ADDR` normally identifies the proxy unless your hosting configuration has already rewritten it. Keep Zen's header parsing enabled if the client IP is available only in the forwarded header.
