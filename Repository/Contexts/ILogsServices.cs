using QMRv2.Models.DTO;

namespace QMRv2.Repository.Contexts
{
    public interface ILogsServices
    {
        Task InsertTblDebugger(TblDebugger tblDebugger);
        Task LogsTrace(Logs logs);
    }
}
