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
        public string OrderId { get; set; } = string.Empty;

        [Required]
        public DateTime BookingDate { get; set; }

        [Required]
        public int PaxCount { get; set; }

        public string? Status { get; set; } = "confirmed";

        public DateTime? CreatedAt { get; set; }

        public decimal GratuityFee { get; set; }
        public bool IsFirstBooking { get; set; }

        // Navigation property
        [ForeignKey("OrderId")]
        public virtual Order? Order { get; set; }

        public bool IsDeleted { get; set; }
    }
}