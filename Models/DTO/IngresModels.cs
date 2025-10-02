using System.Text.Json.Serialization;

namespace QMRv2.Models.DTO
{
    public class IngresModels
    {
        [JsonPropertyName("TransferID")]
        public string? TransferID { get; set; }
        [JsonPropertyName("QmrNumber")]
        public string? CaseNumber { get; set; }
        [JsonPropertyName("LotNumber")]
        public string? LotNumber { get; set; }
        [JsonPropertyName("Split")]
        public string? Split { get; set; }
        [JsonPropertyName("IncludeParent")]
        public string? IncludeParent { get; set; }
        [JsonPropertyName("OriginLotNumber")]
        public string? OriginLotNumber { get; set; }
    }
}
