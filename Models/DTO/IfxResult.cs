namespace QMRv2.Models.DTO
{
    public class IfxResult
    {
        public string? Id { get; set; }
        public string? TransferID { get; set; }
        public string? CaseNumber { get; set; }
        public string? LotNumber { get; set; }
        public string? LotCount { get; set; }
        public bool Completed { get; set; }
        public DateTime BeginTracing { get; set; }
        public DateTime EndTracing { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Flag { get; set; }
        public string? Username { get; set; }
    }
}
