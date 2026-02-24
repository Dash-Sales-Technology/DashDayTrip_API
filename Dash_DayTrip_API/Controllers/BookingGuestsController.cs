using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    public class CreateBookingGuestRequest
    {
        public string FullName { get; set; } = string.Empty;
        public string IcNumber { get; set; } = string.Empty;
        public string? GuestType { get; set; } = "adult";
        public string? Notes { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class BookingGuestsController : ControllerBase
    {
        private readonly ApiContext _context;
        private readonly ILogger<BookingGuestsController> _logger;

        public BookingGuestsController(ApiContext context, ILogger<BookingGuestsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/BookingGuests/by-booking/{bookingId}
        [HttpGet("by-booking/{bookingId}")]
        public async Task<ActionResult<IEnumerable<BookingGuest>>> GetGuestsByBooking(int bookingId)
        {
            var bookingExists = await _context.Bookings
                .AnyAsync(b => b.BookingId == bookingId);

            if (!bookingExists)
            {
                return NotFound(new { message = "Booking not found." });
            }

            var guests = await _context.BookingGuests
                .Where(g => g.BookingId == bookingId)
                .OrderBy(g => g.GuestId)
                .ToListAsync();

            return Ok(guests);
        }

        // POST: api/BookingGuests/{bookingId}
        [HttpPost("{bookingId}")]
        public async Task<ActionResult<IEnumerable<BookingGuest>>> AddGuests(
            int bookingId,
            [FromBody] List<CreateBookingGuestRequest> requests)
        {
            var bookingExists = await _context.Bookings
                .AnyAsync(b => b.BookingId == bookingId);

            if (!bookingExists)
            {
                return BadRequest(new { message = "Invalid Booking ID." });
            }

            var guests = new List<BookingGuest>();

            foreach (var req in requests)
            {
                var guest = new BookingGuest
                {
                    BookingId = bookingId,
                    FullName = req.FullName,
                    IcNumber = req.IcNumber,
                    GuestType = req.GuestType ?? "adult",
                    Notes = req.Notes,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };

                _context.BookingGuests.Add(guest);
                guests.Add(guest);
            }

            await _context.SaveChangesAsync();

            return Ok(guests);
        }

        // POST: api/BookingGuests/{guestId}/update
        [HttpPost("{guestId}/update")]
        public async Task<ActionResult<BookingGuest>> UpdateGuest(
            int guestId,
            [FromBody] CreateBookingGuestRequest request)
        {
            var guest = await _context.BookingGuests
                .FirstOrDefaultAsync(g => g.GuestId == guestId);

            if (guest == null)
            {
                return NotFound(new { message = "Guest not found." });
            }

            guest.FullName = request.FullName;
            guest.IcNumber = request.IcNumber;
            guest.GuestType = request.GuestType ?? guest.GuestType;
            guest.Notes = request.Notes;
            guest.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(guest);
        }

        // POST: api/BookingGuests/{guestId}/delete
        [HttpPost("{guestId}/delete")]
        public async Task<IActionResult> DeleteGuest(int guestId)
        {
            var guest = await _context.BookingGuests
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(g => g.GuestId == guestId && !g.IsDeleted);

            if (guest == null)
            {
                return NotFound(new { message = "Guest not found." });
            }

            guest.IsDeleted = true;
            guest.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Guest soft-deleted", guestId });
        }
    }
}