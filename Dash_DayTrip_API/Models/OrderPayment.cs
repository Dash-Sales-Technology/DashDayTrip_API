using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dash_DayTrip_API.Models
{
    [Table("OrderPayments")]
    public class OrderPayment
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderPaymentId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        [Range(typeof(decimal), "0.01", "999999999.99", ErrorMessage = "Amount must be greater than 0.")]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? TransactionRef { get; set; }

        public string? ReceiptUrl { get; set; }

        [Required]
        [MaxLength(30)]
        public string Source { get; set; } = "manual";

        [MaxLength(500)]
        public string? Notes { get; set; }

        [MaxLength(50)]
        public string? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public bool IsVoided { get; set; } = false;
        public DateTime? VoidedAt { get; set; }

        [MaxLength(50)]
        public string? VoidedBy { get; set; }

        [MaxLength(500)]
        public string? VoidReason { get; set; }

        [ForeignKey(nameof(OrderId))]
        public virtual Order? Order { get; set; }
    }
}