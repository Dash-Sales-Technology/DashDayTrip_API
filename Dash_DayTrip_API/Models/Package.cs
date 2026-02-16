using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Dash_DayTrip_API.Models
{
    [Table("Packages")]
    public class Package
    {
        [Key]
        public string PackageId { get; set; } = string.Empty;

        [Required]
        public string FormId { get; set; } = string.Empty;

        [Required]
        public string MerchantId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string PackageName { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        public int NoOfPax { get; set; } = 1;
        public bool Availability { get; set; } = true;

        // Boat Fare
        public bool BoatFareEnabled { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? BoatFareAmount { get; set; }

        [MaxLength(20)]
        public string? BoatFareCalcType { get; set; } // per_pax or per_unit

        // Gratuity
        public bool GratuityEnabled { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal? GratuityAmount { get; set; }

        [MaxLength(20)]
        public string? GratuityCalcType { get; set; } // per_pax or per_unit

        // Deposit Configuration 
        [Column(TypeName = "decimal(10,2)")]
        public decimal? DepositAmount { get; set; }

        [MaxLength(20)]
        public string? DepositMode { get; set; } // per_pax or per_package

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        [ForeignKey("FormId")]
        [JsonIgnore]
        public virtual Form? Form { get; set; }
        public bool IsDeleted { get; set; }
    }
}