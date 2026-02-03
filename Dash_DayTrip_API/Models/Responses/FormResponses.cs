namespace Dash_DayTrip_API.Models.Responses
{
    public class FormCreateResponse : ApiResponse
    {
        public Form? Form { get; set; }
        public bool SettingsCreated { get; set; }
        public int PackagesCount { get; set; }
    }

    public class FormUpdateResponse : ApiResponse
    {
        public Form? Form { get; set; }
        public List<string> Warnings { get; set; } = new();
        public int AffectedBookingsCount { get; set; }
    }

    public class FormDeleteResponse : ApiResponse
    {
        public string FormId { get; set; } = string.Empty;
        public int BookingsDeleted { get; set; }
        public int PackagesDeleted { get; set; }
    }

    public class FormStatusUpdateResponse : ApiResponse
    {
        public string FormId { get; set; } = string.Empty;
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public int AffectedBookingsCount { get; set; }
    }

    public class FormListResponse : ApiResponse
    {
        public List<Form> Forms { get; set; } = new();
        public int TotalCount { get; set; }
        public int ActiveCount { get; set; }
        public int DraftCount { get; set; }
    }
}