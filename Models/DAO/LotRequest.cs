using QMRv2.Models.DTO;
using System.ComponentModel.DataAnnotations;

namespace QMRv2.Models.DAO
{
    public class LotRequest
    {
        [Required(AllowEmptyStrings = false, ErrorMessage = "Please input value on required fields.")]
        public string? QmrNumber { get; set; }

        [Required(AllowEmptyStrings = false, ErrorMessage = "Please input value on required fields.")]
        public string? TransferID { get; set; }
        public int TransferCount { get; set; }
        public string? Status { get; set; }
        public string? ActionCode { get; set; }
        public string? CaseManager { get; set; }
        public string? BlockingReason { get; set; }
        public string? DeviationID { get; set; }

        public List<IfxLot>? LotList { get; set; }
        public string? ApiKey { get; set; }
    }
}
