using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Dash_DayTrip_API.Models
{
    [Table("Forms")]
    public class Form
    {
        [Key]
        public string FormId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(20)]
        public string Status { get; set; } = "draft"; // active, draft, archived

        public bool IsDefault { get; set; }
        public int SubmissionCount { get; set; }

        // Branding
        public string? LogoUrl { get; set; }

        [MaxLength(255)]
        public string? LogoName { get; set; }

        [MaxLength(500)]
        public string? BrandingSubtitle { get; set; }

        [MaxLength(1000)]
        public string? BrandingDescription { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties - all ignored for Swagger schema generation
        [JsonIgnore]
        public virtual FormSettings? FormSettings { get; set; }

        [JsonIgnore]
        public virtual ICollection<Package>? Packages { get; set; }

        [JsonIgnore]
        public virtual ICollection<Order>? Orders { get; set; }

        public bool IsDeleted { get; set; }
    }
}