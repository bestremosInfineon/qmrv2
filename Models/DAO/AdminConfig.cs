using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;

namespace QMRv2.Models.DAO
{
    [Keyless]
    public class AdminConfig
    {
        [JsonPropertyName("Id")]
        public string ID { get; set; }
        [JsonPropertyName("Name")]
        public string? NAME { get; set; }
        [JsonPropertyName("Description")]
        public string? DESCRIPTION { get; set; }
        [JsonPropertyName("CreatedDate")]
        public string? CREATED_AT { get; set; }
        [JsonPropertyName("Remarks")]
        public string? REMARKS { get; set; }
        [JsonPropertyName("SqlQuery")]
        public string? SQLQUERY { get; set; }
    }
}
