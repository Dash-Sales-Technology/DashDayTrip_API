using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PromotionsController : ControllerBase
    {
        private readonly ApiContext _context;

        private const string SourceQuickBooking = "quick_booking";
        private static readonly HashSet<string> AllowedDiscountTypes =
            new(StringComparer.OrdinalIgnoreCase) { "amount", "percentage" };

        public PromotionsController(ApiContext context)
        {
            _context = context;
        }

        private static string NormalizeDiscountType(string? discountType)
        {
            if (string.IsNullOrWhiteSpace(discountType)) return string.Empty;
            var normalized = discountType.Trim().ToLowerInvariant();
            return AllowedDiscountTypes.Contains(normalized) ? normalized : string.Empty;
        }

        private async Task<Order?> GetActiveOrderAsync(int orderId)
        {
            return await _context.Orders
                .FirstOrDefaultAsync(o => o.OrderId == orderId && !o.IsDeleted);
        }

        private static bool IsQuickBooking(Order order) =>
            string.Equals(order.Source?.Trim(), SourceQuickBooking, StringComparison.OrdinalIgnoreCase);

        // GET: api/Promotions/order/{orderId}
        [HttpGet("order/{orderId:int}")]
        public async Task<ActionResult<Promotion>> GetPromotionByOrder(int orderId)
        {
            var promotion = await _context.Promotions
                .FirstOrDefaultAsync(p => p.OrderId == orderId && !p.IsDeleted);

            if (promotion == null)
            {
                return NotFound(new { message = "No promotion found for this order." });
            }

            return Ok(promotion);
        }

        // POST: api/Promotions
        [HttpPost]
        public async Task<ActionResult<Promotion>> CreatePromotion([FromBody] Promotion promotion)
        {
            var order = await GetActiveOrderAsync(promotion.OrderId);
            if (order == null)
                return BadRequest(new { message = "Invalid OrderId." });

            if (IsQuickBooking(order))
                return BadRequest(new { message = "Promotions are not allowed for quick_booking orders." });

            var normalizedType = NormalizeDiscountType(promotion.DiscountType);
            if (!string.IsNullOrEmpty(promotion.DiscountType) && string.IsNullOrEmpty(normalizedType))
                return BadRequest(new { message = "Invalid DiscountType. Allowed: amount, percentage." });

            if (promotion.DiscountValue.HasValue && promotion.DiscountValue.Value < 0)
                return BadRequest(new { message = "DiscountValue cannot be negative." });

            var existing = await _context.Promotions
                .FirstOrDefaultAsync(p => p.OrderId == promotion.OrderId && !p.IsDeleted);

            if (existing != null)
                return BadRequest(new { message = "Promotion already exists for this order. Use /api/Promotions/upsert." });

            promotion.DiscountType = string.IsNullOrEmpty(normalizedType) ? null : normalizedType;
            promotion.CreatedAt = DateTime.UtcNow;
            promotion.UpdatedAt = DateTime.UtcNow;
            promotion.IsDeleted = false;

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            return Ok(promotion);
        }

        // POST: api/Promotions/upsert
        [HttpPost("upsert")]
        public async Task<ActionResult<Promotion>> UpsertPromotion([FromBody] Promotion promotion)
        {
            var order = await GetActiveOrderAsync(promotion.OrderId);
            if (order == null)
                return BadRequest(new { message = "Invalid OrderId." });

            if (IsQuickBooking(order))
                return BadRequest(new { message = "Promotions are not allowed for quick_booking orders." });

            var normalizedType = NormalizeDiscountType(promotion.DiscountType);
            if (!string.IsNullOrEmpty(promotion.DiscountType) && string.IsNullOrEmpty(normalizedType))
                return BadRequest(new { message = "Invalid DiscountType. Allowed: amount, percentage." });

            if (promotion.DiscountValue.HasValue && promotion.DiscountValue.Value < 0)
                return BadRequest(new { message = "DiscountValue cannot be negative." });

            var existing = await _context.Promotions
                .FirstOrDefaultAsync(p => p.OrderId == promotion.OrderId && !p.IsDeleted);

            if (existing != null)
            {
                existing.VoucherCode = promotion.VoucherCode;
                existing.DiscountType = string.IsNullOrEmpty(normalizedType) ? null : normalizedType;
                existing.DiscountValue = promotion.DiscountValue;
                existing.AwardRemarkEnabled = promotion.AwardRemarkEnabled;
                existing.AwardRemark = promotion.AwardRemark;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(existing);
            }

            promotion.DiscountType = string.IsNullOrEmpty(normalizedType) ? null : normalizedType;
            promotion.CreatedAt = DateTime.UtcNow;
            promotion.UpdatedAt = DateTime.UtcNow;
            promotion.IsDeleted = false;

            _context.Promotions.Add(promotion);
            await _context.SaveChangesAsync();

            return Ok(promotion);
        }

        // POST: api/Promotions/{id}/delete
        [HttpPost("{id:int}/delete")]
        public async Task<IActionResult> DeletePromotion(int id)
        {
            var promotion = await _context.Promotions.FindAsync(id);
            if (promotion == null || promotion.IsDeleted)
            {
                return NotFound(new { message = "Promotion not found." });
            }

            promotion.IsDeleted = true;
            promotion.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Promotion deleted.", promotionId = id });
        }
    }
}