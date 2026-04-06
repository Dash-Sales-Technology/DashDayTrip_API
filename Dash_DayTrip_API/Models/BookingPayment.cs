using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dash_DayTrip_API.Models
{
    public class BookingPayment
    {
        [Key]
        public int BookingPaymentId { get; set; }

        [Required]
        public int BookingId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Amount { get; set; }

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? TransactionRef { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        public bool IsVoided { get; set; } = false;

        public DateTime? VoidedAt { get; set; }

        [MaxLength(100)]
        public string? VoidedBy { get; set; }

        [MaxLength(300)]
        public string? VoidReason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Booking? Booking { get; set; }
        public Order? Order { get; set; }
    }
}