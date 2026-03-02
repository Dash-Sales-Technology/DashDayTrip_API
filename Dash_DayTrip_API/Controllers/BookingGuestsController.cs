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
        public string? MobilePhone { get; set; }
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
                .Where(g => g.BookingId == bookingId && !g.IsDeleted)
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
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && !b.IsDeleted);

            if (booking == null) return BadRequest(new { message = "Booking not found or deleted." });

            var guests = new List<BookingGuest>();
            foreach (var req in requests)
            {
                var guest = new BookingGuest
                {
                    BookingId = bookingId,
                    FullName = req.FullName,
                    IcNumber = req.IcNumber,
                    MobilePhone = req.MobilePhone,
                    GuestType = req.GuestType ?? "adult",
                    Notes = req.Notes,
                    CreatedAt = DateTime.UtcNow,
                    IsDeleted = false
                };
                _context.BookingGuests.Add(guest);
                guests.Add(guest);
            }

            // ⭐ STEP 1: Save the new guest records first
            await _context.SaveChangesAsync();

            // ⭐ STEP 2: RE-SYNC PAX COUNT
            // We count the total number of guests currently in the DB for this booking
            // This prevents "Double Counting" if the booking already had an initial PaxCount.
            booking.PaxCount = await _context.BookingGuests
                .CountAsync(g => g.BookingId == bookingId && !g.IsDeleted);

            // ⭐ STEP 3: RECALCULATE GRATUITY
            booking.GratuityFee = booking.PaxCount * 5.0m;

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
            guest.MobilePhone = request.MobilePhone;
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
                .FirstOrDefaultAsync(g => g.GuestId == guestId && !g.IsDeleted);

            if (guest == null) return NotFound();

            var booking = await _context.Bookings.FindAsync(guest.BookingId);

            // ⭐ STEP 1: Soft-delete the guest
            guest.IsDeleted = true;
            guest.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            // ⭐ STEP 2: RE-SYNC PAX COUNT
            // After deletion, we recount the remaining active guests
            if (booking != null)
            {
                booking.PaxCount = await _context.BookingGuests
                    .CountAsync(g => g.BookingId == booking.BookingId && !g.IsDeleted);

                // ⭐ STEP 3: RECALCULATE GRATUITY
                booking.GratuityFee = booking.PaxCount * 5.0m;

                // Optional: If you want to auto-delete empty bookings:
                // if (booking.PaxCount == 0) booking.IsDeleted = true;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Guest removed and pax count synced", bookingId = guest.BookingId });
        }
    }
}