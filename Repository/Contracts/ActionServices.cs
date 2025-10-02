using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contexts;
using System.Data;
using v2.Repository.Contexts;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace QMRv2.Repository.Contracts
{
    public class ActionServices : IActionServices
    {
        private readonly IConfiguration _configuration;
        private readonly ILogsServices _logsServices;
        public ActionServices(IConfiguration configuration, ILogsServices logsServices)
        {
            _configuration = configuration;
            _logsServices = logsServices;
        }

        public async Task<bool> InsertResults(LotResultsDetails item, string caseNumber, string transferID)
        {
            try
            {
                if (item != null)
                {
                    using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                    {
                        conn.Open();
                        OracleCommand cmd = new OracleCommand
                        {
                            Connection = conn,
                            CommandText = "PROD.MRB_INSERT_IFX_LOT_RESULT",
                            BindByName = true,
                            CommandType = CommandType.StoredProcedure
                        };

                        cmd.Parameters.Add("P_TRANSFER_ID", "varchar2").Value = transferID;
                        cmd.Parameters.Add("P_MRBCASENO", "varchar2").Value = caseNumber;
                        cmd.Parameters.Add("P_LOT_NO", "varchar2").Value = item.LotNumber?.Trim();
                        cmd.Parameters.Add("P_PARENTLOT", "varchar2").Value = item.ParentLotNumber?.Trim();
                        cmd.Parameters.Add("P_MFG_SITE_CODE", "varchar2").Value = item.MfgSiteCode?.Trim();
                        cmd.Parameters.Add("P_MFG_AREA", "varchar2").Value = item.MfgArea?.Trim();
                        cmd.Parameters.Add("P_CURRENT_QTY", "varchar2").Value = item.Quantity?.Trim();
                        cmd.Parameters.Add("P_DEVICE", "varchar2").Value = item.Device?.Trim();
                        cmd.Parameters.Add("P_PKG_CODE", "varchar2").Value = item.PackageCode?.Trim();
                        cmd.Parameters.Add("P_PL", "varchar2").Value = item.ProductionLine?.Trim();
                        cmd.Parameters.Add("P_LOT_TRACE_ORIGIN", "varchar2").Value = item.LotTraceOrigin?.Trim();
                        cmd.Parameters.Add("P_ORIGINAL_LOT_NO", "varchar2").Value = item.OrigLotNumber?.Trim();
                        cmd.Parameters.Add("P_SHIP_LOC", "varchar").Value = item.ShipLoc?.Trim();
                        cmd.Parameters.Add("P_SPLIT_NO", "varchar").Value = item.SplitNo?.Trim();
                        cmd.Parameters.Add("P_ID", "varchar").Value = item.ID?.Trim();
                        cmd.Parameters.Add("P_SOURCE", "varchar").Value = item.Source?.Trim();
                        cmd.Parameters.Add("P_IFXLOTNAME", "varchar").Value = item.IfxLotName?.Trim();
                        cmd.ExecuteNonQuery();
                        cmd.Parameters.Clear();

                        conn.Close();
                    }

                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "ActionService_001",
                    Var2 = JsonConvert.SerializeObject(new { item }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return false;
            }
        }

        public async Task BulkUpdateRequestProcessingTrace(string transferId, string Case, string lotNumber)
        {
            var updateString = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET IS_PROCESSED = '2' " +
                                $"WHERE TRANSFER_ID IN ({transferId}) " +
                                $"AND QMRCASENO IN ({Case}) " +
                                $"AND LOT_NO IN ({lotNumber})";
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command1 = new OracleCommand(updateString, conn);
                    command1.Transaction = trans;
                    command1.ExecuteNonQuery();
                    trans.Commit();
                    conn.Close();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_002",
                        Var2 = $"TransferID #{transferId} Case #{Case} Lot #{lotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateRequestProcessingBlock(string transferId, string Case, string lotNumber, string value)
        {
            var updateString = $"UPDATE MRB_QMIFX_LOT_BLOCK_REQUESTS SET IS_BLOCKED = '{value}' " +
                                $"WHERE TRANSFER_ID IN ({transferId}) " +
                                $"AND QMRCASENO IN ({Case}) " +
                                $"AND LOT_NO IN ({lotNumber})";
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command1 = new OracleCommand(updateString, conn);
                    command1.Transaction = trans;
                    command1.ExecuteNonQuery();
                    trans.Commit();
                    conn.Close();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_003",
                        Var2 = $"TransferID #{transferId} Case #{Case} Lot #{lotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task<int> InsertRequestTrace(IfxResult data)
        {
            var retValue = 0;
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                try
                {
                    conn.Open();
                    OracleTransaction transOra;
                    transOra = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                    string query = $"INSERT INTO MRB_QMRIFX_LOT_REQUESTS (ID, TRANSFER_ID, QMRCASENO, LOT_NO, EVENT_TIMESTAMP, IS_PROCESSED, IS_SENT, USERNAME) " +
                                    $"VALUES ('{data.Id}','{data.TransferID}','{data.CaseNumber}', '{data.LotNumber}', CURRENT_TIMESTAMP, '0', '0', '{data.Username}')";
                    OracleCommand command = new OracleCommand(query, conn);
                    command.ExecuteNonQuery();
                    transOra.Commit();
                    conn.Close();

                    return await Task.FromResult(retValue);
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_004",
                        Var2 = $"TransferID #{data.TransferID} Case #{data.CaseNumber} Lot #{data.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    conn.Close();
                    conn.Dispose();
                    throw;
                }
            }

        }

        public async Task<int> InsertRequestBlock(IfxBlockResult data)
        {
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                var retValue = 0;
                try
                {
                    conn.Open();
                    OracleTransaction transOra;
                    transOra = conn.BeginTransaction(IsolationLevel.ReadCommitted);

                    var execString = $"INSERT INTO MRB_QMIFX_LOT_BLOCK_REQUESTS (" +
                    $"ID, TRANSFER_ID, QMRCASENO, LOT_NO, EVENT_TIMESTAMP, CASE_MANAGER, BLOCKING_REASON, IS_BLOCKED) VALUES " +
                    $"('{data.Id}', '{data.TransferID}','{data.CaseNumber}', '{data.LotNumber}', CURRENT_TIMESTAMP, '{data.CaseManager}', '{data.BlockingReason}', '0')";

                    OracleCommand command = new OracleCommand(execString, conn);
                    command.ExecuteNonQuery();
                    transOra.Commit();
                    conn.Close();
                    conn.Dispose();
                    return retValue;
                }

                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_005",
                        Var2 = $"TransferID #{data.TransferID} Case #{data.CaseNumber} Lot #{data.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    conn.Close();
                    conn.Dispose();
                    throw;
                }
            }

        }

        public async Task<int> InsertRequestDispo(IfxBlockResult data)
        {
            var retValue = 0;
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                try
                {
                    conn.Open();
                    OracleTransaction transOra;
                    transOra = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                    string query = $"INSERT INTO MRB_QMIFX_LOT_DISPO_REQUESTS (ID, TRANSFER_ID, QMRCASENO, CASE_MANAGER, LOT_NO, DISPOSITION, EVENT_TIMESTAMP, IS_DISPOSITIONED) " +
                                    $"VALUES ('{data.Id}','{data.TransferID}','{data.CaseNumber}', '{data.CaseManager}', '{data.LotNumber}', '{data.Disposition}', CURRENT_TIMESTAMP, '0')";
                    OracleCommand command = new OracleCommand(query, conn);
                    command.ExecuteNonQuery();
                    transOra.Commit();
                    conn.Close();

                    return retValue;
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_006",
                        Var2 = $"TransferID #{data.TransferID} Case #{data.CaseNumber} Lot #{data.LotNumber}",

                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    conn.Close();
                    conn.Dispose();
                    throw;
                }
            }

        }

        public async Task UpdateRequestStatusTrace(ActionModel models)
        {
            string query = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET IS_PROCESSED = '{(int)models.Status}' " +
                                 $"WHERE QMRCASENO = '{models.CaseNumber}' " +
                                 $"AND TRANSFER_ID = '{models.TransferId}' " +
                                 $"AND LOT_NO = '{models.LotNumber}'";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command = new OracleCommand(query, conn);
                    command.Transaction = trans;
                    command.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_007",
                        Var2 = $"TransferID #{models.TransferId} Case #{models.CaseNumber} Lot #{models.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateRequestStatusTraceBatch(ActionModel models)
        {
            string query = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET IS_PROCESSED = '{(int)models.Status}' " +
                                 $"WHERE QMRCASENO = '{models.CaseNumber}' " +
                                 $"AND TRANSFER_ID = '{models.TransferId}' ";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command = new OracleCommand(query, conn);
                    command.Transaction = trans;
                    command.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_008",
                        Var2 = $"TransferID #{models.TransferId} Case #{models.CaseNumber} Lot #{models.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateRequestStatusBlock(ActionModel model)
        {
            string query = $"UPDATE MRB_QMIFX_LOT_BLOCK_REQUESTS SET IS_BLOCKED = '1', BLOCKED_DATE = CURRENT_TIMESTAMP " +
                                 $"WHERE QMRCASENO = '{model.CaseNumber}' " +
                                 $"AND LOT_NO = '{model.LotNumber}' " +
                                 $"AND TRANSFER_ID = '{model.TransferId}'";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command = new OracleCommand(query, conn);
                    command.Transaction = trans;
                    command.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_009",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateBeginTracing(ActionModel model)
        {
            using (OracleConnection connDebug = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                try
                {
                    connDebug.Open();
                    OracleTransaction oracleTransaction = connDebug.BeginTransaction();
                    string query = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET BEGIN_TRACING = CURRENT_TIMESTAMP " +
                                   $"WHERE TRANSFER_ID = '{model.TransferId}' AND LOT_NO = '{model.LotNumber}' " +
                                   $"AND QMRCASENO = '{model.CaseNumber}'";
                    OracleCommand command1 = new OracleCommand(query, connDebug);
                    command1.ExecuteNonQuery();
                    oracleTransaction.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_010",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    connDebug.Close();
                    connDebug.Dispose();
                }
            }
        }

        public async Task UpdateEndTracing(ActionModel model)
        {
            using (OracleConnection connDebug = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                try
                {
                    connDebug.Open();
                    OracleTransaction oracleTransaction = connDebug.BeginTransaction();
                    string query = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET END_TRACING = CURRENT_TIMESTAMP " +
                                   $"WHERE TRANSFER_ID = '{model.TransferId}' AND LOT_NO = '{model.LotNumber}' " +
                                   $"AND QMRCASENO = '{model.CaseNumber}'";
                    OracleCommand command1 = new OracleCommand(query, connDebug);
                    command1.ExecuteNonQuery();
                    oracleTransaction.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_011",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    connDebug.Close();
                    connDebug.Dispose();
                }
            }
        }

        public async Task UpdateRequestSentStatusTrace(ActionModel model)
        {
            string query = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET IS_SENT = '{(int)model.SentStatus}', SENT_DATE = CURRENT_TIMESTAMP " +
                                 $"WHERE QMRCASENO = '{model.CaseNumber}' " +
                                 $"AND TRANSFER_ID = '{model.TransferId}' " +
                                 $"AND LOT_NO = '{model.LotNumber}'";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command = new OracleCommand(query, conn);
                    command.Transaction = trans;
                    command.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_012",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateResultsIsSent(ActionModel model, LotResultsDetails details)
        {
            string query = $"UPDATE MRB_IFX_LOT_RESULT SET IS_FOUND = '1' " +
                     $"WHERE QMRCASENO = '{model.CaseNumber}' AND TRANSFER_ID = '{model.TransferId}' " +
                     $"AND ID = '{details.ID}' AND LOT_TRACE_ORIGIN = '{details.LotTraceOrigin}' AND (LOT_NO||SPLIT_NO = '{details.LotNumber}' OR LOT_NO = '{details.LotNumber}') ";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command = new OracleCommand(query, conn);
                    command.Transaction = trans;
                    command.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_013",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateRequestStatusPerennialBatch(ActionModel model)
        {
            string query = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET IS_PROCESSED = {(int)model.Status},  IS_SENT = '0' " +
                                 $"WHERE QMRCASENO = '{model.CaseNumber}' " +
                                 $"AND TRANSFER_ID = '{model.TransferId}'" +
                                 $"AND IS_PROCESSED = '2' AND IS_SENT = '0'";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command = new OracleCommand(query, conn);
                    command.Transaction = trans;
                    command.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_014",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateRequestResetLots(ActionModel model)
        {
            string updateString = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET IS_PROCESSED = '0',  IS_SENT = '0', BEGIN_TRACING = NULL, END_TRACING = NULL, SENT_DATE = NULL " +
                                 $"WHERE TRANSFER_ID = '{model.TransferId}' AND QMRCASENO = '{model.CaseNumber}' AND LOT_NO = '{model.LotNumber}' ";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand commandUpdate = new OracleCommand(updateString, conn);
                    commandUpdate.Transaction = trans;
                    commandUpdate.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_015",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateRequestResetIsSentLots(ActionModel model)
        {
            string updateString = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET IS_PROCESSED = '1'" +
                                 $"WHERE TRANSFER_ID = '{model.TransferId}' AND QMRCASENO = '{model.CaseNumber}' AND LOT_NO = '{model.LotNumber}'";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand commandUpdate = new OracleCommand(updateString, conn);
                    commandUpdate.Transaction = trans;
                    commandUpdate.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_016",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateRequestStatusDisposition(ActionModel model)
        {
            string query = $"UPDATE MRB_QMIFX_LOT_DISPO_REQUESTS SET IS_DISPOSITIONED = '1', DISPO_DATE = CURRENT_TIMESTAMP " +
                                 $"WHERE QMRCASENO = '{model.CaseNumber}' " +
                                 $"AND LOT_NO = '{model.LotNumber}' " +
                                 $"AND TRANSFER_ID = '{model.TransferId}'";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command = new OracleCommand(query, conn);
                    command.Transaction = trans;
                    command.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_017",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }

        public async Task UpdateRequestEmailSent(ActionModel model)
        {
            string query = $"UPDATE MRB_QMRIFX_LOT_REQUESTS SET IS_PROCESSED = '{(int)model.Status}', IS_EMAIL_SENT = '1', SENT_EMAIL_DATE = CURRENT_TIMESTAMP " +
                                 $"WHERE QMRCASENO = '{model.CaseNumber}' " +
                                 $"AND TRANSFER_ID = '{model.TransferId}' " +
                                 $"AND LOT_NO = '{model.LotNumber}' ";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                OracleTransaction trans = conn.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    OracleCommand command = new OracleCommand(query, conn);
                    command.Transaction = trans;
                    command.ExecuteNonQuery();
                    trans.Commit();
                }
                catch (Exception err)
                {

                    var debug = new TblDebugger
                    {
                        Var1 = "ActionService_018",
                        Var2 = $"TransferID #{model.TransferId} Case #{model.CaseNumber} Lot #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    trans.Rollback();
                    throw;
                }
                finally
                {
                    conn.Close();
                    conn.Dispose();
                }
            }
        }
    }
}
