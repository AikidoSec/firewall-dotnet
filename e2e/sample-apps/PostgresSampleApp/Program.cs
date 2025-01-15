using PostgresSampleApp;

var builder = Host
    .CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder =>
        webBuilder.UseStartup<PostgresStartup>()
    );

var app = builder.Build();
app.Run();
