using QMRv2.Models.DAO;
using QMRv2.Models.DTO;

namespace QMRv2.Repository.Contexts
{
    public interface IBlockServices
    {
        Task<string> InsertBlockRequests(LotRequest query);
        Task RunBlockJob();
        Task BlockingProcess(IfxBlockResult cases);
        Task<bool> ExecuteBlockIngres(IfxBlockResult blockModel);
        Task<List<IfxBlockResult>> GetLotListBlock();
        Task<bool> IfQMRExistsBlock(string caseNo, string lotNo, string transferID);
    }
}
