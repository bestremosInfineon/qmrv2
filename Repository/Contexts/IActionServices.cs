using QMRv2.Models.DTO;


namespace v2.Repository.Contexts
{
    public interface IActionServices
    {

        Task<bool> InsertResults(LotResultsDetails item, string caseNumber, string transferID);
        Task BulkUpdateRequestProcessingTrace(string transferId, string Case, string lotNumber);
        Task UpdateRequestProcessingBlock(string transferId, string Case, string lotNumber, string value);
        Task<int> InsertRequestTrace(IfxResult data);
        Task<int> InsertRequestBlock(IfxBlockResult data);
        Task<int> InsertRequestDispo(IfxBlockResult data);
        Task UpdateRequestStatusTrace(ActionModel models);
        Task UpdateRequestStatusTraceBatch(ActionModel models);
        Task UpdateRequestStatusBlock(ActionModel model);
        Task UpdateBeginTracing(ActionModel request);
        Task UpdateEndTracing(ActionModel request);
        Task UpdateRequestSentStatusTrace(ActionModel models);
        Task UpdateResultsIsSent(ActionModel head, LotResultsDetails details);
        Task UpdateRequestStatusPerennialBatch(ActionModel models);
        Task UpdateRequestResetLots(ActionModel model);
        Task UpdateRequestResetIsSentLots(ActionModel model);
        Task UpdateRequestStatusDisposition(ActionModel model);
        Task UpdateRequestEmailSent(ActionModel models);
    }
}
