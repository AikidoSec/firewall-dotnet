using Aikido.Zen.Core.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace UmbracoSampleApp.Controllers
{
    public class PathTraversalController
    {
        public virtual void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api/path-traversal", (HttpContext httpContext) =>
            {
                var filePath = httpContext.Request.Query.TryGetValue("path", out var pathValues) ? pathValues.FirstOrDefault() : null;

                if (string.IsNullOrEmpty(filePath))
                {
                    return Results.BadRequest("Path parameter is required");
                }

                try
                {
                    filePath = Uri.UnescapeDataString(filePath);
                    var fullPath = GetTestFilePath(filePath);
                    return ReadFile(httpContext, filePath, fullPath);
                }
                catch (AikidoException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    return Results.BadRequest($"Error reading file: {ex.Message}");
                }
            });
        }

        private static string GetTestFilePath(string filePath)
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "aikido-test");
            Directory.CreateDirectory(tempDir);

            var secretFile = Path.Combine(tempDir, "secret.txt");
            var safeFile = Path.Combine(tempDir, "safe.txt");
            var anotherSafeFile = Path.Combine(tempDir, "another-safe.txt");
            var thirdSafeFile = Path.Combine(tempDir, "third-safe.txt");

            File.WriteAllText(secretFile, "This is a secret file that should not be accessible!");
            File.WriteAllText(safeFile, "This is a safe file.");
            File.WriteAllText(anotherSafeFile, "This is another safe file.");
            File.WriteAllText(thirdSafeFile, "This is a third safe file.");

            return tempDir + Path.DirectorySeparatorChar + filePath;
        }

        private static IResult ReadFile(HttpContext httpContext, string requestedPath, string fullPath)
        {
            var content = File.ReadAllText(fullPath);

            return Results.Ok(new
            {
                content = content,
                requestedPath = requestedPath,
                resolvedPath = fullPath,
                allFlattenedParams = httpContext.Request.Query.ToDictionary(query => query.Key, query => query.Value.FirstOrDefault())
            });
        }
    }
}
