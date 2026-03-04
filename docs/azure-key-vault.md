# Using Azure Key Vault for Aikido Token

## Add Key Vault to configuration

**Program.cs**
```csharp
using Azure.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add Azure Key Vault as a configuration source
builder.Configuration.AddAzureKeyVault(
    new Uri("https://your-vault-name.vault.azure.net/"),
    new DefaultAzureCredential()
);

var app = builder.Build();
app
// ...
    .UseRouting()
// ...
    .UseZenFirewall()
```

## Store token in Key Vault

Add a secret named `Aikido--AikidoToken` (note the double hyphens):

```bash
az keyvault secret set --vault-name your-vault-name --name "Aikido--AikidoToken" --value "your-aikido-token"
```

Zen will automatically look for `configuration["Aikido:AikidoToken"]`. The `--` separator in Key Vault secret names is automatically converted to `:` for .NET configuration.
