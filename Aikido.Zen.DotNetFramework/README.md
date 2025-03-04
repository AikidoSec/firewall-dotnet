# Aikido Zen for .NET Framework

## Diagnostics

If you're experiencing issues with the Aikido Zen agent, you can use the built-in diagnostic tools to troubleshoot.

### Enabling the Diagnostics Page

Add the following to your `web.config` file:

```xml
<configuration>
  <system.webServer>
    <handlers>
      <add name="AikidoZenDiagnostics" path="aikido-diagnostics" verb="*" type="Aikido.Zen.DotNetFramework.DiagnosticsHandler" />
    </handlers>
  </system.webServer>
</configuration>
```

### Securing the Diagnostics Page

For security, you can set an environment variable to require a key for accessing the diagnostics page:

```
AIKIDO_DIAGNOSTICS_KEY=your_secret_key
```

Then access the page with:

```
http://your-site/aikido-diagnostics?key=your_secret_key
```

### Troubleshooting Background Task Issues

If the background task is not running (showing status "WaitingForActivation"), you can:

1. Visit the diagnostics page
2. Click the "Restart Agent" button
3. Check the new status

### Enabling Debug Logging

To see more detailed logs, set the following environment variable:

```
AIKIDO_DEBUG=true
```

### Common Issues

#### Background Task Not Running

If the background task shows "WaitingForActivation" status:

1. Make sure your application pool is configured correctly
2. Try restarting the agent using the diagnostics page
3. Check for any exceptions in the Application Event Log

#### No Logs in Debug Output

If you're not seeing logs in the debug output:

1. Make sure `AIKIDO_DEBUG` is set to `true`
2. Check if you have a custom logger configured that might be capturing the logs
3. Look for logs in the console output or application event log

## Programmatic Access to Diagnostics

You can also access the diagnostics programmatically:

```csharp
// Get the current status of the agent
string status = Aikido.Zen.DotNetFramework.Zen.GetAgentStatus();

// Restart the agent
string result = Aikido.Zen.DotNetFramework.Zen.RestartAgent();
```
