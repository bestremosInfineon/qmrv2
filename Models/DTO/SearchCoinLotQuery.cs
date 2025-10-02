namespace QMRv2.Models.DTO
{
    public class SearchCoinLotQuery
    {
        public int Id { get; set; }
        public string? CaseNumber { get; set; }
        public string? LotNumber { get; set; }
        public string? SearchType { get; set; }
        public int IsRetracing { get; set; }
        public int LotNumberCount { get; set; }
        public string? UserId { get; set; }
        public int UserActionId { get; set; }
        public string? TransferID { get; set; }
        public string? ApiKey { get; set; }
    }
}
