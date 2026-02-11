using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;
using System.IO;

[Route("api/[controller]")]
[ApiController]
[EnableCors("AllowAll")]
public class UploadsController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<UploadsController> _logger;

    public UploadsController(IConfiguration configuration, ILogger<UploadsController> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    [HttpPost]
    [DisableRequestSizeLimit]
    [RequestFormLimits(MultipartBodyLengthLimit = 104857600)]
    public async Task<IActionResult> UploadImage([FromForm] IFormFile file)
    {
        try
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { error = "No file uploaded" });

            // Validate file type
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { error = "Invalid file type. Allowed: jpg, jpeg, png, gif, pdf" });
            }

            // Get paths from configuration
            var basePath = _configuration["ImageStorage:BasePath"] ?? @"C:\inetpub\wwwroot\DayTripImages";
            var baseUrl = _configuration["ImageStorage:BaseUrl"] ?? "http://localhost:8081";

            var folderPath = Path.Combine(basePath, "Image", "Receipt");
            _logger.LogInformation("Target folder: {FolderPath}", folderPath);

            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
                _logger.LogInformation("Created directory: {FolderPath}", folderPath);
            }

            var fileName = $"{Guid.NewGuid()}{extension}";
            var filePath = Path.Combine(folderPath, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            _logger.LogInformation("File saved successfully: {FilePath}", filePath);

            // Return full URL accessible via IIS image site
            var url = $"{baseUrl}/Image/Receipt/{fileName}";
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload failed: {Message}", ex.Message);
            return StatusCode(500, new { error = ex.Message, stackTrace = ex.StackTrace });
        }
    }
}