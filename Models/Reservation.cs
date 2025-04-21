namespace ProductReservationApi.Models
{
    public class Reservation
    {
        public string ProductCode { get; set; }
        public string Quantity { get; set; }
        public string OrderType { get; set; }
        public string OrderNumber { get; set; }
    }
}
