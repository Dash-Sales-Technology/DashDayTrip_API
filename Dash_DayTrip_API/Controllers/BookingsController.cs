using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Models.Responses; // ADD THIS
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly ApiContext _context;
        private readonly ILogger<BookingsController> _logger; //ADD THIS

        public BookingsController(ApiContext context, ILogger<BookingsController> logger) // UPDATE THIS
        {
            _context = context;
            _logger = logger; // ADD THIS
        }

        // GET: api/Bookings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<DayTripBooking>>> GetBookings()
        {
            return await _context.Bookings
                .Include(b => b.BookingPackages)
                .ToListAsync();
        }

        // GET: api/Bookings/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<DayTripBooking>> GetBooking(string id)
        {
            var booking = await _context.Bookings
                .Include(b => b.BookingPackages)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (booking == null)
            {
                return NotFound();
            }

            return booking;
        }

        // POST: api/Bookings
        [HttpPost]
        public async Task<ActionResult<DayTripBooking>> CreateBooking([FromBody] DayTripBooking booking)
        {
            booking.BookingId = Guid.NewGuid().ToString();
            booking.CreatedAt = DateTime.UtcNow;
            booking.UpdatedAt = DateTime.UtcNow;

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBooking), new { id = booking.BookingId }, booking);
        }

        // PUT: api/Bookings/{id}
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateBooking(string id, [FromBody] DayTripBooking booking)
        {
            if (id != booking.BookingId)
            {
                return BadRequest("ID mismatch");
            }

            // 1. Fetch the EXISTING booking from database (with packages)
            var existingBooking = await _context.Bookings
                .Include(b => b.BookingPackages)
                .FirstOrDefaultAsync(b => b.BookingId == id);

            if (existingBooking == null)
            {
                return NotFound();
            }

            // 2. Update parent booking properties
            _context.Entry(existingBooking).CurrentValues.SetValues(booking);
            existingBooking.UpdatedAt = DateTime.UtcNow;

            // 3. Handle child packages ONLY if provided
            if (booking.BookingPackages != null)
            {
                existingBooking.BookingPackages ??= new List<BookingPackage>();

                var newPackageIds = booking.BookingPackages.Select(p => p.BookingPackageId).ToList();

                // A. DELETE packages that were removed
                var packagesToDelete = existingBooking.BookingPackages
                    .Where(p => !newPackageIds.Contains(p.BookingPackageId) && p.BookingPackageId != 0)
                    .ToList();

                if (packagesToDelete.Any())
                {
                    _context.BookingPackages.RemoveRange(packagesToDelete);
                }

                // B. ADD or UPDATE packages
                foreach (var package in booking.BookingPackages)
                {
                    var existingPackage = existingBooking.BookingPackages
                        .FirstOrDefault(p => p.BookingPackageId == package.BookingPackageId && p.BookingPackageId != 0);

                    if (existingPackage != null)
                    {
                        // UPDATE existing package
                        _context.Entry(existingPackage).CurrentValues.SetValues(package);
                    }
                    else
                    {
                        // INSERT new package
                        package.BookingPackageId = 0; // Let DB generate new ID
                        existingBooking.BookingPackages.Add(package);
                    }
                }
            }

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!BookingExists(id))
                {
                    return NotFound();
                }
                throw;
            }

            return NoContent();
        }
        //[HttpPut("{id}")]
        //public async Task<IActionResult> UpdateBooking(string id, [FromBody] DayTripBooking booking)
        //{
        //    if (id != booking.BookingId)
        //    {
        //        return BadRequest();
        //    }

        //    booking.UpdatedAt = DateTime.UtcNow;
        //    _context.Entry(booking).State = EntityState.Modified;

        //    try
        //    {
        //        await _context.SaveChangesAsync();
        //    }
        //    catch (DbUpdateConcurrencyException)
        //    {
        //        if (!BookingExists(id))
        //        {
        //            return NotFound();
        //        }
        //        throw;
        //    }

        //    return NoContent();
        //}

        // DELETE: api/Bookings/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteBooking(string id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            _context.Bookings.Remove(booking);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // GET: api/Bookings/statistics
        [HttpGet("statistics")]
        public async Task<ActionResult<BookingStatistics>> GetStatistics(
            [FromQuery] string? formId = null,
            [FromQuery] string? merchantId = null)
        {
            var query = _context.Bookings.AsQueryable();

            if (!string.IsNullOrEmpty(formId))
            {
                query = query.Where(b => b.FormId == formId);
            }

            if (!string.IsNullOrEmpty(merchantId))
            {
                query = query.Where(b => b.MerchantId == merchantId);
            }

            var today = DateTime.Today;

            var stats = new BookingStatistics
            {
                TotalBookings = await query.CountAsync(),
                TotalRevenue = await query.SumAsync(b => b.GrandTotal),
                TotalDeposits = await query.SumAsync(b => b.DepositPaid),
                OutstandingBalance = await query.SumAsync(b => b.BalanceDue),
                TodayBookings = await query.CountAsync(b => b.CreatedAt.Date == today),
                TodayRevenue = await query.Where(b => b.CreatedAt.Date == today).SumAsync(b => b.GrandTotal),
                PendingCount = await query.CountAsync(b => b.Status == "pending"),
                ConfirmedCount = await query.CountAsync(b => b.Status == "confirmed"),
                CompletedCount = await query.CountAsync(b => b.Status == "completed"),
                CancelledCount = await query.CountAsync(b => b.Status == "cancelled")
            };

            return stats;
        }

        // GET: api/Bookings/form/{formId}
        [HttpGet("form/{formId}")]
        public async Task<ActionResult<IEnumerable<DayTripBooking>>> GetBookingsByForm(string formId)
        {
            return await _context.Bookings
                .Where(b => b.FormId == formId)
                .Include(b => b.BookingPackages)
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();
        }

        // PATCH: api/Bookings/{id}/status
        [HttpPatch("{id}/status")]
        public async Task<IActionResult> UpdateBookingStatus(string id, [FromBody] string status)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null)
            {
                return NotFound();
            }

            booking.Status = status;
            booking.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { BookingId = id, NewStatus = status });
        }

        private bool BookingExists(string id)
        {
            return _context.Bookings.Any(b => b.BookingId == id);
        }
    }
}