namespace Dash_DayTrip_API.Models.Responses
{
    public class OrderCreateResponse : ApiResponse
    {
        public Order? Order { get; set; }
        public int PackagesCount { get; set; }
        public List<string> GeneratedIds { get; set; } = new();
    }

    public class OrderUpdateResponse : ApiResponse
    {
        public Order? Order { get; set; }
        public int PackagesCount { get; set; }
        public int PackagesAdded { get; set; }
        public int PackagesUpdated { get; set; }
        public int PackagesDeleted { get; set; }
    }

    public class OrderDeleteResponse : ApiResponse
    {
        public int OrderId { get; set; }
        public int PackagesDeleted { get; set; }
    }

    public class OrderStatusUpdateResponse : ApiResponse
    {
        public int OrderId { get; set; }
        public string PreviousStatus { get; set; } = string.Empty;
        public string NewStatus { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class OrderListResponse : ApiResponse
    {
        public List<Order> Orders { get; set; } = new();
        public int TotalCount { get; set; }
        public int PageNumber { get; set; }
        public int PageSize { get; set; }
        public string? AppliedFilters { get; set; }
    }

    public class OrderPaymentStatusUpdateResponse : ApiResponse
    {
        public List<int> OrderIds { get; set; } = new();
        public string NewPaymentStatus { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    public class OrderPaymentItemResponse
    {
        public int OrderPaymentId { get; set; }
        public int OrderId { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string? PaymentMethod { get; set; }
        public string? TransactionRef { get; set; }
        public string? ReceiptUrl { get; set; }
        public string? Source { get; set; }
        public string? Notes { get; set; }
        public string? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsVoided { get; set; }
        public DateTime? VoidedAt { get; set; }
        public string? VoidedBy { get; set; }
        public string? VoidReason { get; set; }
    }

    public class OrderPaymentCreateResponse : ApiResponse
    {
        public int OrderId { get; set; }
        public int PaymentId { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
    }

    public class OrderPaymentListResponse : ApiResponse
    {
        public int OrderId { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal BalanceDue { get; set; }
        public string PaymentStatus { get; set; } = string.Empty;
        public List<OrderPaymentItemResponse> Payments { get; set; } = new();
    }

}