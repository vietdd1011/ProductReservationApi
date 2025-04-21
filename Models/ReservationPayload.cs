namespace ProductReservationApi.Models
{
    public class ReservationPayload
    {
        public List<Reservation> Reservations { get; set; } = new();
    }
}
