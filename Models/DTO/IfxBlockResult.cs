namespace QMRv2.Models.DTO
{
    public class IfxBlockResult
    {
        public string? Id { get; set; }
        public string? TransferID { get; set; }
        public string? CaseNumber { get; set; }
        public string? LotNumber { get; set; }
        public string? LotCount { get; set; }
        public bool IsBlocked { get; set; }
        public string? CaseManager { get; set; }
        public string? BlockingReason { get; set; }
        public string? Split { get; set; }
        public string? BlockDate { get; set; }
        public string? Disposition { get; set; }
        public string DispositionMap { get; set; }
        public string SPName { get; set; }
        public bool Toggle { get; set; }
    }
}
