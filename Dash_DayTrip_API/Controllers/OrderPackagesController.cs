using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    public class CreateOrderPackageRequest
    {
        public int OrderId { get; set; }
        public int PackageId { get; set; }
        public string PackageName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public int NoOfPax { get; set; }
        public bool BoatFareEnabled { get; set; }
        public decimal? BoatFareAmount { get; set; }
        public string? BoatFareCalcType { get; set; }
        public bool GratuityEnabled { get; set; }
        public decimal? GratuityAmount { get; set; }
        public string? GratuityCalcType { get; set; }
        public decimal LineTotal { get; set; }
    }

    public class UpdateOrderPackageRequest
    {
        public string? PackageName { get; set; }
        public int? Quantity { get; set; }
        public decimal? UnitPrice { get; set; }
        public int? NoOfPax { get; set; }
        public bool? BoatFareEnabled { get; set; }
        public decimal? BoatFareAmount { get; set; }
        public string? BoatFareCalcType { get; set; }
        public bool? GratuityEnabled { get; set; }
        public decimal? GratuityAmount { get; set; }
        public string? GratuityCalcType { get; set; }
        public decimal? LineTotal { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class OrderPackagesController : ControllerBase
    {
        private readonly ApiContext _context;
        private readonly ILogger<OrderPackagesController> _logger;

        private const string SourceQuickBooking = "quick_booking";

        public OrderPackagesController(ApiContext context, ILogger<OrderPackagesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static bool IsQuickBooking(string? source) =>
            string.Equals(source?.Trim(), SourceQuickBooking, StringComparison.OrdinalIgnoreCase);

        private static string ComputePaymentStatus(decimal grandTotal, decimal amountPaid)
        {
            if (amountPaid >= grandTotal && grandTotal > 0) return "Paid";
            if (amountPaid > 0) return "Partial";
            return "Pending";
        }

        private static decimal SafeMoney(decimal? value) => value.HasValue && value.Value > 0 ? value.Value : 0m;

        private static string? ValidateCreatePayload(CreateOrderPackageRequest request)
        {
            if (request.OrderId <= 0) return "OrderId must be greater than zero.";
            if (request.PackageId <= 0) return "PackageId must be greater than zero.";
            if (request.Quantity <= 0) return "Quantity must be greater than zero.";
            if (request.UnitPrice < 0) return "UnitPrice cannot be negative.";
            if (request.NoOfPax < 0) return "NoOfPax cannot be negative.";
            if (request.BoatFareAmount.HasValue && request.BoatFareAmount.Value < 0) return "BoatFareAmount cannot be negative.";
            if (request.GratuityAmount.HasValue && request.GratuityAmount.Value < 0) return "GratuityAmount cannot be negative.";
            return null;
        }

        private static string? ValidateUpdatePayload(UpdateOrderPackageRequest request)
        {
            if (request.Quantity.HasValue && request.Quantity.Value <= 0) return "Quantity must be greater than zero.";
            if (request.UnitPrice.HasValue && request.UnitPrice.Value < 0) return "UnitPrice cannot be negative.";
            if (request.NoOfPax.HasValue && request.NoOfPax.Value < 0) return "NoOfPax cannot be negative.";
            if (request.BoatFareAmount.HasValue && request.BoatFareAmount.Value < 0) return "BoatFareAmount cannot be negative.";
            if (request.GratuityAmount.HasValue && request.GratuityAmount.Value < 0) return "GratuityAmount cannot be negative.";
            return null;
        }

        private async Task RecalculateOrderTotalsAsync(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return;

            var packages = await _context.OrderPackages
                .Where(op => op.OrderId == orderId && !op.IsDeleted)
                .ToListAsync();

            var subtotal = packages.Sum(p => p.LineTotal);
            var totalBoatFare = packages.Sum(p => p.BoatFareEnabled ? SafeMoney(p.BoatFareAmount) : 0m);
            var totalGratuity = packages.Sum(p => p.GratuityEnabled ? SafeMoney(p.GratuityAmount) : 0m);
            var grandTotal = subtotal + totalBoatFare + totalGratuity;

            order.Subtotal = subtotal;
            order.TotalBoatFare = totalBoatFare;
            order.TotalGratuity = totalGratuity;
            order.GrandTotal = grandTotal;

            if (IsQuickBooking(order.Source))
            {
                order.DepositPaid = 0m;
            }

            order.BalanceDue = Math.Max(0m, order.GrandTotal - order.AmountPaid);
            order.PaymentStatus = ComputePaymentStatus(order.GrandTotal, order.AmountPaid);
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderPackage>>> GetOrderPackages()
        {
            var orderPackages = await _context.OrderPackages
                .Where(op => !op.IsDeleted)
                .ToListAsync();

            return Ok(orderPackages);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<OrderPackage>> GetOrderPackage(int id)
        {
            var orderPackage = await _context.OrderPackages
                .FirstOrDefaultAsync(op => op.OrderPackageId == id && !op.IsDeleted);

            if (orderPackage == null)
            {
                return NotFound(new { message = "Order package not found." });
            }

            return Ok(orderPackage);
        }

        [HttpGet("ByOrder/{orderId}")]
        public async Task<ActionResult<IEnumerable<OrderPackage>>> GetOrderPackagesByOrder(int orderId)
        {
            var orderPackages = await _context.OrderPackages
                .Where(op => op.OrderId == orderId && !op.IsDeleted)
                .ToListAsync();

            return Ok(orderPackages);
        }

        [HttpPost]
        public async Task<ActionResult<OrderPackage>> CreateOrderPackage([FromBody] CreateOrderPackageRequest request)
        {
            var validationError = ValidateCreatePayload(request);
            if (!string.IsNullOrEmpty(validationError))
                return BadRequest(new { message = validationError });

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == request.OrderId);
            if (order == null)
                return BadRequest(new { message = "Invalid Order ID." });

            var quantity = request.Quantity;
            var unitPrice = request.UnitPrice;
            var boatFareEnabled = request.BoatFareEnabled;
            var gratuityEnabled = request.GratuityEnabled;
            var boatFareAmount = boatFareEnabled ? SafeMoney(request.BoatFareAmount) : 0m;
            var gratuityAmount = gratuityEnabled ? SafeMoney(request.GratuityAmount) : 0m;

            var orderPackage = new OrderPackage
            {
                OrderId = request.OrderId,
                PackageId = request.PackageId,
                PackageName = request.PackageName,
                Quantity = quantity,
                UnitPrice = unitPrice,
                NoOfPax = request.NoOfPax,
                BoatFareEnabled = boatFareEnabled,
                BoatFareAmount = boatFareAmount,
                BoatFareCalcType = request.BoatFareCalcType,
                GratuityEnabled = gratuityEnabled,
                GratuityAmount = gratuityAmount,
                GratuityCalcType = request.GratuityCalcType,
                LineTotal = quantity * unitPrice,
                IsDeleted = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.OrderPackages.Add(orderPackage);
            await _context.SaveChangesAsync();

            await RecalculateOrderTotalsAsync(request.OrderId);

            return CreatedAtAction(nameof(GetOrderPackage), new { id = orderPackage.OrderPackageId }, orderPackage);
        }

        [HttpPost("{id}/update")]
        public async Task<IActionResult> UpdateOrderPackagePost(int id, [FromBody] UpdateOrderPackageRequest request)
        {
            var validationError = ValidateUpdatePayload(request);
            if (!string.IsNullOrEmpty(validationError))
                return BadRequest(new { message = validationError });

            var orderPackage = await _context.OrderPackages
                .FirstOrDefaultAsync(op => op.OrderPackageId == id && !op.IsDeleted);

            if (orderPackage == null)
            {
                return NotFound(new { message = "Order package not found." });
            }

            if (request.PackageName is not null)
                orderPackage.PackageName = request.PackageName;

            if (request.Quantity.HasValue)
                orderPackage.Quantity = request.Quantity.Value;

            if (request.UnitPrice.HasValue)
                orderPackage.UnitPrice = request.UnitPrice.Value;

            if (request.NoOfPax.HasValue)
                orderPackage.NoOfPax = request.NoOfPax.Value;

            if (request.BoatFareEnabled.HasValue)
                orderPackage.BoatFareEnabled = request.BoatFareEnabled.Value;

            if (request.BoatFareAmount.HasValue)
                orderPackage.BoatFareAmount = request.BoatFareEnabled == false ? 0m : request.BoatFareAmount.Value;

            if (request.BoatFareCalcType is not null)
                orderPackage.BoatFareCalcType = request.BoatFareCalcType;

            if (request.GratuityEnabled.HasValue)
                orderPackage.GratuityEnabled = request.GratuityEnabled.Value;

            if (request.GratuityAmount.HasValue)
                orderPackage.GratuityAmount = request.GratuityEnabled == false ? 0m : request.GratuityAmount.Value;

            if (request.GratuityCalcType is not null)
                orderPackage.GratuityCalcType = request.GratuityCalcType;

            // Always recompute line total server-side to avoid payload tampering.
            orderPackage.LineTotal = orderPackage.Quantity * orderPackage.UnitPrice;
            orderPackage.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            await RecalculateOrderTotalsAsync(orderPackage.OrderId);

            return Ok(orderPackage);
        }

        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteOrderPackagePost(int id)
        {
            var orderPackage = await _context.OrderPackages.FindAsync(id);
            if (orderPackage == null || orderPackage.IsDeleted)
            {
                return NotFound(new { message = "Order package not found." });
            }

            orderPackage.IsDeleted = true;
            orderPackage.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            await RecalculateOrderTotalsAsync(orderPackage.OrderId);

            return Ok(new { message = "Order package soft-deleted.", orderPackageId = id });
        }
    }
}