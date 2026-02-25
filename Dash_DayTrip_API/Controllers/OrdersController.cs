using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Models.Responses;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    public class UpdateOrderStatusRequest
    {
        public string Status { get; set; } = string.Empty;
    }

    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly ApiContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly IConfiguration _configuration;

        public OrdersController(ApiContext context, ILogger<OrdersController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        // GET: api/Orders
        [HttpGet]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrders()
        {
            try
            {
                var orders = await _context.Orders
                    .Include(o => o.OrderPackages)
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                // Log the exception and return details for debugging (remove stack in production)
                _logger.LogError(ex, "GetOrders failed");
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        // GET: api/Orders/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(string id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderPackages)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null)
            {
                return NotFound();
            }

            return order;
        }

        // POST: api/Orders
        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] Order order)
        {
            order.OrderId = Guid.NewGuid().ToString();
            order.CreatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, order);
        }

        // POST: api/Orders/{id}/update
        [HttpPost("{id}/update")]
        public async Task<IActionResult> UpdateOrder(string id, [FromBody] Order order)
        {
            if (id != order.OrderId)
            {
                return BadRequest("ID mismatch");
            }

            var existingOrder = await _context.Orders
                .Include(o => o.OrderPackages)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (existingOrder == null)
            {
                return NotFound();
            }

            _context.Entry(existingOrder).CurrentValues.SetValues(order);
            existingOrder.UpdatedAt = DateTime.UtcNow;

            if (order.OrderPackages != null)
            {
                existingOrder.OrderPackages ??= new List<OrderPackage>();
                var newPackageIds = order.OrderPackages.Select(p => p.OrderPackageId).ToList();

                // Soft-delete packages that were removed in the incoming list
                var packagesToDelete = existingOrder.OrderPackages
                    .Where(p => !newPackageIds.Contains(p.OrderPackageId) && p.OrderPackageId != 0)
                    .ToList();
                if (packagesToDelete.Any())
                {
                    foreach (var pkg in packagesToDelete)
                    {
                        pkg.IsDeleted = true;
                        pkg.UpdatedAt = DateTime.UtcNow;
                    }
                }

                // Add or update packages
                foreach (var package in order.OrderPackages)
                {
                    var existingPackage = existingOrder.OrderPackages
                        .FirstOrDefault(p => p.OrderPackageId == package.OrderPackageId && p.OrderPackageId != 0);

                    if (existingPackage != null)
                    {
                        _context.Entry(existingPackage).CurrentValues.SetValues(package);
                        existingPackage.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        package.OrderPackageId = 0;
                        package.IsDeleted = false;
                        package.CreatedAt = DateTime.UtcNow;
                        existingOrder.OrderPackages.Add(package);
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Return updated order
            var updatedOrder = await _context.Orders
                .Include(o => o.OrderPackages)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            return Ok(updatedOrder);
        }

        // POST: api/Orders/{id}/delete
        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteOrder(string id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            // Soft delete - set flag instead of removing the row
            order.IsDeleted = true;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Order deleted", orderId = id });
        }

        // GET: api/Orders/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<OrderStatistics>> GetStatistics(
            [FromQuery] string? formId = null,
            [FromQuery] string? merchantId = null)
        {
            var query = _context.Orders.AsQueryable();

            if (!string.IsNullOrEmpty(formId))
            {
                query = query.Where(o => o.FormId == formId);
            }

            if (!string.IsNullOrEmpty(merchantId))
            {
                query = query.Where(o => o.MerchantId == merchantId);
            }

            var today = DateTime.Today;

            var stats = new OrderStatistics
            {
                TotalOrders = await query.CountAsync(),
                TotalRevenue = await query.SumAsync(o => o.GrandTotal),
                TotalDeposits = await query.SumAsync(o => o.DepositPaid),
                OutstandingBalance = await query.SumAsync(o => o.BalanceDue),
                TodayOrders = await query.CountAsync(o => o.CreatedAt.Date == today),
                TodayRevenue = await query.Where(o => o.CreatedAt.Date == today).SumAsync(o => o.GrandTotal),
                PendingCount = await query.CountAsync(o => o.Status == "pending"),
                ConfirmedCount = await query.CountAsync(o => o.Status == "confirmed"),
                CompletedCount = await query.CountAsync(o => o.Status == "completed"),
                CancelledCount = await query.CountAsync(o => o.Status == "cancelled")
            };

            return stats;
        }

        // GET: api/Orders/form/{formId}
        [HttpGet("form/{formId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByForm(string formId)
        {
            return await _context.Orders
                .Where(o => o.FormId == formId)
                .Include(o => o.OrderPackages)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        // POST: api/Orders/{id}/status
        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(string id, [FromBody] UpdateOrderStatusRequest request)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            order.Status = request.Status;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { OrderId = id, NewStatus = request.Status });
        }

        // POST: api/Orders/{id}/invoice-sent
        [HttpPost("{id}/invoice-sent")]
        public async Task<IActionResult> MarkInvoiceSent(string id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            order.InvoiceSentAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { OrderId = id, InvoiceSentAt = order.InvoiceSentAt });
        }

        // POST: api/Orders/{id}/invoice-reset
        [HttpPost("{id}/invoice-reset")]
        public async Task<IActionResult> ResetInvoiceStatus(string id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.InvoiceSentAt = null; // Clear the timestamp
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { OrderId = id, InvoiceSentAt = (DateTime?)null });
        }

        // POST: api/Orders/{id}/receipt - Upload receipt image
        [HttpPost("{id}/receipt")]
        public async Task<IActionResult> UploadReceipt(string id, IFormFile file)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file provided" });
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
            {
                return BadRequest(new { message = "Invalid file type. Allowed: jpg, jpeg, png, gif, pdf" });
            }

            try
            {
                var basePath = _configuration["ImageStorage:BasePath"] ?? @"C:\inetpub\wwwroot\DayTripImages";
                var baseUrl = _configuration["ImageStorage:BaseUrl"] ?? "http://localhost:8081";
                var receiptFolder = Path.Combine(basePath, "Image", "Receipt");
                Directory.CreateDirectory(receiptFolder);

                var fileName = $"{id}_{DateTime.UtcNow:yyyyMMddHHmmss}{extension}";
                var fullPath = Path.Combine(receiptFolder, fileName);

                if (!string.IsNullOrEmpty(order.PaymentReceipt))
                {
                    var oldFileName = Path.GetFileName(new Uri(order.PaymentReceipt).LocalPath);
                    var oldFilePath = Path.Combine(receiptFolder, oldFileName);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                order.PaymentReceipt = $"{baseUrl}/Image/Receipt/{fileName}";
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Receipt uploaded for order {OrderId}: {Url}", id, order.PaymentReceipt);

                return Ok(new { 
                    message = "Receipt uploaded successfully",
                    imageUrl = order.PaymentReceipt 
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading receipt for order {OrderId}", id);
                return StatusCode(500, new { message = "Error uploading file", error = ex.Message });
            }
        }

        // POST: api/Orders/{id}/receipt/delete - Delete receipt image
        [HttpPost("{id}/receipt/delete")]
        public async Task<IActionResult> DeleteReceipt(string id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound(new { message = "Order not found" });
            }

            if (string.IsNullOrEmpty(order.PaymentReceipt))
            {
                return NotFound(new { message = "No receipt found for this order" });
            }

            try
            {
                var basePath = _configuration["ImageStorage:BasePath"] ?? @"C:\inetpub\wwwroot\DayTripImages";
                var fileName = Path.GetFileName(new Uri(order.PaymentReceipt).LocalPath);
                var filePath = Path.Combine(basePath, "Image", "Receipt", fileName);

                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }

                order.PaymentReceipt = null;
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return Ok(new { message = "Receipt deleted successfully" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting receipt for order {OrderId}", id);
                return StatusCode(500, new { message = "Error deleting file", error = ex.Message });
            }
        }

        private bool OrderExists(string id)
        {
            return _context.Orders.Any(o => o.OrderId == id);
        }
    }
}
