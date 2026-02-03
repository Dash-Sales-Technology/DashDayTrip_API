namespace Dash_DayTrip_API.Models.Responses
{
    public class PackageCreateResponse : ApiResponse
    {
        public Package? Package { get; set; }
        public decimal CalculatedTotalPrice { get; set; }
    }

    public class PackageUpdateResponse : ApiResponse
    {
        public Package? Package { get; set; }
        public int AffectedBookingsCount { get; set; }
        public List<string> Warnings { get; set; } = new();
    }

    public class PackageDeleteResponse : ApiResponse
    {
        public string PackageId { get; set; } = string.Empty;
        public int BookingsAffected { get; set; }
    }

    public class PackageListResponse : ApiResponse
    {
        public List<Package> Packages { get; set; } = new();
        public int TotalCount { get; set; }
        public int AvailableCount { get; set; }
        public int UnavailableCount { get; set; }
    }
}