using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dash_DayTrip_API.Models
{
    [Table("Orders")]
    public class Order
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int OrderId { get; set; }

        [Required]
        public int FormId { get; set; }

        [Required]
        [MaxLength(50)]
        public string MerchantId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string ReferenceNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        // Customer Information
        [MaxLength(100)]
        public string? SalesExecutive { get; set; }

        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Nationality { get; set; }

        [Required]
        [MaxLength(10)]
        public string CountryCode { get; set; } = "+60";

        [Required]
        [MaxLength(50)]
        public string Phone { get; set; } = string.Empty;

        // Travel Information
        public DateTime? TravelDate { get; set; }

        // Payment Information (latest non-voided payment snapshot)
        [MaxLength(50)]
        public string? PaymentMethod { get; set; }

        [MaxLength(100)]
        public string? TransactionRef { get; set; }

        // Financial Totals
        [Column(TypeName = "decimal(10,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalBoatFare { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal TotalGratuity { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal GrandTotal { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal DepositPaid { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal AmountPaid { get; set; } = 0m;

        [Column(TypeName = "decimal(10,2)")]
        public decimal BalanceDue { get; set; }

        [MaxLength(20)]
        public string? DepositMode { get; set; }

        [Required]
        [MaxLength(20)]
        public string Source { get; set; } = "order";

        // Remarks
        public string? TravelDateRemarks { get; set; }
        public string? AwardRemarks { get; set; }
        public string? Notes { get; set; }
        public string? PaymentReceipt { get; set; }

        // Ledger-derived payment state
        [Required]
        [MaxLength(20)]
        public string PaymentStatus { get; set; } = "Pending";

        [MaxLength(50)]
        public string? AssignedSEUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }

        // Invoice tracking
        public DateTime? InvoiceSentAt { get; set; }

        // Navigation properties
        public virtual ICollection<OrderPackage>? OrderPackages { get; set; }
        public virtual ICollection<OrderPayment>? OrderPayments { get; set; }

        [InverseProperty("Order")]
        public virtual Promotion? Promotion { get; set; }
    }
}