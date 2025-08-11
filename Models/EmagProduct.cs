using System.Text.Json.Serialization;

namespace ProductReservationApi.Models
{
    public class EmagProductsResponse
    {
        [JsonPropertyName("products")]
        public List<EmagProduct> Products { get; set; } = new List<EmagProduct>();
    }

    public class EmagProduct
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }
        [JsonPropertyName("params")]
        public List<EmagProductParams> Params { get; set; } = new List<EmagProductParams>();
    }

    public class  EmagProductParams
    {
        [JsonPropertyName("param")]
        public string Param { get; set; } = "";
        [JsonPropertyName("value")]
        public string Value { get; set; } = "";
    }
}
