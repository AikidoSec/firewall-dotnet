var builder = WebApplication.CreateBuilder(args);

var startup = new NewRelicSampleApp.EFCoreSqliteStartup();
startup.ConfigureServices(builder.Services);

var app = builder.Build();

startup.Configure(app);

app.Run();
