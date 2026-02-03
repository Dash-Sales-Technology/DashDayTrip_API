using Microsoft.AspNetCore.Mvc;
using System.IO;

[Route("api/[controller]")]
[ApiController]
public class UploadsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> UploadImage(IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest("No file uploaded");

        // 1. Path where images will be stored
        var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "receipts");
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

        // 2. Give it a unique name
        var fileName = $"{Guid.NewGuid()}_{file.FileName}";
        var filePath = Path.Combine(folderPath, fileName);

        // 3. Save the binary data to your hard drive
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        // 4. Return the SHORT URL to the frontend
        // This is what will be saved in your "PaymentReceipt" column
        var url = $"/uploads/receipts/{fileName}";
        return Ok(new { url = url });
    }
}