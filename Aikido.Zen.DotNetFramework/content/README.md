# Aikido.Zen.DotNetFramework

## Handling Dependency Version Conflicts

The Aikido.Zen.DotNetFramework package has multiple dependencies, but is designed to be as forward compatilbe as possible.

If you encounter runtime errors related to version conflicts with for example `Microsoft.
Extensions.Logging.Abstractions`, you may need to manually add binding
redirects to your `app.config` or `web.config` file:

```xml
<configuration>
  <runtime>
    <assemblyBinding xmlns="urn:schemas-microsoft-com:asm.v1">
      <dependentAssembly>
        <assemblyIdentity name="Microsoft.Extensions.Logging.Abstractions"
        publicKeyToken="adb9793829ddae60" culture="neutral" />
        <bindingRedirect oldVersion="0.0.0.0-99.0.0.0"
        newVersion="YOUR_VERSION" />
      </dependentAssembly>
    </assemblyBinding>
  </runtime>
</configuration>
```

### Automatic Binding Redirects

The package automatically adds binding redirects to your app.config or web.config file. This should handle most version conflict scenarios without any manual intervention.

### Manual Configuration (if needed)

If you encounter runtime errors related to version conflicts with any of the dependencies, you may need to manually add binding redirects to your `app.config` or `web.config` file. Below is an example for `Microsoft.Extensions.Logging.Abstractions`:
