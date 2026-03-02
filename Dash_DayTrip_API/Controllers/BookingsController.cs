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

        // HARDCODED CONSTANT
        private const int MAX_PAX_PER_DATE = 20;
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
                    b.GratuityFee,
                    b.IsFirstBooking,
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
            // Use .Date to ensure we ignore time components
            var totalPax = await _context.Bookings
                .Where(b => b.BookingDate.Date == date.Date && b.Status == "confirmed" && !b.IsDeleted)
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
                .Where(b => b.BookingDate.Date >= start.Date && b.BookingDate.Date <= end.Date && b.Status == "confirmed" && !b.IsDeleted)
                .GroupBy(b => b.BookingDate.Date) // ⭐ Group by Date portion only
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
            // Capacity check using the constant
            var currentPax = await _context.Bookings
                .Where(b => b.BookingDate.Date == request.BookingDate.Date && b.Status == "confirmed" && !b.IsDeleted)
                .SumAsync(b => b.PaxCount);
            if (currentPax + request.PaxCount > MAX_PAX_PER_DATE)
            {
                return BadRequest(new
                {
                    message = $"Capacity exceeded. Maximum {MAX_PAX_PER_DATE} pax allowed.",
                    currentPax,
                    remainingCapacity = MAX_PAX_PER_DATE - currentPax
                });
            }
            // Check if this is the first booking for this order
            bool hasExistingBookings = await _context.Bookings
                .AnyAsync(b => b.OrderId == request.OrderId && !b.IsDeleted);

            var booking = new Booking
            {
                OrderId = request.OrderId,
                BookingDate = request.BookingDate.Date,
                PaxCount = request.PaxCount,
                Status = request.Status,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                GratuityFee = request.PaxCount * 5.0m, // Calculate RM5 per pax
                IsFirstBooking = !hasExistingBookings
            };
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();
            return Ok(booking);
        }

        // POST: api/Bookings/{id}/delete
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