using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Dash_DayTrip_API.Models
{
    [Table("FormSettings")]
    public class FormSettings
    {
        [Key]
        public int SettingId { get; set; }

        [Required]
        public string FormId { get; set; } = string.Empty;

        // Staff Settings (stored as JSON)
        public string? SalesExecutives { get; set; }

        // General Settings
        public string? TaxIdNumber { get; set; }

        public string? Currency { get; set; }

        public string? NextDayCutoffTime { get; set; }

        public int? MaxGuestPerDay { get; set; }

        // Fees & Invoicing Settings
        [MaxLength(20)]
        public string? DepositMode { get; set; } = "fixed"; // per_pax or fixed

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DepositAmount { get; set; } = 100.00m;

        public bool SSTEnabled { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? SSTPercentage { get; set; } = 8.00m;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("FormId")]
        [JsonIgnore]  // Added to break circular reference for Swagger
        public virtual Form? Form { get; set; }

        public bool IsDeleted { get; set; }
    }
}