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

        private const decimal DEFAULT_GRATUITY_PER_PAX = 5.0m;

        public BookingGuestsController(ApiContext context, ILogger<BookingGuestsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private async Task<decimal> GetGratuityPerPaxAsync(int orderId)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return DEFAULT_GRATUITY_PER_PAX;

            var settings = await _context.FormSettings.AsNoTracking()
                .FirstOrDefaultAsync(fs => fs.FormId == order.FormId);

            return settings?.BookingGratuityAmount ?? DEFAULT_GRATUITY_PER_PAX;
        }

        private async Task RecalculateOrderFinancialsAsync(int orderId)
        {
            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == orderId);
            if (order == null) return;

            // Only first confirmed booking gratuity contributes to order ledger totals.
            var firstBookingGratuity = await _context.Bookings
                .Where(b => b.OrderId == orderId &&
                            !b.IsDeleted &&
                            b.Status == "confirmed" &&
                            b.IsFirstBooking)
                .SumAsync(b => (decimal?)b.GratuityFee) ?? 0m;

            order.TotalGratuity = firstBookingGratuity;
            order.GrandTotal = order.Subtotal + order.TotalBoatFare + firstBookingGratuity;

            var totalPaid = await _context.OrderPayments
                .Where(p => p.OrderId == orderId && !p.IsVoided)
                .SumAsync(p => (decimal?)p.Amount) ?? 0m;

            order.AmountPaid = totalPaid;
            order.BalanceDue = Math.Max(0m, order.GrandTotal - totalPaid);
            order.PaymentStatus = totalPaid <= 0m
                ? "Pending"
                : order.BalanceDue <= 0m
                    ? "Paid"
                    : "Partial";

            var latestPayment = await _context.OrderPayments
                .Where(p => p.OrderId == orderId && !p.IsVoided)
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.OrderPaymentId)
                .FirstOrDefaultAsync();

            order.PaymentMethod = latestPayment?.PaymentMethod;
            order.TransactionRef = latestPayment?.TransactionRef;
            order.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

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

            await _context.SaveChangesAsync();

            booking.PaxCount = await _context.BookingGuests
                .CountAsync(g => g.BookingId == bookingId && !g.IsDeleted);

            var gratuityPerPax = await GetGratuityPerPaxAsync(booking.OrderId);
            booking.GratuityFee = booking.PaxCount * gratuityPerPax;

            await _context.SaveChangesAsync();
            await RecalculateOrderFinancialsAsync(booking.OrderId);

            return Ok(guests);
        }

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

        [HttpPost("{guestId}/delete")]
        public async Task<IActionResult> DeleteGuest(int guestId)
        {
            var guest = await _context.BookingGuests
                .FirstOrDefaultAsync(g => g.GuestId == guestId && !g.IsDeleted);

            if (guest == null) return NotFound();

            var booking = await _context.Bookings.FindAsync(guest.BookingId);

            guest.IsDeleted = true;
            guest.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            if (booking != null)
            {
                booking.PaxCount = await _context.BookingGuests
                    .CountAsync(g => g.BookingId == booking.BookingId && !g.IsDeleted);

                var gratuityPerPax = await GetGratuityPerPaxAsync(booking.OrderId);
                booking.GratuityFee = booking.PaxCount * gratuityPerPax;

                await _context.SaveChangesAsync();
                await RecalculateOrderFinancialsAsync(booking.OrderId);
            }

            return Ok(new { message = "Guest removed and pax count synced", bookingId = guest.BookingId });
        }
    }
}