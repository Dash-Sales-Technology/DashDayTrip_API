using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Dash_DayTrip_API.Models
{
    [Table("Bookings")]
    public class DayTripBooking
    {
        [Key]
        public string BookingId { get; set; } = string.Empty;
        
        public string FormId { get; set; } = string.Empty;
        public string MerchantId { get; set; } = string.Empty;
        public string ReferenceNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        
        // Customer Information
        public string? SalesExecutive { get; set; }
        public string? Salutation { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Nationality { get; set; }
        public string CountryCode { get; set; } = "+60";
        public string Phone { get; set; } = string.Empty;
        
        // Travel Information
        public DateTime? TravelDate { get; set; }
        
        // Payment Information
        public string? PaymentMethod { get; set; }
        public string? TransactionRef { get; set; }
        
        // Financial Totals
        public decimal Subtotal { get; set; }
        public decimal TotalBoatFare { get; set; }
        public decimal TotalGratuity { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal DepositPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string? DepositMode { get; set; }
        
        // Remarks
        public string? TravelDateRemarks { get; set; }
        public string? AwardRemarks { get; set; }
        public string? Notes { get; set; }
        public string? PaymentReceipt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        
        // Navigation properties
        public virtual ICollection<BookingPackage>? BookingPackages { get; set; }
    }
}
