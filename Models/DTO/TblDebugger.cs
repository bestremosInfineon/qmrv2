using Microsoft.EntityFrameworkCore;

namespace QMRv2.Models.DTO
{
    public class TblDebugger
    {

        public string? Var1 { get; set; }
        public string? Var2 { get; set; }
        public string Var3 { get; set; } = DateTime.Now.ToString();
        public Exception? Var4 { get; set; }
    }
}
