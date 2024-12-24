using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace DotNetCore.Sample.App.Controllers
{
    [ApiController]
    [Route("path-traversal")]
    public class PathTraversalController : ControllerBase
    {
        private readonly string _baseDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Files");

        public PathTraversalController()
        {
            if (!Directory.Exists(_baseDirectory))
            {
                Directory.CreateDirectory(_baseDirectory);
                // Create some sample files
                System.IO.File.WriteAllText(Path.Combine(_baseDirectory, "test.txt"), "This is a test file");
                System.IO.File.WriteAllText(Path.Combine(_baseDirectory, "secret.txt"), "This is a secret file");
            }
        }

        [HttpGet("read")]
        public IActionResult ReadFile()
        {
            var filename = Request.Query["file"].ToString();
            if (string.IsNullOrEmpty(filename))
            {
                return BadRequest("Filename parameter is required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, filename);
                var content = System.IO.File.ReadAllText(path);
                return Ok(content);
            }
            catch (Exception ex)
            {
                return BadRequest($"Error reading file: {ex.Message}");
            }
        }

        [HttpGet("list")]
        public IActionResult ListFiles()
        {
            var directory = Request.Query["dir"].ToString() ?? "";
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

        [HttpPost("write")]
        public IActionResult WriteFile()
        {
            var filename = Request.Query["file"].ToString();
            var content = Request.Query["content"].ToString();

            if (string.IsNullOrEmpty(filename) || string.IsNullOrEmpty(content))
            {
                return BadRequest("Both filename and content parameters are required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, filename);
                System.IO.File.WriteAllText(path, content);
                return Ok($"File written successfully to {path}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error writing file: {ex.Message}");
            }
        }

        [HttpDelete("delete")]
        public IActionResult DeleteFile()
        {
            var filename = Request.Query["file"].ToString();
            if (string.IsNullOrEmpty(filename))
            {
                return BadRequest("Filename parameter is required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, filename);
                System.IO.File.Delete(path);
                return Ok($"File {path} deleted successfully");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error deleting file: {ex.Message}");
            }
        }

        [HttpPost("copy")]
        public IActionResult CopyFile()
        {
            var sourceFile = Request.Query["source"].ToString();
            var destFile = Request.Query["dest"].ToString();

            if (string.IsNullOrEmpty(sourceFile) || string.IsNullOrEmpty(destFile))
            {
                return BadRequest("Both source and dest parameters are required");
            }

            try
            {
                var sourcePath = Path.Combine(_baseDirectory, sourceFile);
                var destPath = Path.Combine(_baseDirectory, destFile);
                System.IO.File.Copy(sourcePath, destPath);
                return Ok($"File copied from {sourcePath} to {destPath}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error copying file: {ex.Message}");
            }
        }

        [HttpPost("move")]
        public IActionResult MoveFile()
        {
            var sourceFile = Request.Query["source"].ToString();
            var destFile = Request.Query["dest"].ToString();

            if (string.IsNullOrEmpty(sourceFile) || string.IsNullOrEmpty(destFile))
            {
                return BadRequest("Both source and dest parameters are required");
            }

            try
            {
                var sourcePath = Path.Combine(_baseDirectory, sourceFile);
                var destPath = Path.Combine(_baseDirectory, destFile);
                System.IO.File.Move(sourcePath, destPath);
                return Ok($"File moved from {sourcePath} to {destPath}");
            }
            catch (Exception ex)
            {
                return BadRequest($"Error moving file: {ex.Message}");
            }
        }

        [HttpGet("list-directories")]
        public IActionResult ListDirectories()
        {
            var directory = Request.Query["dir"].ToString() ?? "";
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

        [HttpPost("create-directory")]
        public IActionResult CreateDirectory()
        {
            var directory = Request.Query["dir"].ToString();
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

        [HttpDelete("delete-directory")]
        public IActionResult DeleteDirectory()
        {
            var directory = Request.Query["dir"].ToString();
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

        [HttpGet("full-path")]
        public IActionResult GetFullPath()
        {
            var path = Request.Query["path"].ToString();
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

        [HttpGet("open")]
        public IActionResult OpenFile()
        {
            var filename = Request.Query["file"].ToString();
            if (string.IsNullOrEmpty(filename))
            {
                return BadRequest("Filename parameter is required");
            }

            try
            {
                var path = Path.Combine(_baseDirectory, filename);
                using (var stream = System.IO.File.Open(path, FileMode.Open))
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
    }
}
