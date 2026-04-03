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
        public int FormId { get; set; }

        public string? SalesExecutives { get; set; }

        public string? TaxIdNumber { get; set; }

        public string? Currency { get; set; }

        public string? NextDayCutoffTime { get; set; }

        public int? MaxGuestPerDay { get; set; }

        [MaxLength(20)]
        public string? DepositMode { get; set; } = "fixed";

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DepositAmount { get; set; } = 100.00m;

        public bool SSTEnabled { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal? SSTPercentage { get; set; } = 8.00m;

        public string? GiftItems { get; set; }

        // Per-pax gratuity charged at booking time
        [Column(TypeName = "decimal(10,2)")]
        public decimal? BookingGratuityAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("FormId")]
        [JsonIgnore]
        public virtual Form? Form { get; set; }

        public bool IsDeleted { get; set; }
    }
}