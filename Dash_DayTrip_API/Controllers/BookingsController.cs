using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Dash_DayTrip_API.Models;
using Dash_DayTrip_API.Data;

namespace Dash_DayTrip_API.Controllers
{
    public class CreateBookingRequest
    {
        public int OrderId { get; set; }
        public DateTime BookingDate { get; set; }
        public int PaxCount { get; set; }
        public string Status { get; set; } = "confirmed";
        public int? PackageId { get; set; }
        public string? PackageName { get; set; }
    }

    public class CancelBookingRequest
    {
        public string? CancellationReason { get; set; }
    }

    public class UpdateBookingRequest
    {
        public DateTime? BookingDate { get; set; }
        public int? PaxCount { get; set; }
        public string? Status { get; set; }
        public string? CancellationReason { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class BookingsController : ControllerBase
    {
        private readonly ApiContext _context;
        private readonly ILogger<BookingsController> _logger;

        private const int DEFAULT_MAX_PAX = 20;
        private const decimal DEFAULT_GRATUITY_PER_PAX = 5.0m;

        private static readonly HashSet<string> AllowedBookingStatuses =
            new(StringComparer.OrdinalIgnoreCase) { "confirmed", "cancelled", "pending" };

        public BookingsController(ApiContext context, ILogger<BookingsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        private static string NormalizeBookingStatus(string? status)
        {
            var normalized = (status ?? "confirmed").Trim().ToLowerInvariant();
            return AllowedBookingStatuses.Contains(normalized) ? normalized : string.Empty;
        }

        private async Task<(int maxPax, decimal gratuityPerPax)> GetFormSettingsValuesAsync(int orderId)
        {
            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == orderId);

            if (order == null)
                return (DEFAULT_MAX_PAX, DEFAULT_GRATUITY_PER_PAX);

            var settings = await _context.FormSettings.AsNoTracking()
                .FirstOrDefaultAsync(fs => fs.FormId == order.FormId);

            int maxPax = settings?.MaxGuestPerDay ?? DEFAULT_MAX_PAX;
            decimal gratuity = settings?.BookingGratuityAmount ?? DEFAULT_GRATUITY_PER_PAX;

            return (maxPax, gratuity);
        }

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
                query = query.Where(b => b.BookingDate >= startDate.Value);

            if (endDate.HasValue)
                query = query.Where(b => b.BookingDate <= endDate.Value);

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
                    b.CancellationReason,
                    b.CancelledAt,
                    b.PackageId,
                    b.PackageName,
                    CustomerName = b.Order != null ? b.Order.CustomerName : null,
                    ReferenceNumber = b.Order != null ? b.Order.ReferenceNumber : null
                })
                .ToListAsync();

            return Ok(bookings);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Booking>> GetBooking(int id)
        {
            var booking = await _context.Bookings
                .Include(b => b.Order)
                .FirstOrDefaultAsync(b => b.BookingId == id && !b.IsDeleted);

            if (booking == null)
                return NotFound();

            return Ok(booking);
        }

        [HttpGet("availability")]
        public async Task<ActionResult<object>> GetAvailability(
            [FromQuery] DateTime date,
            [FromQuery] int? formId = null)
        {
            int maxPax = DEFAULT_MAX_PAX;

            if (formId.HasValue)
            {
                var settings = await _context.FormSettings.AsNoTracking()
                    .FirstOrDefaultAsync(fs => fs.FormId == formId.Value);
                maxPax = settings?.MaxGuestPerDay ?? DEFAULT_MAX_PAX;
            }

            var totalPax = await _context.Bookings
                .Where(b => b.BookingDate.Date == date.Date &&
                            b.Status == "confirmed" &&
                            !b.IsDeleted)
                .SumAsync(b => b.PaxCount);

            return Ok(new
            {
                BookingDate = date.Date,
                TotalPax = totalPax,
                RemainingCapacity = maxPax - totalPax,
                MaxCapacity = maxPax
            });
        }

        [HttpGet("calendar")]
        public async Task<ActionResult<IEnumerable<object>>> GetCalendarData(
            [FromQuery] DateTime start,
            [FromQuery] DateTime end)
        {
            var data = await _context.Bookings
                .Where(b => b.BookingDate.Date >= start.Date &&
                            b.BookingDate.Date <= end.Date &&
                            b.Status == "confirmed" &&
                            !b.IsDeleted)
                .GroupBy(b => b.BookingDate.Date)
                .Select(g => new
                {
                    Date = g.Key,
                    TotalPax = g.Sum(b => b.PaxCount),
                    BookingCount = g.Count()
                })
                .ToListAsync();

            return Ok(data);
        }

        [HttpPost]
        public async Task<ActionResult<Booking>> CreateBooking([FromBody] CreateBookingRequest request)
        {
            if (request.OrderId <= 0)
                return BadRequest(new { message = "OrderId is required." });

            if (request.PaxCount <= 0)
                return BadRequest(new { message = "PaxCount must be greater than zero." });

            var normalizedStatus = NormalizeBookingStatus(request.Status);
            if (string.IsNullOrEmpty(normalizedStatus))
                return BadRequest(new { message = "Invalid booking status. Allowed: confirmed, cancelled, pending." });

            var order = await _context.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderId == request.OrderId);

            if (order == null)
                return BadRequest(new { message = "Invalid Order ID." });

            var formSettings = await _context.FormSettings.AsNoTracking()
                .FirstOrDefaultAsync(fs => fs.FormId == order.FormId);

            int maxPax = formSettings?.MaxGuestPerDay ?? DEFAULT_MAX_PAX;
            decimal gratuityPerPax = formSettings?.BookingGratuityAmount ?? DEFAULT_GRATUITY_PER_PAX;

            var currentPax = await _context.Bookings
                .Where(b => b.BookingDate.Date == request.BookingDate.Date &&
                            b.Status == "confirmed" &&
                            !b.IsDeleted)
                .SumAsync(b => b.PaxCount);

            if (normalizedStatus == "confirmed" && currentPax + request.PaxCount > maxPax)
            {
                return BadRequest(new
                {
                    message = $"Capacity exceeded. Maximum {maxPax} pax allowed for this date.",
                    currentPax,
                    remainingCapacity = maxPax - currentPax
                });
            }

            bool hasExistingConfirmed = await _context.Bookings
                .AnyAsync(b => b.OrderId == request.OrderId &&
                               b.Status == "confirmed" &&
                               !b.IsDeleted);

            var booking = new Booking
            {
                OrderId = request.OrderId,
                BookingDate = request.BookingDate.Date,
                PaxCount = request.PaxCount,
                Status = normalizedStatus,
                CreatedAt = DateTime.UtcNow,
                IsDeleted = false,
                GratuityFee = request.PaxCount * gratuityPerPax,
                IsFirstBooking = !hasExistingConfirmed,
                PackageId = request.PackageId,
                PackageName = request.PackageName
            };

            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return Ok(booking);
        }

        [HttpPost("{id}/update")]
        public async Task<IActionResult> UpdateBooking(int id, [FromBody] UpdateBookingRequest request)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == id && !b.IsDeleted);

            if (booking == null)
                return NotFound();

            if (request.PaxCount.HasValue && request.PaxCount.Value <= 0)
                return BadRequest(new { message = "PaxCount must be greater than zero." });

            var targetDate = request.BookingDate?.Date ?? booking.BookingDate.Date;
            var targetPax = request.PaxCount ?? booking.PaxCount;

            var targetStatus = NormalizeBookingStatus(request.Status ?? booking.Status ?? "confirmed");
            if (string.IsNullOrEmpty(targetStatus))
                return BadRequest(new { message = "Invalid booking status. Allowed: confirmed, cancelled, pending." });

            if (targetStatus == "confirmed")
            {
                var (maxPax, _) = await GetFormSettingsValuesAsync(booking.OrderId);

                var confirmedPaxOnDate = await _context.Bookings
                    .Where(b => b.BookingId != id &&
                                !b.IsDeleted &&
                                b.Status == "confirmed" &&
                                b.BookingDate.Date == targetDate)
                    .SumAsync(b => b.PaxCount);

                if (confirmedPaxOnDate + targetPax > maxPax)
                {
                    return BadRequest(new
                    {
                        message = $"Cannot set booking to confirmed: capacity exceeded ({confirmedPaxOnDate}/{maxPax} already used)."
                    });
                }
            }

            if (request.BookingDate.HasValue)
                booking.BookingDate = request.BookingDate.Value.Date;

            if (request.PaxCount.HasValue)
                booking.PaxCount = request.PaxCount.Value;

            booking.Status = targetStatus;

            if (targetStatus == "cancelled")
            {
                booking.CancellationReason = request.CancellationReason;
                booking.CancelledAt = booking.CancelledAt ?? DateTime.UtcNow;
            }
            else if (targetStatus == "confirmed" || targetStatus == "pending")
            {
                booking.CancellationReason = null;
                booking.CancelledAt = null;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Booking updated successfully.",
                bookingId = booking.BookingId,
                booking.Status,
                booking.CancellationReason,
                booking.CancelledAt
            });
        }

        [HttpPost("{id}/delete")]
        public async Task<IActionResult> DeleteBookingPost(int id)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null || booking.IsDeleted)
                return NotFound();

            booking.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Booking soft-deleted", bookingId = id });
        }

        [HttpPost("{id}/cancel")]
        public async Task<IActionResult> CancelBooking(int id, [FromBody] CancelBookingRequest request)
        {
            var booking = await _context.Bookings.FindAsync(id);
            if (booking == null || booking.IsDeleted)
                return NotFound();

            if (booking.Status == "cancelled")
                return BadRequest(new { message = "Booking is already cancelled." });

            booking.Status = "cancelled";
            booking.CancellationReason = request?.CancellationReason;
            booking.CancelledAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Booking cancelled successfully.",
                bookingId = id,
                booking.Status,
                booking.CancellationReason,
                booking.CancelledAt
            });
        }

        [HttpPost("{id}/reinstate")]
        public async Task<IActionResult> ReinstateBooking(int id)
        {
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == id && !b.IsDeleted);

            if (booking == null)
                return NotFound();

            if ((booking.Status ?? "").ToLower() != "cancelled")
                return BadRequest(new { message = "Booking is not cancelled." });

            var (maxPax, _) = await GetFormSettingsValuesAsync(booking.OrderId);

            var confirmedPaxOnDate = await _context.Bookings
                .Where(b => b.BookingId != id &&
                            !b.IsDeleted &&
                            b.Status == "confirmed" &&
                            b.BookingDate.Date == booking.BookingDate.Date)
                .SumAsync(b => b.PaxCount);

            if (confirmedPaxOnDate + booking.PaxCount > maxPax)
            {
                return BadRequest(new
                {
                    message = $"Cannot reinstate booking: capacity exceeded ({confirmedPaxOnDate}/{maxPax} already used)."
                });
            }

            booking.Status = "confirmed";
            booking.CancellationReason = null;
            booking.CancelledAt = null;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Booking reinstated successfully.",
                bookingId = booking.BookingId,
                booking.Status,
                booking.CancellationReason,
                booking.CancelledAt
            });
        }
    }
}