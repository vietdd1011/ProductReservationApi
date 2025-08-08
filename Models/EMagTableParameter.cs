namespace ProductReservationApi.Models
{
    public class EMagTableParameter
    {
        public string Id { get; set; } = "";
        public string Description { get; set; } = "";
        public string FieldType { get; set; } = "";
        public List<string> SelectOptions { get; set; } = new List<string>();
        public string InputValue { get; set; } = "";
    }

}
