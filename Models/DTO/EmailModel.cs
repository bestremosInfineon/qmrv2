namespace QMRv2.Models.DTO
{
    public class EmailModel
    {
        public string? TransferID { get; set; }
        public string? CaseNumber { get; set; }
        public string? LotNumber { get; set; }
        public string? OriginalLotNumber { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime BeginTracing { get; set; }
        public string? Device { get; set; }
    }
}
