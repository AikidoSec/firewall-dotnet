using MongoDB.Driver;
using Aikido.Zen.DotNetCore;
using MongoDB.Bson;
using Aikido.Zen.Core.Exceptions;

namespace MongoDbSampleApp
{
    /// <summary>
    /// Helper class to configure and create a WebApplication instance for the MongoDB app.
    /// </summary>
    public static class AppBuilderHelper
    {
        /// <summary>
        /// Configures and returns a WebApplication instance.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        /// <returns>Configured WebApplication instance.</returns>
        public static WebApplication CreateApp(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddZenFirewall();
            // Use the connection string from WebApplicationTestBase for MongoDB
            builder.Services.AddSingleton<IMongoClient>(new MongoClient("mongodb://root:password@127.0.0.1:27017"));

            var app = builder.Build();

            // Configure middleware
            app.UseZenFirewall();
            app.UseHttpsRedirection();

            app.Use(async (context, next) =>
            {
                try
                {
                    await next(context);
                }
                catch (AikidoException ex)
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    await context.Response.WriteAsync(ex.Message);
                }
                catch (Exception ex)
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    throw;
                }
            });

            // Configure endpoints
            app.MapGet("/", async (HttpContext context, IMongoClient client) =>
            {
                var search = context.Request.Query["search"].ToString();
                var database = client.GetDatabase("test");
                var collection = database.GetCollection<BsonDocument>("items");

                // This endpoint is vulnerable to NoSQL injection
                // The search parameter is directly inserted into the filter without validation or sanitization
                var filter = new BsonDocument("title", new BsonDocument("$eq", search));
                var result = await collection.Find(filter).ToListAsync();

                return Results.Ok(result);
            });

            app.MapGet("/where", async (HttpContext context, IMongoClient client) =>
            {
                var title = context.Request.Query["title"].ToString();
                var database = client.GetDatabase("test");
                var collection = database.GetCollection<BsonDocument>("items");

                // This endpoint is vulnerable to NoSQL injection
                var filter = BsonDocument.Parse($"{{ title: '{title}' }}");
                var result = await collection.Find(filter).ToListAsync();

                return Results.Ok(result);
            });

            return app;
        }
    }
}
