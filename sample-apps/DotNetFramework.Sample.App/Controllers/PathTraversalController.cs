using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Web.Http;

namespace DotNetFramework.Sample.App.Controllers
{
    [RoutePrefix("api/path-traversal")]
    public class PathTraversalController : ApiController
    {
        private readonly string _baseDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Files");

        public PathTraversalController()
        {
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
                // Create some sample files
                File.WriteAllText(Path.Combine(_baseDirectory, "test.txt"), "This is a test file");
                File.WriteAllText(Path.Combine(_baseDirectory, "secret.txt"), "This is a secret file");
            }
        }

        [HttpGet]
        [Route("read")]
        public IHttpActionResult ReadFile()
        {
            var filename = GetQueryParam("file");
            if (string.IsNullOrEmpty(filename))
            {
                return BadRequest("Filename parameter is required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, filename);
                var content = File.ReadAllText(path);
                return Ok(content);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error reading file: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("list")]
        public IHttpActionResult ListFiles()
        {
            var directory = GetQueryParam("dir") ?? "";
            try
            {
                var path = Path.Combine(_baseDirectory, directory);
                var files = Directory.GetFiles(path);
                return Ok(files);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error listing directory: {ex.Message}");
            }
        }

        [HttpPost]
        [Route("write")]
        public IHttpActionResult WriteFile()
        {
            var filename = GetQueryParam("file");
            var content = GetQueryParam("content");

            if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(content))
            {
                return BadRequest("Both filename and content parameters are required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, filename);
                File.WriteAllText(path, content);
                return Ok($"File written successfully to {path}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error writing file: {ex.Message}");
            }
        }

        [HttpDelete]
        [Route("delete")]
        public IHttpActionResult DeleteFile()
        {
            var filename = GetQueryParam("file");
            if (string.IsNullOrEmpty(filename))
            {
                return BadRequest("Filename parameter is required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, filename);
                File.Delete(path);
                return Ok($"File {path} deleted successfully");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting file: {ex.Message}");
            }
        }

        [HttpPost]
        [Route("copy")]
        public IHttpActionResult CopyFile()
        {
            var sourceFile = GetQueryParam("source");
            var destFile = GetQueryParam("dest");

            if (string.IsNullOrEmpty(sourceFile) || string.IsNullOrEmpty(destFile))
            {
                return BadRequest("Both source and dest parameters are required");
            }

            try
            {
                var sourcePath = Path.Combine(_baseDirectory, sourceFile);
                var destPath = Path.Combine(_baseDirectory, destFile);
                File.Copy(sourcePath, destPath);
                return Ok($"File copied from {sourcePath} to {destPath}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error copying file: {ex.Message}");
            }
        }

        [HttpPost]
        [Route("move")]
        public IHttpActionResult MoveFile()
        {
            var sourceFile = GetQueryParam("source");
            var destFile = GetQueryParam("dest");

            if (string.IsNullOrEmpty(sourceFile) || string.IsNullOrEmpty(destFile))
            {
                return BadRequest("Both source and dest parameters are required");
            }

            try
            {
                var sourcePath = Path.Combine(_baseDirectory, sourceFile);
                var destPath = Path.Combine(_baseDirectory, destFile);
                File.Move(sourcePath, destPath);
                return Ok($"File moved from {sourcePath} to {destPath}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error moving file: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("list-directories")]
        public IHttpActionResult ListDirectories()
        {
            var directory = GetQueryParam("dir") ?? "";
            try
            {
                var path = Path.Combine(_baseDirectory, directory);
                var directories = Directory.GetDirectories(path);
                return Ok(directories);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error listing directories: {ex.Message}");
            }
        }

        [HttpPost]
        [Route("create-directory")]
        public IHttpActionResult CreateDirectory()
        {
            var directory = GetQueryParam("dir");
            if (string.IsNullOrEmpty(directory))
            {
                return BadRequest("Directory parameter is required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, directory);
                Directory.CreateDirectory(path);
                return Ok($"Directory created at {path}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error creating directory: {ex.Message}");
            }
        }

        [HttpDelete]
        [Route("delete-directory")]
        public IHttpActionResult DeleteDirectory()
        {
            var directory = GetQueryParam("dir");
            if (string.IsNullOrEmpty(directory))
            {
                return BadRequest("Directory parameter is required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, directory);
                Directory.Delete(path);
                return Ok($"Directory {path} deleted successfully");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting directory: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("full-path")]
        public IHttpActionResult GetFullPath()
        {
            var path = GetQueryParam("path");
            if (string.IsNullOrEmpty(path))
            {
                return BadRequest("Path parameter is required");
            }

            try
            {
                var fullPath = Path.GetFullPath(Path.Combine(_baseDirectory, path));
                return Ok($"Full path: {fullPath}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error getting full path: {ex.Message}");
            }
        }

        [HttpGet]
        [Route("open")]
        public IHttpActionResult OpenFile()
        {
            var filename = GetQueryParam("file");
            if (string.IsNullOrEmpty(filename))
            {
                return BadRequest("Filename parameter is required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, filename);
                using (var stream = File.Open(path, FileMode.Open))
                {
                    // Just demonstrate opening the file
                    return Ok($"File {path} opened successfully");
                }
            }
            catch (Exception ex)
            {
                return BadRequest($"Error opening file: {ex.Message}");
            }
        }

        private string GetQueryParam(string key)
        {
            var pairs = Request.GetQueryNameValuePairs();
            return pairs.FirstOrDefault(p => p.Key == key).Value;
        }
    }
}
