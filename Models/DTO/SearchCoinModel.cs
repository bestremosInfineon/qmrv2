using Oracle.ManagedDataAccess.Client;

namespace QMRv2.Models.DTO
{
    public class SearchCoinModel
    {
        public OracleConnection OracleConnection { get; set; }
        public string? LotNumbers { get; set; }
        public string? LotNumbersDC { get; set; }
        public string? LotNumber { get; set; }
        public string? ShippedCustomer { get; set; }
        public string? OriginalLot { get; set; }
        public string? CaseNumber { get; set; }
        public string? TransferID { get; set; }
    }
}
