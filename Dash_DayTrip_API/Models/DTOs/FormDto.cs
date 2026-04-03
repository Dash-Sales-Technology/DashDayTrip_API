namespace Dash_DayTrip_API.Models.DTOs
{
    public class FormDto
    {
        public int FormId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = "draft";
        public bool IsDefault { get; set; }
        public int SubmissionCount { get; set; }
        public string? LogoUrl { get; set; }
        public string? LogoName { get; set; }
        public string? BrandingSubtitle { get; set; }
        public string? BrandingDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public FormSettingsDto? FormSettings { get; set; }
    }

    public class FormSettingsDto
    {
        public int SettingId { get; set; }
        public int FormId { get; set; }
        public string? SalesExecutives { get; set; }
        public string? TaxIdNumber { get; set; }
        public string? Currency { get; set; }
        public string? NextDayCutoffTime { get; set; }
        public int? MaxGuestPerDay { get; set; }
        public string? DepositMode { get; set; }
        public decimal? DepositAmount { get; set; }
        public bool SSTEnabled { get; set; }
        public decimal? SSTPercentage { get; set; }
        public decimal? BookingGratuityAmount { get; set; }   
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}