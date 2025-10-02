using QMRv2.Models.Enums;

namespace QMRv2.Models.DTO
{
    public class ActionModel
    {
        public string? CaseNumber { get; set; }
        public string? TransferId { get; set; }
        public string? LotNumber { get; set; }
        public string? LotTraceOrigin { get; set; }

        public RequestStatus Status { get; set; }
        public SentStatus SentStatus { get; set; }
        public string? ID { get; set; }
    }
}
