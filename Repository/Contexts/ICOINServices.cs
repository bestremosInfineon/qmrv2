using Oracle.ManagedDataAccess.Client;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;

namespace QMRv2.Repository.Contexts
{
    public interface ICOINServices
    {
        Task<string> GetIngresPL(SearchCoinModel model, string device);
        Task<string> GetIngresSiteName(SearchCoinModel model, string siteCode);
        Task<string> GetIngresCmsMrbNumber(SearchCoinModel model, string lotNumber);
        Task<string> GetParentChildMapping(string lotno);
        Task<List<string>> GetRelatedLot(string tableName, string lot, OracleConnection conn, List<string> data);
        Task<List<CaseInfo>> SearchCOINLots(string lotno, string mrbcaseno);
        Task<List<CaseInfo>> CoinResultMapping(string query, SearchCoinModel model, string order);
        Task<string> GetIngressParentLot(string lotNo);
        Task<List<string>> GetLotListByCaseNumberTrace(string caseNo, string transferID);
        Task<bool> IfQMRExistsTrace(string caseNo, string lotNo, string transferID);
        Task<List<IfxResult>> GetLotListTraceDefault(int requestCount);
        Task<List<ActionModel>> GetUnsentResultsHead();
        Task<List<LotResultsDetails>> GetUnsentResultsDetails(ActionModel models);
        Task<bool> IfQMRResultsExistsCOIN(ActionModel model);
        Task<List<IfxResult>> GetStuckLots();
        Task<string> GetRequestCount();
        Task<List<IfxResult>> GetLotsWithUnchangedStatusDoneTracing();
        Task<List<IfxResult>> GetLotListTraceUsers(int requestCount);
        Task<int> CheckIfMailSent(SearchCoinLotQuery details);
        Task<bool> VerifyLotExisting(string lot);
        Task<string> GetIfxPl(string device);
    }
}
