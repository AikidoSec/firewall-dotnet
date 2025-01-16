using MySqlSampleApp;

var builder = Host
    .CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder =>
        webBuilder.UseStartup<MySqlStartup>()
    );

var app = builder.Build();
app.Run();
