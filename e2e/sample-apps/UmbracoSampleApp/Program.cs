using Aikido.Zen.DotNetCore;

namespace UmbracoSampleApp
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var app = await AppBuilderHelper.CreateApp(args);

            await app.RunAsync();
        }
    }
}
