namespace MongoDbSampleApp;

public class Program
{
    public static void Main(string[] args)
    {
        var app = AppBuilderHelper.CreateApp(args);
        app.Run();
    }
}
