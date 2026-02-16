using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    public class CreateOrderPackageRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public string PackageId { get; set; } = string.Empty;
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

        public OrderPackagesController(ApiContext context, ILogger<OrderPackagesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/OrderPackages
        [HttpGet]
        public async Task<ActionResult<IEnumerable<OrderPackage>>> GetOrderPackages()
        {
            var orderPackages = await _context.OrderPackages
                .Where(op => !op.IsDeleted)
                .ToListAsync();

            return Ok(orderPackages);
        }

        // GET: api/OrderPackages/{id}
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

        // GET: api/OrderPackages/ByOrder/{orderId}
        [HttpGet("ByOrder/{orderId}")]
        public async Task<ActionResult<IEnumerable<OrderPackage>>> GetOrderPackagesByOrder(string orderId)
        {
            var orderPackages = await _context.OrderPackages
                .Where(op => op.OrderId == orderId && !op.IsDeleted)
                .ToListAsync();

            return Ok(orderPackages);
        }

        // POST: api/OrderPackages
        [HttpPost]
        public async Task<ActionResult<OrderPackage>> CreateOrderPackage([FromBody] CreateOrderPackageRequest request)
        {
            // Validate order exists
            var orderExists = await _context.Orders.AnyAsync(o => o.OrderId == request.OrderId);
            if (!orderExists)
            {
                return BadRequest(new { message = "Invalid Order ID." });
            }

            var orderPackage = new OrderPackage
            {
                OrderId = request.OrderId,
                PackageId = request.PackageId,
                PackageName = request.PackageName,
                Quantity = request.Quantity,
                UnitPrice = request.UnitPrice,
                NoOfPax = request.NoOfPax,
                BoatFareEnabled = request.BoatFareEnabled,
                BoatFareAmount = request.BoatFareAmount,
                BoatFareCalcType = request.BoatFareCalcType,
                GratuityEnabled = request.GratuityEnabled,
                GratuityAmount = request.GratuityAmount,
                GratuityCalcType = request.GratuityCalcType,
                LineTotal = request.LineTotal,
                IsDeleted = false
            };

            _context.OrderPackages.Add(orderPackage);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetOrderPackage), new { id = orderPackage.OrderPackageId }, orderPackage);
        }

        // POST: api/OrderPackages/{id}/update
        [HttpPost("{id}/update")]
        public async Task<IActionResult> UpdateOrderPackagePost(int id, [FromBody] UpdateOrderPackageRequest request)
        {
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
                orderPackage.BoatFareAmount = request.BoatFareAmount.Value;
            if (request.BoatFareCalcType is not null)
                orderPackage.BoatFareCalcType = request.BoatFareCalcType;
            if (request.GratuityEnabled.HasValue)
                orderPackage.GratuityEnabled = request.GratuityEnabled.Value;
            if (request.GratuityAmount.HasValue)
                orderPackage.GratuityAmount = request.GratuityAmount.Value;
            if (request.GratuityCalcType is not null)
                orderPackage.GratuityCalcType = request.GratuityCalcType;
            if (request.LineTotal.HasValue)
                orderPackage.LineTotal = request.LineTotal.Value;

            await _context.SaveChangesAsync();

            return Ok(orderPackage);
        }

        // POST: api/OrderPackages/{id}/delete  -> soft-delete via POST
        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteOrderPackagePost(int id)
        {
            var orderPackage = await _context.OrderPackages.FindAsync(id);
            if (orderPackage == null || orderPackage.IsDeleted)
            {
                return NotFound(new { message = "Order package not found." });
            }

            orderPackage.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Order package soft-deleted.", orderPackageId = id });
        }
    }
}