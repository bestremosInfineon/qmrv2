using QMRv2.Models.DAO;

namespace QMRv2.Repository.Contexts
{
    public interface IAdminConfigServices
    {
        Task<List<AdminConfig>> GetConfiguration();
    }
}
