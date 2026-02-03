namespace Dash_DayTrip_API.Models.Responses
{
    // Base class for all successful responses
    public class ApiResponse
    {
        public bool Success { get; set; } = true;
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    // Base class for all error responses
    public class ApiErrorResponse
    {
        public bool Success { get; set; } = false;
        public string Error { get; set; } = string.Empty;
        public string? Field { get; set; }
        public Dictionary<string, string[]>? ValidationErrors { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}