using Aikido.Zen.DotNetCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddZenFireWall();
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

var app = builder.Build();

app.UseZenFireWall();

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
