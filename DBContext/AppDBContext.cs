using Microsoft.EntityFrameworkCore;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using System.Reflection.Emit;

namespace QMRv2.DBContext
{
    public class AppDBContext : DbContext
    {
        public AppDBContext(DbContextOptions<AppDBContext> options)
        : base(options)
        { }

        public DbSet<AdminConfig> MRB_ADMIN_CONFIG { get; set; }


        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<TblDebugger>(entity =>
            {
                entity.HasNoKey();
            });
        }
    }
}
