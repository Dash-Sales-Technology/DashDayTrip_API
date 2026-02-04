using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.IO;

[Route("api/[controller]")]
[ApiController]
[EnableCors("AllowAll")]
public class UploadsController : ControllerBase
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(IWebHostEnvironment env, ILogger<UploadsController> logger)
    {
        _env = env;
        _logger = logger;
    }

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
    {
        try
        {
            _logger.LogInformation("Upload started. WebRootPath: {WebRoot}, ContentRootPath: {ContentRoot}", 
                _env.WebRootPath, _env.ContentRootPath);

            if (file == null || file.Length == 0)
                return BadRequest("No file uploaded");

            // Try WebRootPath first, fall back to ContentRootPath/wwwroot
            var basePath = _env.WebRootPath;
            if (string.IsNullOrEmpty(basePath))
            {
                basePath = Path.Combine(_env.ContentRootPath, "wwwroot");
                _logger.LogWarning("WebRootPath was null, using: {BasePath}", basePath);
            }

            var folderPath = Path.Combine(basePath, "uploads", "receipts");
            _logger.LogInformation("Target folder: {FolderPath}", folderPath);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                _logger.LogInformation("Created directory: {FolderPath}", folderPath);
            }

            var fileName = $"{Guid.NewGuid()}{Path.GetExtension(file.FileName)}";
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File saved successfully: {FilePath}", filePath);

            var url = $"/uploads/receipts/{fileName}";
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}