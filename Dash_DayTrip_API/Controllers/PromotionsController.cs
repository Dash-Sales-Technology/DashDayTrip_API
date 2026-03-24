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

        public PromotionsController(ApiContext context)
        {
            _context = context;
        }

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
            var existing = await _context.Promotions
                .FirstOrDefaultAsync(p => p.OrderId == promotion.OrderId && !p.IsDeleted);

            if (existing != null)
            {
                // Update
                existing.VoucherCode = promotion.VoucherCode;
                existing.DiscountType = promotion.DiscountType;
                existing.DiscountValue = promotion.DiscountValue;
                existing.AwardRemarkEnabled = promotion.AwardRemarkEnabled;
                existing.AwardRemark = promotion.AwardRemark;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(existing);
            }
            else
            {
                // Create
                promotion.CreatedAt = DateTime.UtcNow;
                promotion.UpdatedAt = DateTime.UtcNow;
                promotion.IsDeleted = false;

                _context.Promotions.Add(promotion);
                await _context.SaveChangesAsync();

                return Ok(promotion);
            }
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
