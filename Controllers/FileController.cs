using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NasBridgeApi.Models;
using NasBridgeApi.Services;

namespace NasBridgeApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileController : ControllerBase
    {
        private readonly NasService _nasService;

        public FileController(NasService nasService)
        {
            _nasService = nasService;
        }

        [HttpGet("list")]
        public async Task<IActionResult> ListFiles(string relativePath = "")
        {
            try
            {
                var files = await _nasService.ListFilesAsync(relativePath);
                return Ok(files);
            }
            catch (DirectoryNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpPost("upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFile([FromForm] FileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("File is missing");

            var fullRelativePath = Path.Combine(request.RelativePath ?? "", request.File.FileName)
                .Replace("\\", "/");

            using var stream = request.File.OpenReadStream();
            await _nasService.UploadFileAsync(fullRelativePath, stream);

            return Ok("File uploaded successfully");
        }

        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string relativePath)
        {
            try
            {
                var stream = await _nasService.DownloadFileAsync(relativePath);
                var fileName = Path.GetFileName(relativePath);
                var fileSize = stream.Length;

                // Check for Range header
                if (Request.Headers.ContainsKey("Range"))
                {
                    var rangeHeader = Request.Headers["Range"].ToString();
                    var range = rangeHeader.Replace("bytes=", "").Split('-');
                    long start = long.Parse(range[0]);
                    long end = range.Length > 1 && !string.IsNullOrWhiteSpace(range[1])
                        ? long.Parse(range[1])
                        : fileSize - 1;

                    var length = end - start + 1;

                    stream.Seek(start, SeekOrigin.Begin);
                    var partialStream = new MemoryStream();
                    await stream.CopyToAsync(partialStream);
                    partialStream.Position = 0;

                    Response.StatusCode = 206; // Partial Content
                    Response.Headers.Add("Accept-Ranges", "bytes");
                    Response.Headers.Add("Content-Range", $"bytes {start}-{end}/{fileSize}");
                    Response.ContentLength = length;

                    return File(partialStream, "application/octet-stream", enableRangeProcessing: true);
                }

                Response.Headers.Add("Accept-Ranges", "bytes");
                return File(stream, "application/octet-stream", fileName);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteFile([FromQuery] string relativePath)
        {
            await _nasService.DeleteFileAsync(relativePath);
            return Ok("File deleted successfully");
        }
    }
}
