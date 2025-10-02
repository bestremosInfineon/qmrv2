using QMRv2.Models.Enums;

namespace QMRv2.Models.DAO
{
    public class Lot
    {
        [System.Text.Json.Serialization.JsonPropertyName("LotNumber")]
        public string? LotNumber { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("ProductionLine")]
        public string? ProductionLine { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("Quantity")]
        public int Quantity { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("InterimDecision")]
        public string? InterimDecision { get; set; }
        public DateTime? InterimInitDate { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("FinalDecision")]
        public string? FinalDecision { get; set; }
        public DateTime? FinalInitDate { get; set; }
        [System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
        public Status Status { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("LotStatus")]
        public string? LotStatus { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("Location")]
        public string? Location { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("Remarks")]
        public string? Remarks { get; set; }
    }
}
