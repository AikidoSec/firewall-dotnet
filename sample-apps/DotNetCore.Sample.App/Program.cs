using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Npgsql;
using MySql.Data.MySqlClient;
using System.Data.Common;
using DotNetCore.Sample.App;
using Aikido.Zen.DotNetCore;
using System.Net;
using RestSharp;
using System.Xml.Linq;
using Microsoft.AspNetCore.Mvc;


/// <summary>
/// Creates and configures the web application
/// </summary>
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddZenFireWall();
builder.Services.AddEndpointsApiExplorer();
builder.Services
    .AddControllers()
    .AddXmlDataContractSerializerFormatters();

var app = builder.Build();
app
    // add the firewall first
    .UseZenFireWall()
    // add routing
    .UseRouting()
    // add controllers
    .UseEndpoints(endpoints => endpoints.MapControllers());

app.Run();
