using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace Dash_DayTrip_API.Models
{
    [Table("Promotions")]
    public class Promotion
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int PromotionId { get; set; }

        [Required]
        public int OrderId { get; set; }

        [MaxLength(100)]
        public string? VoucherCode { get; set; }

        [MaxLength(20)]
        public string? DiscountType { get; set; } // 'Amount' or 'Percentage'

        [Column(TypeName = "decimal(10,2)")]
        public decimal? DiscountValue { get; set; }

        public bool AwardRemarkEnabled { get; set; }

        public string? AwardRemark { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public bool IsDeleted { get; set; }

        // Navigation property
        [ForeignKey("OrderId")]
        [JsonIgnore]
        public virtual Order? Order { get; set; }
    }
}
