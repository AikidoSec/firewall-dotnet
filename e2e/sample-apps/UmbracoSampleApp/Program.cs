using UmbracoSampleApp;
using Aikido.Zen.DotNetCore;

public class Program
{
    public static async Task Main(string[] args)
    {
        var app = await AppBuilderHelper.CreateApp(args);

        await app.RunAsync();
    }
}
