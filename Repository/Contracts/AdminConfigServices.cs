using Microsoft.EntityFrameworkCore;
using QMRv2.DBContext;
using QMRv2.Models.DAO;
using QMRv2.Repository.Contexts;

namespace QMRv2.Repository.Contracts
{
    public class AdminConfigServices : IAdminConfigServices
    {
        private readonly AppDBContext _dbContext;
        public AdminConfigServices( AppDBContext dBContext) 
        {
            _dbContext = dBContext; 
        }

        public async Task<List<AdminConfig>> GetConfiguration()
        {
            return await _dbContext.MRB_ADMIN_CONFIG.Where(q => q.ID.Equals("9")).ToListAsync();
        }
    }
}
