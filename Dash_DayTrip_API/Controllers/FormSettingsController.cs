using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class FormSettingsController : ControllerBase
    {
        private readonly ApiContext _context;

        public FormSettingsController(ApiContext context)
        {
            _context = context;
        }
        
        // GET: api/FormSettings/form/{formId}
        [HttpGet("form/{formId}")]
        public async Task<ActionResult<FormSettings>> GetFormSettings(string formId)
        {
            var settings = await _context.FormSettings
                .FirstOrDefaultAsync(fs => fs.FormId == formId);
            
            if (settings == null)
            {
                return NotFound();
            }
            
            return settings;
        }
        
        // POST: api/FormSettings/upsert
        [HttpPost("upsert")]
        public async Task<ActionResult<FormSettings>> UpsertFormSettings([FromBody] FormSettings settings)
        {
            var existing = await _context.FormSettings
                .FirstOrDefaultAsync(fs => fs.FormId == settings.FormId);
            
            if (existing == null)
            {
                // Create new
                settings.CreatedAt = DateTime.UtcNow;
                settings.UpdatedAt = DateTime.UtcNow;
                _context.FormSettings.Add(settings);
                await _context.SaveChangesAsync();
                return Ok(settings);
            }
            else
            {
                // Update existing
                existing.SalesExecutives = settings.SalesExecutives;
                existing.DepositMode = settings.DepositMode;
                existing.DepositAmount = settings.DepositAmount;
                existing.SSTEnabled = settings.SSTEnabled;
                existing.SSTPercentage = settings.SSTPercentage;
                existing.TaxIdNumber = settings.TaxIdNumber;
                existing.Currency = settings.Currency;
                existing.NextDayCutoffTime = settings.NextDayCutoffTime;
                existing.MaxGuestPerDay = settings.MaxGuestPerDay;
                existing.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                return Ok(existing);
            }
        }
        
        // POST: api/FormSettings/{id}/delete  -> soft-delete
        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteFormSettings(int id)
        {
            var settings = await _context.FormSettings.FindAsync(id);
            if (settings == null || settings.IsDeleted)
            {
                return NotFound();
            }
            
            // Soft-delete: set flag instead of Remove
            settings.IsDeleted = true;
            settings.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            
            return Ok(new { message = "FormSettings soft-deleted", id });
        }
    }
}