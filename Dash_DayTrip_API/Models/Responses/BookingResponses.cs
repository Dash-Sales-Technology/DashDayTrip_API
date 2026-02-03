namespace Dash_DayTrip_API.Models.Responses
{
    public class BookingCreateResponse : ApiResponse
    {
        public DayTripBooking? Booking { get; set; }
        public int PackagesCount { get; set; }
        public List<string> GeneratedIds { get; set; } = new();
    }

    public class BookingUpdateResponse : ApiResponse
    {
        public DayTripBooking? Booking { get; set; }
        public int PackagesCount { get; set; }
        public int PackagesAdded { get; set; }
        public int PackagesUpdated { get; set; }
        public int PackagesDeleted { get; set; }
    }

    public class BookingDeleteResponse : ApiResponse
    {
        public string BookingId { get; set; } = string.Empty;
        public int PackagesDeleted { get; set; }
    }

    public class BookingStatusUpdateResponse : ApiResponse
    {
        public string BookingId { get; set; } = string.Empty;
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class BookingListResponse : ApiResponse
    {
        public List<DayTripBooking> Bookings { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? AppliedFilters { get; set; }
    }
}