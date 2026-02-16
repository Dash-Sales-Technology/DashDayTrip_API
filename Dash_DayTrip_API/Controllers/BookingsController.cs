using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    public class CreateBookingRequest
    {
        public string OrderId { get; set; } = string.Empty;
        public DateTime BookingDate { get; set; }
        public int PaxCount { get; set; }
        public string Status { get; set; } = "confirmed";
    }

    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly ApiContext _context;
        private readonly ILogger<BookingsController> _logger;
        private const int MAX_PAX_PER_DATE = 3;

        public BookingsController(ApiContext context, ILogger<BookingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: api/Bookings
        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetBookings(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var query = _context.Bookings
                .Include(b => b.Order)
                .Where(b => !b.IsDeleted)
                .AsQueryable();

            if (startDate.HasValue)
            {
                query = query.Where(b => b.BookingDate >= startDate.Value);
            }

            if (endDate.HasValue)
            {
                query = query.Where(b => b.BookingDate <= endDate.Value);
            }

            var bookings = await query
                .OrderBy(b => b.BookingDate)
                .Select(b => new
                {
                    b.BookingId,
                    b.OrderId,
                    b.BookingDate,
                    b.PaxCount,
                    b.Status,
                    b.CreatedAt,
                    CustomerName = b.Order != null ? b.Order.CustomerName : null,
                    ReferenceNumber = b.Order != null ? b.Order.ReferenceNumber : null
                })
                .ToListAsync();

            return Ok(bookings);
        }

        // GET: api/Bookings/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<Booking>> GetBooking(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Order)
                .FirstOrDefaultAsync(b => b.BookingId == id && !b.IsDeleted);

            if (booking == null)
            {
                return NotFound();
            }

            return Ok(booking);
        }

        // GET: api/Bookings/availability
        [HttpGet("availability")]
        public async Task<ActionResult<object>> GetAvailability([FromQuery] DateTime date)
        {
            var totalPax = await _context.Bookings
                .Where(b => b.BookingDate == date.Date && b.Status == "confirmed" && !b.IsDeleted)
                .SumAsync(b => b.PaxCount);

            return Ok(new
            {
                BookingDate = date.Date,
                TotalPax = totalPax,
                RemainingCapacity = MAX_PAX_PER_DATE - totalPax,
                MaxCapacity = MAX_PAX_PER_DATE
            });
        }

        // GET: api/Bookings/calendar
        [HttpGet("calendar")]
        public async Task<ActionResult<IEnumerable<object>>> GetCalendarData(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var data = await _context.Bookings
                .Where(b => b.BookingDate >= start.Date && b.BookingDate <= end.Date && b.Status == "confirmed" && !b.IsDeleted)
                .GroupBy(b => b.BookingDate)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalPax = g.Sum(b => b.PaxCount),
                    BookingCount = g.Count()
                })
                .ToListAsync();

            return Ok(data);
        }

        // POST: api/Bookings
        [HttpPost]
        public async Task<ActionResult<Booking>> CreateBooking([FromBody] CreateBookingRequest request)
        {
            // Validate order exists
            var orderExists = await _context.Orders.AnyAsync(o => o.OrderId == request.OrderId);
            if (!orderExists)
            {
                return BadRequest(new { message = "Invalid Order ID." });
            }

            // Check capacity
            var currentPax = await _context.Bookings
                .Where(b => b.BookingDate == request.BookingDate.Date && b.Status == "confirmed" && !b.IsDeleted)
                .SumAsync(b => b.PaxCount);

            if (currentPax + request.PaxCount > MAX_PAX_PER_DATE)
            {
                return BadRequest(new
                {
                    message = $"Capacity exceeded for this date. Maximum {MAX_PAX_PER_DATE} pax allowed.",
                    currentPax,
                    remainingCapacity = MAX_PAX_PER_DATE - currentPax,
                    requestedPax = request.PaxCount
                });
            }

            var booking = new Booking
            {
                OrderId = request.OrderId,
                BookingDate = request.BookingDate.Date,
                PaxCount = request.PaxCount,
                Status = request.Status,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetBooking), new { id = booking.BookingId }, booking);
        }

        // POST: api/Bookings/{id}/delete  -> soft-delete via POST
        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteBookingPost(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null || booking.IsDeleted)
            {
                return NotFound();
            }

            booking.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Booking soft-deleted", bookingId = id });
        }
    }
}