using SQLiteSampleApp;

var builder = Host
    .CreateDefaultBuilder(args)
    .ConfigureWebHostDefaults(webBuilder =>
        webBuilder.UseStartup<SQLiteStartup>()
    );

var app = builder.Build();
app.Run();
