using Ingres.Client;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using QMRv2.Common;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contexts;
using System.Data;
using v2.Repository.Contexts;

namespace QMRv2.Repository.Contracts
{
    public class BlockServices : IBlockServices
    {
        private readonly IConfiguration _configuration;
        private readonly ILogsServices _logsServices;
        private readonly IActionServices _actionServices;
        private readonly Converters converters = new Converters();
        public BlockServices(IConfiguration configuration, ILogsServices logsServices,  IActionServices actionServices)
        {
            _configuration = configuration;
            _logsServices = logsServices;
            _actionServices = actionServices;
        }

        public string INGBE => Environment.GetEnvironmentVariable("INGBE") ?? _configuration["ConnectionStrings:INGBE"];
        public string INGCSPIMIS => Environment.GetEnvironmentVariable("INGCSPIMIS") ?? _configuration["ConnectionStrings:INGCSPIMIS"];
        public string CSPMARK => Environment.GetEnvironmentVariable("CSPMARK") ?? _configuration["ConnectionStrings:CSPMARK"];
        public string CSPMFG => Environment.GetEnvironmentVariable("CSPMFG") ?? _configuration["ConnectionStrings:CSPMFG"];
        public string blockToggle => Environment.GetEnvironmentVariable("BlockingDispoToggle") ?? _configuration["BlockingDispoToggle"];
        public string coinConnection => Environment.GetEnvironmentVariable("COIN") ?? _configuration["ConnectionStrings:COIN"];
        
        public async Task<string> InsertBlockRequests(LotRequest query)
        {
            try
            {
                string responseJson = query != null ? JsonConvert.SerializeObject(new { query }) : "null";
                LotRequest response = new LotRequest();
                var responseMessage = string.Empty;
                var ctrExists = 0;
                Dictionary<string, string> dicStatus = new Dictionary<string, string>();

                if (query?.LotList?.Count != 0)
                {
                    List<string> lotNumbers = query?.LotList?.Select(static q => q.LotNumber).Distinct().ToList();
                    foreach (string lot in lotNumbers)
                    {
                        //// if lot exists validation
                        if (await IfQMRExistsBlock(query.QmrNumber, lot, query.TransferID))
                        {
                            ctrExists++;
                            dicStatus.Add(lot, "EXISTING");
                        }
                        else
                        {

                            //// if not exists, insert records 
                            await _actionServices.InsertRequestBlock(new IfxBlockResult
                            {
                                Id = string.IsNullOrEmpty(query.ApiKey) ? Guid.NewGuid().ToString() : query.ApiKey,
                                TransferID = query.TransferID,
                                CaseNumber = query.QmrNumber,
                                LotNumber = lot,
                                CaseManager = query.CaseManager,
                                BlockingReason = query.BlockingReason,
                            });

                            dicStatus.Add(lot, "INSERTED");
                        }
                    }

                    var logs = new Logs
                    {
                        Message = "BLOCK_REQUEST_LOTS",
                        Reference = $"TransferID #{query.TransferID}, Case #{query.QmrNumber}",
                        RequestData = JsonConvert.SerializeObject(query),
                        ResponseData = JsonConvert.SerializeObject(dicStatus),
                        Verb = "POST",
                        ResponseCode = "200",

                    };
                    await _logsServices.LogsTrace(logs);

                    return "200";
                }
                else
                {
                    return "500";
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "BlockService_001",
                    Var2 = $"TRANSFERID #{query.TransferID} CASE #{query.QmrNumber} LotNumbers: {JsonConvert.SerializeObject(new { query.LotList })}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw err;
            }
        }

        public async Task RunBlockJob()
        {
            List<IfxBlockResult> lotRequest = await GetLotListBlock();
            try
            {
                if (lotRequest.Count > 0)
                {
                    //// BEGIN BLOCKING
                    var tasks = lotRequest.Select(cases => BlockingProcess(cases));
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "BlockService_002",
                    Var2 = JsonConvert.SerializeObject(new { lotRequest }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
        }

        public async Task BlockingProcess(IfxBlockResult cases)
        {
            var insertAction = new ActionModel
            {
                CaseNumber = cases.CaseNumber,
                LotNumber = cases.LotNumber,
                TransferId = cases.TransferID,
            };

            try
            {
                IfxBlockResult lotsplice = converters.LotSplice(cases.LotNumber);
                using (var ctoken = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
                {
                    var blockModel = new IfxBlockResult
                    {
                        TransferID = cases.TransferID,
                        CaseNumber = cases.CaseNumber,
                        LotNumber = lotsplice.LotNumber,
                        BlockingReason = cases.BlockingReason,
                        CaseManager = cases.CaseManager,
                        Split = lotsplice.Split
                    };

                    if ("123456789".Contains(cases.LotNumber[0].ToString()))
                    {
                        await Task.Run(async () =>
                        {
                            if (!await ExecuteBlockIngres(blockModel))  ////// EXECUTE BLOCKING
                            {
                                await _actionServices.UpdateRequestStatusBlock(insertAction);  ///// UPDATE REQUEST TABLE
                                var blockLog = new Logs
                                {
                                    Message = "LOT_BLOCKED",
                                    Reference = $"TransferID #{cases.TransferID} Case #{cases.CaseNumber} Lot #{cases.LotNumber}",
                                    RequestData = JsonConvert.SerializeObject(new { blockModel })
                                };
                                await _logsServices.LogsTrace(blockLog);
                            }
                        });
                    }
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "BlockService_003",
                    Var2 = $"TRANSFERID #{cases.TransferID} CASE #{cases.CaseNumber} LOT #{cases.LotNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
            }
        }

        public async Task<bool> ExecuteBlockIngres(IfxBlockResult blockModel)
        {
            bool toggle = Convert.ToBoolean(blockToggle);
            var connString = toggle ? INGBE : INGCSPIMIS;
            using (IngresConnection connIngres = new IngresConnection(connString))
            {
                connIngres.Open();
                IngresTransaction trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    string query = $"EXECUTE PROCEDURE setMRBHoldInWip ('{blockModel.CaseNumber}', {blockModel.LotNumber}, '{blockModel.Split}', '{blockModel.CaseManager}', '{blockModel.CaseManager}', '{blockModel.BlockingReason}', '{DateTime.Now.AddHours(-8).ToString("dd-MMM-yy HH:mm:ss").ToUpper()}')";
                    IngresCommand cmd = new IngresCommand(query, connIngres);
                    cmd.Transaction = trans;
                    cmd.ExecuteNonQuery();
                    trans.Commit();
                    connIngres.Close();

                    await ExecuteBlockIngresCspMfg(blockModel);
                    await ExecuteBlockIngresCspMark(blockModel);

                    return false;
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "BlockService_004",
                        Var2 = $"TRANSFERID #{blockModel.TransferID} CASE #{blockModel.CaseNumber} LOT #{blockModel.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);

                    trans.Rollback();
                    connIngres.Close();
                    return true;
                }
            }
        }

        public async Task<List<IfxBlockResult>> GetLotListBlock()
        {
            List<IfxBlockResult> retListValue = new List<IfxBlockResult>();
            try
            {
                using (OracleConnection conn = new OracleConnection(coinConnection))
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand($"SELECT TRANSFER_ID, QMRCASENO, LOT_NO, CASE_MANAGER, BLOCKING_REASON FROM MRB_QMIFX_LOT_BLOCK_REQUESTS WHERE IS_BLOCKED = '0' ORDER BY EVENT_TIMESTAMP DESC ", conn);
                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var list = new IfxBlockResult
                        {
                            TransferID = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            CaseNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            LotNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            CaseManager = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            BlockingReason = reader.IsDBNull(4) ? "" : reader.GetString(4)
                        };

                        retListValue.Add(list);
                    }
                    conn.Close();
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "BlockService_005",
                    Var2 = JsonConvert.SerializeObject(new { retListValue }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(retListValue);
            }
            return await Task.FromResult(retListValue);
        }

        public async Task<bool> IfQMRExistsBlock(string caseNo, string lotNo, string transferID)
        {
            bool retValue = false;
            string commandString = $"SELECT LOT_NO FROM MRB_QMIFX_LOT_BLOCK_REQUESTS WHERE QMRCASENO = '{caseNo}' AND LOT_NO = '{lotNo}' AND TRANSFER_ID = '{transferID}' and IS_BLOCKED is null ";
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand(commandString, conn);
                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        retValue = true;
                    }

                    conn.Close();
                    return await Task.FromResult(retValue);
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "BlockService_005",
                    Var2 = JsonConvert.SerializeObject(new { retValue }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(false);
            }
        }

        public async Task ExecuteBlockIngresCspMark(IfxBlockResult blockModel)
        {
            var toggle = Convert.ToBoolean(blockToggle);
            var connString = toggle ? CSPMARK : INGBE;
            using (IngresConnection connIngres = new IngresConnection(connString))
            {
                connIngres.Open();
                IngresTransaction trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    ////setMRBHoldInWip(pMRBcaseno VARCHAR(20), pLot INTEGER, pSplit VARCHAR(5), pOriginator VARCHAR(30), pOwner VARCHAR(30), pComment VARCHAR(200), pDatechanged VARCHAR(20))
                    string query = $"EXECUTE PROCEDURE setMRBHoldInWip ('{blockModel.CaseNumber}', {blockModel.LotNumber}, '{blockModel.Split}', '{blockModel.CaseManager}', '{blockModel.CaseManager}', '{blockModel.BlockingReason}', '{DateTime.Now.AddHours(-8).ToString("dd-MMM-yy HH:mm:ss").ToUpper()}')";
                    IngresCommand cmd = new IngresCommand(query, connIngres);
                    cmd.Transaction = trans;
                    cmd.ExecuteNonQuery();
                    trans.Commit();
                    connIngres.Close();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "BlockService_006",
                        Var2 = $"TRANSFERID #{blockModel.TransferID} CASE #{blockModel.CaseNumber} LOT #{blockModel.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);

                    trans.Rollback();
                    connIngres.Close();
                }
            }
        }

        public async Task ExecuteBlockIngresCspMfg(IfxBlockResult blockModel)
        {
            var toggle = Convert.ToBoolean(blockToggle);
            var connString = toggle ? CSPMFG : INGBE;
            using (IngresConnection connIngres = new IngresConnection(connString))
            {
                connIngres.Open();
                IngresTransaction trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    ////setMRBHoldInWip(pMRBcaseno VARCHAR(20), pLot INTEGER, pSplit VARCHAR(5), pOriginator VARCHAR(30), pOwner VARCHAR(30), pComment VARCHAR(200), pDatechanged VARCHAR(20))
                    string query = $"EXECUTE PROCEDURE setMRBHoldInWip ('{blockModel.CaseNumber}', {blockModel.LotNumber}, '{blockModel.Split}', '{blockModel.CaseManager}', '{blockModel.CaseManager}', '{blockModel.BlockingReason}', '{DateTime.Now.AddHours(-8).ToString("dd-MMM-yy HH:mm:ss").ToUpper()}')";
                    IngresCommand cmd = new IngresCommand(query, connIngres);
                    cmd.Transaction = trans;
                    cmd.ExecuteNonQuery();
                    trans.Commit();
                    connIngres.Close();
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "BlockService_007",
                        Var2 = $"TRANSFERID #{blockModel.TransferID} CASE #{blockModel.CaseNumber} LOT #{blockModel.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);

                    trans.Rollback();
                    connIngres.Close();
                }
            }
        }
    }
}
