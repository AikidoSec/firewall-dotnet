using SqlServerSampleApp;

var builder = Host
    .CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder =>
        webBuilder.UseStartup<SqlServerStartup>()
    );

var app = builder.Build();
app.Run();
