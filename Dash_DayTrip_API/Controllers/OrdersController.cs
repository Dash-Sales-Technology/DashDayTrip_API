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

    public class ApplyPaymentRequest
    {
        public decimal AmountPaidNow { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionRef { get; set; }
    }

    public class VoidOrderPaymentRequest
    {
        public string? VoidedBy { get; set; }
        public string? VoidReason { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class OrdersController : ControllerBase
    {
        private readonly ApiContext _context;
        private readonly ILogger<OrdersController> _logger;
        private readonly IConfiguration _configuration;

        private static readonly HashSet<string> AllowedOrderStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "pending", "confirmed", "cancelled" };

        private const string SourceOrder = "order";
        private const string SourceQuickBooking = "quick_booking";

        private const string PaymentPending = "Pending";
        private const string PaymentPartial = "Partial";
        private const string PaymentPaid = "Paid";

        public OrdersController(ApiContext context, ILogger<OrdersController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        private static string NormalizeSource(string? source)
        {
            var normalized = (source ?? SourceOrder).Trim().ToLowerInvariant();
            return normalized switch
            {
                SourceOrder => SourceOrder,
                SourceQuickBooking => SourceQuickBooking,
                _ => string.Empty
            };
        }

        private static bool IsQuickBooking(string? source) =>
            string.Equals(source?.Trim(), SourceQuickBooking, StringComparison.OrdinalIgnoreCase);

        private static string NormalizeOrderStatus(string? status)
        {
            var normalized = (status ?? "pending").Trim().ToLowerInvariant();
            return AllowedOrderStatuses.Contains(normalized) ? normalized : string.Empty;
        }

        private static string ComputePaymentStatus(decimal grandTotal, decimal amountPaid)
        {
            if (amountPaid >= grandTotal && grandTotal > 0) return PaymentPaid;
            if (amountPaid > 0) return PaymentPartial;
            return PaymentPending;
        }

        private static decimal ComputeBalanceDue(decimal grandTotal, decimal amountPaid) =>
            Math.Max(0m, grandTotal - amountPaid);

        private async Task<object?> RecalculateOrderPaymentSummaryAsync(int orderId)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order == null) return null;

            var totalPaid = await _context.Set<OrderPayment>()
                .Where(p => p.OrderId == orderId && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            order.AmountPaid = totalPaid;
            order.BalanceDue = ComputeBalanceDue(order.GrandTotal, totalPaid);
            order.PaymentStatus = ComputePaymentStatus(order.GrandTotal, totalPaid);

            var latestPayment = await _context.Set<OrderPayment>()
                .Where(p => p.OrderId == orderId && !p.IsVoided)
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.OrderPaymentId)
                .FirstOrDefaultAsync();

            if (latestPayment != null)
            {
                order.PaymentMethod = string.IsNullOrWhiteSpace(latestPayment.PaymentMethod)
                    ? null
                    : latestPayment.PaymentMethod;

                order.TransactionRef = string.IsNullOrWhiteSpace(latestPayment.TransactionRef)
                    ? null
                    : latestPayment.TransactionRef;
            }
            else
            {
                order.PaymentMethod = null;
                order.TransactionRef = null;
            }

            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return new
            {
                orderId = order.OrderId,
                grandTotal = order.GrandTotal,
                amountPaid = order.AmountPaid,
                balanceDue = order.BalanceDue,
                paymentStatus = order.PaymentStatus,
                paymentMethod = order.PaymentMethod,
                transactionRef = order.TransactionRef
            };
        }


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
                return StatusCode(500, new { error = ex.Message });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Order>> GetOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.OrderPackages)
                .Include(o => o.Promotion)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (order == null) return NotFound();
            return order;
        }

        [HttpPost]
        public async Task<ActionResult<Order>> CreateOrder([FromBody] Order order)
        {
            var source = NormalizeSource(order.Source);
            if (string.IsNullOrEmpty(source))
                return BadRequest(new { message = "Invalid source. Allowed: order, quick_booking." });

            var status = NormalizeOrderStatus(order.Status);
            if (string.IsNullOrEmpty(status))
                return BadRequest(new { message = "Invalid order status. Allowed: pending, confirmed, cancelled." });

            if (order.GrandTotal < 0 || order.DepositPaid < 0 || order.AmountPaid < 0)
                return BadRequest(new { message = "GrandTotal, DepositPaid and AmountPaid cannot be negative." });

            if (order.AmountPaid > order.GrandTotal)
                return BadRequest(new { message = "AmountPaid cannot exceed GrandTotal." });

            if (IsQuickBooking(source))
            {
                order.DepositPaid = 0m;
                if (order.Promotion != null)
                    return BadRequest(new { message = "Promotions are not allowed for quick_booking orders." });
            }

            order.Source = source;
            order.Status = status;
            order.PaymentStatus = ComputePaymentStatus(order.GrandTotal, order.AmountPaid);
            order.BalanceDue = ComputeBalanceDue(order.GrandTotal, order.AmountPaid);
            order.CreatedAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            order.IsDeleted = false;

            if (order.Promotion != null)
            {
                order.Promotion.CreatedAt = DateTime.UtcNow;
                order.Promotion.UpdatedAt = DateTime.UtcNow;
                order.Promotion.IsDeleted = false;
            }

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrder), new { id = order.OrderId }, order);
        }

        [HttpPost("{id}/update")]
        public async Task<IActionResult> UpdateOrder(int id, [FromBody] Order order)
        {
            if (id != order.OrderId) return BadRequest("ID mismatch");

            var existingOrder = await _context.Orders
                .Include(o => o.OrderPackages)
                .Include(o => o.Promotion)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            if (existingOrder == null) return NotFound();

            var incomingSource = NormalizeSource(order.Source);
            if (string.IsNullOrEmpty(incomingSource))
                return BadRequest(new { message = "Invalid source. Allowed: order, quick_booking." });

            var existingSource = NormalizeSource(existingOrder.Source);
            if (!string.Equals(existingSource, incomingSource, StringComparison.Ordinal))
                return BadRequest(new { message = "Source mutation is not allowed." });

            var incomingStatus = NormalizeOrderStatus(order.Status);
            if (string.IsNullOrEmpty(incomingStatus))
                return BadRequest(new { message = "Invalid order status. Allowed: pending, confirmed, cancelled." });

            if (order.GrandTotal < 0 || order.DepositPaid < 0)
                return BadRequest(new { message = "GrandTotal and DepositPaid cannot be negative." });

            if (existingOrder.AmountPaid > order.GrandTotal)
                return BadRequest(new { message = "GrandTotal cannot be lower than persisted paid amount." });

            if (IsQuickBooking(existingSource))
            {
                order.DepositPaid = 0m;
                if (order.Promotion != null || existingOrder.Promotion != null)
                    return BadRequest(new { message = "Promotions are not allowed for quick_booking orders." });
            }

            var existingAmountPaid = existingOrder.AmountPaid;
            var existingPaymentMethod = existingOrder.PaymentMethod;
            var existingTransactionRef = existingOrder.TransactionRef;

            _context.Entry(existingOrder).CurrentValues.SetValues(order);

            existingOrder.Source = existingSource;
            existingOrder.Status = incomingStatus;

            existingOrder.AmountPaid = existingAmountPaid;
            existingOrder.PaymentMethod = existingPaymentMethod;
            existingOrder.TransactionRef = existingTransactionRef;
            existingOrder.BalanceDue = ComputeBalanceDue(existingOrder.GrandTotal, existingOrder.AmountPaid);
            existingOrder.PaymentStatus = ComputePaymentStatus(existingOrder.GrandTotal, existingOrder.AmountPaid);
            existingOrder.UpdatedAt = DateTime.UtcNow;

            if (order.OrderPackages != null)
            {
                existingOrder.OrderPackages ??= new List<OrderPackage>();
                var incomingIds = order.OrderPackages.Select(p => p.OrderPackageId).ToList();

                var toDelete = existingOrder.OrderPackages
                    .Where(p => !incomingIds.Contains(p.OrderPackageId) && p.OrderPackageId != 0)
                    .ToList();

                foreach (var pkg in toDelete)
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
                        package.UpdatedAt = DateTime.UtcNow;
                        existingOrder.OrderPackages.Add(package);
                    }
                }
            }

            await _context.SaveChangesAsync();

            var updatedOrder = await _context.Orders
                .Include(o => o.OrderPackages)
                .Include(o => o.Promotion)
                .FirstOrDefaultAsync(o => o.OrderId == id);

            return Ok(updatedOrder);
        }

        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.IsDeleted = true;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Order deleted", orderId = id });
        }

        [HttpGet("statistics")]
        public async Task<ActionResult<OrderStatistics>> GetStatistics(
            [FromQuery] int? formId = null,
            [FromQuery] string? merchantId = null)
        {
            var query = _context.Orders.AsQueryable();

            if (formId.HasValue)
                query = query.Where(o => o.FormId == formId.Value);

            if (!string.IsNullOrEmpty(merchantId))
                query = query.Where(o => o.MerchantId == merchantId);

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

        [HttpPost("{id}/status")]
        public async Task<IActionResult> UpdateOrderStatus(int id, [FromBody] UpdateOrderStatusRequest request)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            var normalized = NormalizeOrderStatus(request.Status);
            if (string.IsNullOrEmpty(normalized))
                return BadRequest(new { message = "Invalid order status. Allowed: pending, confirmed, cancelled." });

            order.Status = normalized;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { OrderId = id, NewStatus = normalized });
        }

        [HttpPost("{id}/invoice-sent")]
        public async Task<IActionResult> MarkInvoiceSent(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            if (!string.Equals(order.PaymentStatus, PaymentPaid, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Invoice can only be marked as sent for Paid orders." });

            order.InvoiceSentAt = DateTime.UtcNow;
            order.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { OrderId = id, InvoiceSentAt = order.InvoiceSentAt });
        }

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

        [HttpPost("{id}/receipt")]
        public async Task<IActionResult> UploadReceipt(int id, IFormFile file)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Order not found" });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "No file provided" });

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".pdf" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return BadRequest(new { message = "Invalid file type. Allowed: jpg, jpeg, png, gif, pdf" });

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
                        System.IO.File.Delete(oldFilePath);
                }

                using (var stream = new FileStream(fullPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                order.PaymentReceipt = $"{baseUrl}/Image/Receipt/{fileName}";
                order.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogInformation("Receipt uploaded for order {OrderId}: {Url}", id, order.PaymentReceipt);

                return Ok(new
                {
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

        [HttpPost("{id}/receipt/delete")]
        public async Task<IActionResult> DeleteReceipt(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound(new { message = "Order not found" });

            if (string.IsNullOrEmpty(order.PaymentReceipt))
                return NotFound(new { message = "No receipt found for this order" });

            try
            {
                var basePath = _configuration["ImageStorage:BasePath"] ?? @"C:\inetpub\wwwroot\DayTripImages";
                var fileName = Path.GetFileName(new Uri(order.PaymentReceipt).LocalPath);
                var filePath = Path.Combine(basePath, "Image", "Receipt", fileName);

                if (System.IO.File.Exists(filePath))
                    System.IO.File.Delete(filePath);

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

        [HttpGet("expiring-invoices")]
        public async Task<ActionResult<IEnumerable<object>>> GetExpiringInvoices([FromQuery] int daysOld = 3)
        {
            try
            {
                var cutoffDate = DateTime.UtcNow.AddDays(-daysOld);

                var expiring = await _context.Orders
                    .Where(o => (o.PaymentStatus == PaymentPending || o.PaymentStatus == PaymentPartial)
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

        [HttpPost("{id}/payment-status")]
        public async Task<IActionResult> UpdatePaymentStatus(int id, [FromBody] UpdatePaymentStatusRequest request)
        {
            // Disabled to avoid bypassing ledger-based payment status computation.
            return BadRequest(new
            {
                message = "Manual payment status updates are disabled. Use /api/Orders/{id}/payment or payment void actions."
            });
        }

        [HttpPost("{id}/payment")]
        public async Task<IActionResult> ApplyPayment(int id, [FromBody] ApplyPaymentRequest request)
        {
            if (request.AmountPaidNow <= 0)
                return BadRequest(new { message = "Payment amount must be greater than zero." });

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id);
                if (order == null)
                    return NotFound(new { message = "Order not found." });

                var paidBefore = await _context.Set<OrderPayment>()
                    .Where(p => p.OrderId == id && !p.IsVoided)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;

                var outstanding = Math.Max(0m, order.GrandTotal - paidBefore);
                if (request.AmountPaidNow > outstanding)
                    return BadRequest(new { message = "Payment exceeds outstanding balance." });

                var payment = new OrderPayment
                {
                    OrderId = id,
                    Amount = request.AmountPaidNow,
                    PaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod) ? null : request.PaymentMethod,
                    TransactionRef = string.IsNullOrWhiteSpace(request.TransactionRef) ? null : request.TransactionRef,
                    PaymentDate = DateTime.UtcNow,
                    CreatedAt = DateTime.UtcNow,
                    IsVoided = false
                };

                _context.Set<OrderPayment>().Add(payment);
                await _context.SaveChangesAsync();

                var summary = await RecalculateOrderPaymentSummaryAsync(id);
                if (summary == null)
                    return NotFound(new { message = "Order not found after payment recalculation." });

                await tx.CommitAsync();

                return Ok(new
                {
                    message = "Payment applied successfully.",
                    paymentId = payment.OrderPaymentId,
                    orderSummary = summary
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "ApplyPayment failed for order {OrderId}", id);
                return StatusCode(500, new { message = "Failed to apply payment.", error = ex.Message });
            }
        }

        [HttpPost("bulk-payment-status")]
        public async Task<ActionResult<OrderPaymentStatusUpdateResponse>> BulkUpdatePaymentStatus([FromBody] BulkUpdatePaymentStatusRequest request)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Message = "Manual bulk payment status updates are disabled. Status is ledger-derived."
            });
        }

        [HttpGet("{id}/payments")]
        public async Task<IActionResult> GetOrderPayments(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            var payments = await _context.Set<OrderPayment>()
                .Where(p => p.OrderId == id)
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.OrderPaymentId)
                .ToListAsync();

            return Ok(new
            {
                orderId = id,
                grandTotal = order.GrandTotal,
                amountPaid = order.AmountPaid,
                balanceDue = order.BalanceDue,
                paymentStatus = order.PaymentStatus,
                payments
            });
        }

        [HttpPost("payments/{paymentId}/void")]
        public async Task<IActionResult> VoidOrderPayment(int paymentId, [FromBody] VoidOrderPaymentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.VoidReason))
                return BadRequest(new { message = "VoidReason is required." });

            await using var tx = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

            try
            {
                var payment = await _context.Set<OrderPayment>()
                    .FirstOrDefaultAsync(p => p.OrderPaymentId == paymentId);

                if (payment == null)
                    return NotFound(new { message = "Payment not found." });

                if (payment.IsVoided)
                    return BadRequest(new { message = "Payment is already voided." });

                payment.IsVoided = true;
                payment.VoidedAt = DateTime.UtcNow;
                payment.VoidedBy = request?.VoidedBy;
                payment.VoidReason = request?.VoidReason;

                await _context.SaveChangesAsync();

                var summary = await RecalculateOrderPaymentSummaryAsync(payment.OrderId);
                if (summary == null)
                    return NotFound(new { message = "Order not found after void recalculation." });

                await tx.CommitAsync();

                return Ok(new
                {
                    message = "Payment voided successfully.",
                    paymentId = payment.OrderPaymentId,
                    orderSummary = summary
                });
            }
            catch (Exception ex)
            {
                await tx.RollbackAsync();
                _logger.LogError(ex, "VoidOrderPayment failed for payment {PaymentId}", paymentId);
                return StatusCode(500, new { message = "Failed to void payment.", error = ex.Message });
            }
        }

        private bool OrderExists(int id)
        {
            return _context.Orders.Any(o => o.OrderId == id);
        }
    }
}