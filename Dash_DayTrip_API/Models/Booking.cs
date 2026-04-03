using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dash_DayTrip_API.Models
{
    [Table("Bookings")]
    public class Booking
    {
        [Key]
        public int BookingId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Required]
        public DateTime BookingDate { get; set; }

        [Required]
        public int PaxCount { get; set; }

        public string? Status { get; set; } = "confirmed";

        public DateTime? CreatedAt { get; set; }

        public decimal GratuityFee { get; set; }

        // New cancellation/audit fields
        public string? CancellationReason { get; set; }
        public DateTime? CancelledAt { get; set; }

        public bool IsFirstBooking { get; set; }

        // Per-Package Pax Tracking
        public int? PackageId { get; set; }
        public string? PackageName { get; set; }

        public bool IsDeleted { get; set; }

        // Navigation property
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }
    }
}