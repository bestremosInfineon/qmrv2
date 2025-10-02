using Oracle.ManagedDataAccess.Client;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contexts;

namespace QMRv2.Repository.Contracts
{
    public class LogsServices : ILogsServices
    {
        private readonly IConfiguration _configuration;

        public LogsServices(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task InsertTblDebugger(TblDebugger param)
        {
            var var4 = $"Message : {param.Var4?.Message.Replace("\'", "\"")} StackTrace : {(string.IsNullOrEmpty(param.Var4?.StackTrace) ? string.Empty : param.Var4?.StackTrace.Replace("\'", "\""))}";
            using (OracleConnection connDebug = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                connDebug.Open();
                OracleTransaction oracleTransaction = connDebug.BeginTransaction();
                OracleCommand command1 = new OracleCommand($"INSERT INTO MRB_TBLDEBUGGER values ('{param.Var1}','{param.Var2}','{param.Var3}','{var4}')", connDebug);
                await command1.ExecuteNonQueryAsync();
                await oracleTransaction.CommitAsync();
            }
        }

        public async Task LogsTrace(Logs logs)
        {
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();

                    string sql = "INSERT INTO QMR_LOT_TRACE_LOGS (ID, MESSAGE, REFERENCE, REQUEST_DATA, RESPONSE_DATA, VERB, RESPONSE_CODE, INSERT_DATE) VALUES (:p_ID, :p_MESSAGE, :p_REFERENCE, :p_REQUEST_DATA, :p_RESPONSE_DATA, :p_VERB, :p_RESPONSE_CODE, CURRENT_TIMESTAMP)";

                    // Create an OracleCommand object to execute the SQL statement
                    OracleCommand cmd = new OracleCommand(sql, conn);

                    // Add the parameters to the command
                    cmd.Parameters.Add("p_ID", OracleDbType.Varchar2).Value = Guid.NewGuid().ToString();
                    cmd.Parameters.Add("p_MESSAGE", OracleDbType.Varchar2).Value = logs.Message ?? string.Empty;
                    cmd.Parameters.Add("p_REFERENCE", OracleDbType.Varchar2).Value = logs.Reference ?? string.Empty;
                    cmd.Parameters.Add("p_REQUEST_DATA", OracleDbType.Clob).Value = logs.RequestData;
                    cmd.Parameters.Add("p_RESPONSE_DATA", OracleDbType.Clob).Value = logs.ResponseData;
                    cmd.Parameters.Add("p_VERB", OracleDbType.Varchar2).Value = logs.Verb ?? string.Empty;
                    cmd.Parameters.Add("p_RESPONSE_CODE", OracleDbType.Varchar2).Value = logs.ResponseCode ?? string.Empty;


                    await cmd.ExecuteNonQueryAsync();
                    cmd.Parameters.Clear();

                    conn.Close();
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "LOGSTRACE",
                    Var2 = "Error occured!",
                    Var4 = err
                };
                await InsertTblDebugger(debug);
            }
        }
    }
}
