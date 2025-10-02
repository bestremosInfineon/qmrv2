using Ingres.Client;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;

namespace QMRv2.Repository.Contexts
{
    public interface IIngresServices
    {
        Task<List<CaseInfo>> SearchIngresLots(IngresModels model);
        Task<List<CaseInfo>> SearchIngres(IngresModels model);
        Task DeleteLotListStg(IngresModels model, IngresConnection connIngres, IngresTransaction trans);
        Task InsertLotListStgWithSplit(IngresModels model, IngresConnection connIngres, IngresTransaction trans);
        Task InsertLotListStgNoSplit(IngresModels model, IngresConnection connIngres, IngresTransaction trans);
        Task ExecuteProcedures(IngresModels model, IngresConnection connIngres, IngresTransaction trans);
        Task<List<CaseInfo>> GetWIP(IngresModels model, IngresConnection connIngres);
    }
}
