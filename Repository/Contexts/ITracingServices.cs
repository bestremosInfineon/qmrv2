using QMRv2.Models.DAO;
using QMRv2.Models.DTO;

namespace QMRv2.Repository.Contexts
{
    public interface ITracingServices
    {
        Task<LotRequest> InsertRequests(LotRequest query);
        Task RunTraceJob();
        Task TracingProcessInsert(IfxResult cases);
        Task<List<LotResultsDetails>> LotTrace(SearchCoinLotQuery param);
        Task<string> SendToIFX(ActionModel headDetails, List<LotResultsDetails> childDetails, string action);
        Task SendTraceResults();
        Task CancelPerennialLots(string var1, string var2, string var3);
        Task CancelPerennialLotsBatch(string var1, string var2);
        Task CancelFailedLots(string var1, string var2);
        Task MissingPLEmail(List<LotResultsDetails> lotResult, SearchCoinLotQuery query);
        Task TerminateProcess(SearchCoinLotQuery query, Exception errorMessage);
        Task UpdateReqeustResetSentLots();
        Task UpdateStuckLots();
    }
}
