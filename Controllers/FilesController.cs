using Microsoft.AspNetCore.Mvc;
using NasBridgeApi.Models;
using NasBridgeApi.Services;

namespace NasBridgeApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FilesController : ControllerBase
    {
        private readonly NasService _nasService;

        public FilesController(NasService nasService)
        {
            _nasService = nasService;
        }

        /// <summary>
        /// List files in the specified NAS folder.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ListFiles([FromQuery] string relativePath = "")
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

        /// <summary>
        /// Upload a file to the NAS.
        /// </summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadFile([FromForm] FileUploadRequest request)
        {
            if (request.File == null || request.File.Length == 0)
                return BadRequest("File is missing");

            var fullRelativePath = Path.Combine(request.RelativePath ?? "", request.File.FileName)
                .Replace("\\", "/");

            using var stream = request.File.OpenReadStream();
            await _nasService.UploadFileAsync(fullRelativePath, stream, request.Overwrite);

            return Ok("File uploaded successfully");
        }


        /// <summary>
        /// Check if file exists.
        /// </summary>
        [HttpGet("exists")]
        public async Task<IActionResult> FileExists([FromQuery] string relativePath)
        {
            try
            {
                bool exists = await _nasService.FileExistsAsync(relativePath);
                return Ok(new { exists });
            }
            catch (Exception ex)
            {
                return StatusCode(500, ex.Message);
            }
        }

        /// <summary>
        /// Download a file from the NAS (supports resumable).
        /// </summary>
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string relativePath)
        {
            try
            {
                var stream = await _nasService.DownloadFileAsync(relativePath);
                var fileName = Path.GetFileName(relativePath);
                var fileSize = stream.Length;

                // Handle HTTP Range headers
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
                    Response.Headers.Append("Accept-Ranges", "bytes");
                    Response.Headers.Append("Content-Range", $"bytes {start}-{end}/{fileSize}");
                    Response.ContentLength = length;

                    return File(partialStream, "application/octet-stream", enableRangeProcessing: true);
                }

                Response.Headers.Append("Accept-Ranges", "bytes");
                return File(stream, "application/octet-stream", fileName);
            }
            catch (FileNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Delete a file from the NAS.
        /// </summary>
        [HttpDelete]
        public async Task<IActionResult> DeleteFile([FromQuery] string relativePath)
        {
            await _nasService.DeleteFileAsync(relativePath);
            return Ok("File deleted successfully");
        }
    }
}
