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
    public class DispositionServices : IDispositionServices
    {
        private readonly IConfiguration _configuration;
        private readonly ILogsServices _logsServices;
        private readonly IActionServices _actionServices;
        private readonly Converters converters = new Converters();
        public DispositionServices(IConfiguration configuration, ILogsServices logsServices, IActionServices actionServices)
        {
            _configuration = configuration;
            _logsServices = logsServices;
            _actionServices = actionServices;
        }

        public async Task<string> InsertDispositionRequests(LotRequest query)
        {
            try
            {
                var ctrExists = 0;
                Dictionary<string, string> dicStatus = new Dictionary<string, string>();

                if (query.LotList.Count != 0)
                {
                    var lotNumbers = query.LotList.ToList();
                    foreach (var lot in lotNumbers)
                    {
                        //// if lot exists validation
                        if (await IfQMRExistsDispo(query.QmrNumber, lot.LotNumber, query.TransferID))
                        {
                            ctrExists++;
                            dicStatus.Add(lot.LotNumber, "EXISTING");
                        }
                        else
                        {
                            //// if not exists, insert records 
                            await _actionServices.InsertRequestDispo(new IfxBlockResult
                            {
                                Id = string.IsNullOrEmpty(query.ApiKey) ? Guid.NewGuid().ToString() : query.ApiKey,
                                TransferID = query.TransferID,
                                CaseNumber = query.QmrNumber,
                                LotNumber = lot.LotNumber,
                                CaseManager = query.CaseManager,
                                BlockingReason = query.BlockingReason,
                                Disposition = lot.Disposition
                            });

                            dicStatus.Add(lot.LotNumber, "INSERTED");
                        }
                    }

                    var logs = new Logs
                    {
                        Message = "DISPOSITION_REQUEST_LOTS",
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
                    Var1 = "DispositionService_001",
                    Var2 = $"TRANSFERID #{query.TransferID} CASE #{query.QmrNumber} LotNumbers: {JsonConvert.SerializeObject(new { query.LotList})}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw err;
            }
        }

        public async Task RunDispoJob()
        {
            List<IfxBlockResult> lotRequest = await GetLotListDisposition();
            try
            {
                if (lotRequest.Count > 0)
                {
                    //// BEGIN BLOCKING
                    var tasks = lotRequest.Select(cases => DispositionProcess(cases));
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "DispositionService_002",
                    Var2 = JsonConvert.SerializeObject(new { lotRequest }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
        }

        public async Task DispositionProcess(IfxBlockResult query)
        {
            var action = new ActionModel
            {
                CaseNumber = query.CaseNumber,
                LotNumber = query.LotNumber,
                TransferId = query.TransferID,
            };

            try
            {
                IfxBlockResult lotsplice = converters.LotSplice(query.LotNumber);
                using (var ctoken = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
                {
                    var dispoModel = new IfxBlockResult
                    {
                        TransferID = query.TransferID,
                        CaseNumber = query.CaseNumber,
                        LotNumber = lotsplice.LotNumber,
                        CaseManager = query.CaseManager,
                        Split = lotsplice.Split,
                        Disposition = query.Disposition
                    };

                    //ingresLots = mrbServices.SearchIngresLots(query.LotNo, query.Includeparent, query.IncludeShippedCustomer, $"{query.MrbCaseNo}", blockModel, true);
                    if ("123456789".Contains(query.LotNumber[0].ToString()))
                    {
                        await Task.Run(async () =>
                        {
                            if (!await ExecuteDispo(dispoModel))  ////// EXECUTE BLOCKING
                            {
                                await _actionServices.UpdateRequestStatusDisposition(action);  ///// UPDATE REQUEST TABLE
                                var dispoLog = new Logs
                                {
                                    Message = "LOT_DISPOSITIONED",
                                    Reference = $"TransferID #{query.TransferID} Case #{query.CaseNumber} Lot #{query.LotNumber}",
                                    RequestData = JsonConvert.SerializeObject(new { dispoModel })
                                };
                                await _logsServices.LogsTrace(dispoLog);
                            }
                        });
                    }
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "DispositionService_003",
                    Var2 = $"TRANSFERID #{query.TransferID} CASE #{query.CaseNumber} LOT #{query.LotNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
            }
        }

        public async Task<bool> ExecuteDispo(IfxBlockResult model)
        {

            string[] dispoProd = { "RELEASE", "CUSTOMER SELLING RESTRICTION" };
            string dispo;
            switch (model.Disposition.ToUpper())
            {
                case "RELEASE":
                    dispo = "PROD";
                    break;
                case "CUSTOMER SELLING RESTRICTION":
                    dispo = "PRODCR";
                    break;
                case "RETURN TO DC":
                    dispo = "REWORK";
                    break;
                default:
                    dispo = "SCRAP";
                    break;
            }

            var procName = (dispoProd.Contains(model.Disposition.ToUpper()) ? "setDispoToProd" : "setDispoToNonProd");
            bool toggle = Convert.ToBoolean(_configuration["BlockingDispoToggle"]);
            var connString = toggle ? _configuration["Ingres1"] : _configuration["Ingres2"];
            using (IngresConnection connIngres = new IngresConnection(connString))
            {
                connIngres.Open();
                IngresTransaction trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                try
                {
                    // setDispoToNonProd (pMRBcaseno VARCHAR(20), pLot INTEGER, pSplit VARCHAR(5), pOriginator VARCHAR(30), pOwner VARCHAR(30), pDisposition VARCHAR(100))
                    // setDispoToProd (pMRBcaseno VARCHAR(20), pLot INTEGER, pSplit VARCHAR(5), pOriginator VARCHAR(30), pOwner VARCHAR(30), pDisposition VARCHAR(100))
                    string query = $"EXECUTE PROCEDURE {procName} ('{model.CaseNumber}', {model.LotNumber}, '{model.Split}', '{model.CaseManager}', '{model.CaseManager}', '{dispo}', '{DateTime.Now.AddHours(-8).ToString("dd-MMM-yy HH:mm:ss").ToUpper()}')";
                    IngresCommand cmd = new IngresCommand(query, connIngres);
                    cmd.Transaction = trans;
                    cmd.ExecuteNonQuery();
                    trans.Commit();
                    connIngres.Close();

                    return false;
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "DispositionService_004",
                        Var2 = $"TRANSFERID #{model.TransferID} CASE #{model.CaseNumber} LOT #{model.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);

                    trans.Rollback();
                    connIngres.Close();
                    return true;
                }
            }
        }

        public async Task<List<IfxBlockResult>> GetLotListDisposition()
        {
            List<IfxBlockResult> retListValue = new List<IfxBlockResult>();
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand($"SELECT TRANSFER_ID, QMRCASENO, CASE_MANAGER, LOT_NO, DISPOSITION FROM MRB_QMIFX_LOT_DISPO_REQUESTS WHERE IS_DISPOSITIONED = '0' ORDER BY EVENT_TIMESTAMP DESC ", conn);
                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var list = new IfxBlockResult
                        {
                            TransferID = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            CaseNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            CaseManager = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            LotNumber = reader.IsDBNull(3) ? "" : reader.GetString(3),
                            Disposition = reader.IsDBNull(4) ? "" : reader.GetString(4)
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
                    Var1 = "DispositionService_005",
                    Var2 = JsonConvert.SerializeObject(new { retListValue }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);

                return await Task.FromResult(retListValue);
            }
            return await Task.FromResult(retListValue);
        }

        public async Task<bool> IfQMRExistsDispo(string caseNo, string lotNo, string transferID)
        {
            bool retValue = false;
            string commandString = $"SELECT LOT_NO FROM MRB_QMIFX_LOT_DISPO_REQUESTS WHERE QMRCASENO = '{caseNo}' AND LOT_NO = '{lotNo}' AND TRANSFER_ID = '{transferID}' ";
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
                    Var1 = "DispositionService_006",
                    Var2 = $"TRANSFERID #{transferID} CASE #{caseNo} LOT #{lotNo}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(false);
            }
        }
    }
}
