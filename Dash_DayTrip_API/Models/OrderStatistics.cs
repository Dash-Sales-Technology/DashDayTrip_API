namespace Dash_DayTrip_API.Models.Responses
{
    public class OrderStatistics
    {
        public int TotalOrders { get; set; }
        public decimal TotalRevenue { get; set; }
        public decimal TotalDeposits { get; set; }
        public decimal TotalBalance { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal DailySales { get; set; }
        public int ConfirmedOrders { get; set; }
        public int PendingOrders { get; set; }
        public int CancelledOrders { get; set; }
    }
}
