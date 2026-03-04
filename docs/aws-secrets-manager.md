# Using AWS Secrets Manager for Aikido Token

## Add Secrets Manager to configuration

Install the AWS Secrets Manager SDK:

```bash
dotnet add package AWSSDK.SecretsManager
```

**Program.cs**
```csharp
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

var builder = WebApplication.CreateBuilder(args);

const string secretName = "your-secret-name";
if (!string.IsNullOrWhiteSpace(secretName))
{
    var token = await GetAikidoTokenFromAwsSecretsManagerAsync(secretName);
    if (!string.IsNullOrWhiteSpace(token))
    {
        builder.Configuration["Aikido:AikidoToken"] = token;
    }
}

static async Task<string?> GetAikidoTokenFromAwsSecretsManagerAsync(string secretName)
{
    var awsRegion = Environment.GetEnvironmentVariable("AWS_REGION")
        ?? Environment.GetEnvironmentVariable("AWS_DEFAULT_REGION");

    using IAmazonSecretsManager client = string.IsNullOrWhiteSpace(awsRegion)
        ? new AmazonSecretsManagerClient()
        : new AmazonSecretsManagerClient(RegionEndpoint.GetBySystemName(awsRegion));

    var response = await client.GetSecretValueAsync(new GetSecretValueRequest
    {
        SecretId = secretName,
        VersionStage = "AWSCURRENT"
    });

    return response.SecretString;
}

var app = builder.Build();
app
// ...
    .UseRouting()
// ...
    .UseZenFirewall()
```

## Store token in Secrets Manager

Set a secret name and store the token as a plain string:

```bash
aws secretsmanager create-secret \
  --name your-secret-name \
  --secret-string "your-aikido-token"
```

If the secret already exists, update it:

```bash
aws secretsmanager put-secret-value \
  --secret-id your-secret-name \
  --secret-string "your-aikido-token"
```

Required IAM permission: `secretsmanager:GetSecretValue`.

If you get `No RegionEndpoint or ServiceURL configured`, set one of:
- `AWS_REGION`
- `AWS_DEFAULT_REGION`

Zen automatically looks for `configuration["Aikido:AikidoToken"]`. The code above sets that value from Secrets Manager before `AddZenFirewall()` runs.
