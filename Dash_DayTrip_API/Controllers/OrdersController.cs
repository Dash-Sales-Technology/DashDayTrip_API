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
    public class UpdatePaymentStatusRequest
    {
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class BulkUpdatePaymentStatusRequest
    {
        public List<int> OrderIds { get; set; } = new();
        public string PaymentStatus { get; set; } = string.Empty;
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
                    .Include(o => o.Promotion)
                    .ToListAsync();

                return Ok(orders);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetOrders failed");
                return StatusCode(500, new { error = ex.Message, stack = ex.StackTrace });
            }
        }

        // GET: api/Orders/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderPackages)
                .Include(o => o.Promotion)
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
            order.CreatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            // EF will automatically handle the nested order.Promotion object 
            // and associate it with the newly created OrderId.
            if (order.Promotion != null)
            {
                order.Promotion.CreatedAt = DateTime.UtcNow;
                order.Promotion.UpdatedAt = DateTime.UtcNow;
            }
            _context.Orders.Add(order);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, order);
        }

        // POST: api/Orders/{id}/update
        [HttpPost("{id}/update")]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] Order order)
        {
            if (id != order.OrderId)
            {
                return BadRequest("ID mismatch");
            }

            var existingOrder = await _context.Orders
                .Include(o => o.OrderPackages)
                .Include(o => o.Promotion) // Essential for detection
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (existingOrder == null)
            {
                return NotFound();
            }

            // Update main order fields
            _context.Entry(existingOrder).CurrentValues.SetValues(order);
            existingOrder.UpdatedAt = DateTime.UtcNow;

            // --- PROMOTION UPDATE LOGIC ---
            if (order.Promotion != null)
            {
                if (existingOrder.Promotion != null)
                {
                    // Update existing promotion record
                    _context.Entry(existingOrder.Promotion).CurrentValues.SetValues(order.Promotion);
                    existingOrder.Promotion.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Create new promotion record for this order
                    order.Promotion.OrderId = id;
                    order.Promotion.CreatedAt = DateTime.UtcNow;
                    order.Promotion.UpdatedAt = DateTime.UtcNow;
                    existingOrder.Promotion = order.Promotion;
                }
            }
            // ----------------------------

            // Package update logic (Keep as is)
            if (order.OrderPackages != null)
            {
                existingOrder.OrderPackages ??= new List<OrderPackage>();
                var newPackageIds = order.OrderPackages.Select(p => p.OrderPackageId).ToList();

                var packagesToDelete = existingOrder.OrderPackages
                    .Where(p => !newPackageIds.Contains(p.OrderPackageId) && p.OrderPackageId != 0)
                    .ToList();

                foreach (var pkg in packagesToDelete)
                {
                    pkg.IsDeleted = true;
                    pkg.UpdatedAt = DateTime.UtcNow;
                }

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

            // Re-fetch everything to ensure frontend gets the latest saved data
            var updatedOrder = await _context.Orders
                .Include(o => o.OrderPackages)
                .Include(o => o.Promotion) // Crucial to include here too!
                .FirstOrDefaultAsync(o => o.OrderId == id);

            return Ok(updatedOrder);
        }


        // POST: api/Orders/{id}/delete
        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                return NotFound();
            }

            order.IsDeleted = true;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Order deleted", orderId = id });
        }

        // GET: api/Orders/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<OrderStatistics>> GetStatistics(
            [FromQuery] int? formId = null,
            [FromQuery] string? merchantId = null)
        {
            var query = _context.Orders.AsQueryable();

            if (formId.HasValue)
            {
                query = query.Where(o => o.FormId == formId.Value);
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
                TotalBalance = await query.SumAsync(o => o.BalanceDue),
                AverageOrderValue = await query.AnyAsync() ? await query.AverageAsync(o => o.GrandTotal) : 0,
                DailySales = await query.Where(o => o.CreatedAt.Date == today).SumAsync(o => o.GrandTotal),
                ConfirmedOrders = await query.CountAsync(o => o.Status == "confirmed"),
                PendingOrders = await query.CountAsync(o => o.Status == "pending"),
                CancelledOrders = await query.CountAsync(o => o.Status == "cancelled")
            };

            return stats;
        }

        // GET: api/Orders/form/{formId}
        [HttpGet("form/{formId}")]
        public async Task<ActionResult<IEnumerable<Order>>> GetOrdersByForm(int formId)
        {
            return await _context.Orders
                .Where(o => o.FormId == formId)
                .Include(o => o.OrderPackages)
                .Include(o => o.Promotion)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        // POST: api/Orders/{id}/status
        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
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
        public async Task<IActionResult> MarkInvoiceSent(int id)
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
        public async Task<IActionResult> ResetInvoiceStatus(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.InvoiceSentAt = null;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { OrderId = id, InvoiceSentAt = (DateTime?)null });
        }

        // POST: api/Orders/{id}/receipt
        [HttpPost("{id}/receipt")]
        public async Task<IActionResult> UploadReceipt(int id, IFormFile file)
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

        // POST: api/Orders/{id}/receipt/delete
        [HttpPost("{id}/receipt/delete")]
        public async Task<IActionResult> DeleteReceipt(int id)
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

        // GET: api/Orders/expiring-invoices?daysOld=3
        [HttpGet("expiring-invoices")]
        public async Task<ActionResult<IEnumerable<object>>> GetExpiringInvoices([FromQuery] int daysOld = 3)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

                var expiring = await _context.Orders
                    .Where(o => (o.PaymentStatus == "Pending" || o.PaymentStatus == "Partial")
                             && o.InvoiceSentAt != null
                             && o.InvoiceSentAt <= cutoffDate)
                    .OrderBy(o => o.InvoiceSentAt)
                    .Select(o => new
                    {
                        o.OrderId,
                        o.ReferenceNumber,
                        o.CustomerName,
                        o.Phone,
                        o.CountryCode,
                        o.Email,
                        o.InvoiceSentAt,
                        o.GrandTotal,
                        o.BalanceDue,
                        o.PaymentStatus
                    })
                    .ToListAsync();

                return Ok(expiring);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetExpiringInvoices failed");
                return StatusCode(500, new { error = ex.Message });
            }
        }

        // POST: api/Orders/{id}/payment-status
        [HttpPost("{id}/payment-status")]
        public async Task<IActionResult> UpdatePaymentStatus(int id, [FromBody] UpdatePaymentStatusRequest request)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.PaymentStatus = request.PaymentStatus;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { OrderId = id, NewPaymentStatus = request.PaymentStatus });
        }

        // POST: api/Orders/bulk-payment-status
        [HttpPost("bulk-payment-status")]
        public async Task<ActionResult<OrderPaymentStatusUpdateResponse>> BulkUpdatePaymentStatus([FromBody] BulkUpdatePaymentStatusRequest request)
        {
            if (request.OrderIds == null || !request.OrderIds.Any())
            {
                return BadRequest(new ApiResponse { Success = false, Message = "No order IDs provided" });
            }

            var orders = await _context.Orders
                .Where(o => request.OrderIds.Contains(o.OrderId))
                .ToListAsync();

            foreach (var order in orders)
            {
                order.PaymentStatus = request.PaymentStatus;
                order.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            return Ok(new OrderPaymentStatusUpdateResponse
            {
                Success = true,
                Message = $"Successfully updated {orders.Count} orders",
                OrderIds = orders.Select(o => o.OrderId).ToList(),
                NewPaymentStatus = request.PaymentStatus,
                UpdatedAt = DateTime.UtcNow
            });
        }


        private bool OrderExists(int id)
        {
            return _context.Orders.Any(o => o.OrderId == id);
        }
    }
}
