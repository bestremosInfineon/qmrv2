namespace QMRv2.Models.DTO
{
    public class LotResults
    {
        public string? CaseNumber { get; set; }
        public string? TransferID { get; set; }
        public int TransferCount { get; set; }
        public string? ActionCode { get; set; }
        public List<LotResultsDetails>? LotList { get; set; }
        public string? ApiKey { get; set; }
    }
}
