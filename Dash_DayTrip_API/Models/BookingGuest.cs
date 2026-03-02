using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dash_DayTrip_API.Models
{
    [Table("BookingGuests")]
    public class BookingGuest
    {
        [Key]
        public int GuestId { get; set; }

        [Required]
        public int BookingId { get; set; }

        [Required]
        [MaxLength(200)]
        public string FullName { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string IcNumber { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? MobilePhone { get; set; }

        [MaxLength(20)]
        public string GuestType { get; set; } = "adult";

        [MaxLength(500)]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        public bool IsDeleted { get; set; }

        // Navigation property
        [ForeignKey("BookingId")]
        public virtual Booking? Booking { get; set; }
    }
}