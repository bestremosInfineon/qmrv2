using Ingres.Client;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contexts;
using System.Data;

namespace QMRv2.Repository.Contracts
{
    public class IngresServices : IIngresServices
    {
        private readonly IConfiguration _configuration;
        private readonly ILogsServices _logsServices;
        private readonly ICOINServices _coinServices;

        public IngresServices(IConfiguration configuration, ILogsServices logsServices, ICOINServices coinServices)
        {
            _configuration = configuration;
            _logsServices = logsServices;
            _coinServices = coinServices;
        }

        public async Task<List<CaseInfo>> SearchIngresLots(IngresModels model)
        {
            var response = new List<CaseInfo>();
            try
            {
                int maxRetries = 30;
                int retryDelayMs = 500;

                for (int i = 0; i < maxRetries; i++)
                {
                    try
                    {
                        response = await SearchIngres(model);
                        break; // Success

                    }
                    catch (Exception err)
                    {
                        if (i == maxRetries - 1)
                        {
                            var debug = new TblDebugger
                            {
                                Var1 = "IngresService_014",
                                Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber}",
                                Var4 = err
                            };
                            await _logsServices.InsertTblDebugger(debug);

                            return response; // return blank response after all retries
                            throw;
                        }
                        Thread.Sleep(retryDelayMs); // Wait before retrying
                    }
                }

                return response;
            }
            catch (Exception)
            {
                return response; // return blank response after all retries
            }
        }

        public async Task<List<CaseInfo>> SearchIngres(IngresModels model)
        {
            try
            {
                string[] lotlistIngres = model.LotNumber.Replace("'", string.Empty).Split(',');
                var response = new List<CaseInfo>();
                using (IngresConnection connIngres = new IngresConnection(_configuration["ConnectionStrings:Ingres"]))
                {
                    connIngres.Open();
                    IngresTransaction trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                    await DeleteLotListStg(model, connIngres, trans);

                    foreach (var rowlot in lotlistIngres)
                    {
                        if (char.IsDigit(rowlot[0]))
                        {
                            if (rowlot.Length >= 9 && (rowlot.StartsWith("6") || rowlot.StartsWith("7") || rowlot.StartsWith("12")))
                            {
                                model.LotNumber = rowlot.Substring(0, 9);
                                model.Split = rowlot.Substring(9);
                            }
                            else if (rowlot.Length >= 7)
                            {
                                model.LotNumber = rowlot.Substring(0, 7);
                                model.Split = rowlot.Substring(7);
                            }

                            if (!string.IsNullOrEmpty(model.LotNumber))
                                await InsertLotListStgWithSplit(model, connIngres, trans);
                            else
                                await InsertLotListStgNoSplit(model, connIngres, trans);
                        }
                    }
                    connIngres.Close();

                    model.LotNumber = model.LotNumber.Replace("'", string.Empty);
                    await ExecuteProcedures(model, connIngres, trans);

                    response = await GetWIP(model, connIngres);

                    connIngres.Close();
                }

                return response;
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task DeleteLotListStg(IngresModels model, IngresConnection connIngres, IngresTransaction trans)
        {
            try
            {
                string query = "DELETE FROM cspimis.QMR_LOTLIST_STG WHERE MRBCASENO = '" + model.CaseNumber + "'";
                IngresCommand cmd = new IngresCommand(query, connIngres);
                cmd.Transaction = trans;
                cmd.CommandTimeout = 1800;
                cmd.ExecuteNonQuery();
                trans.Commit();


                query = "DELETE FROM cspimis.QMR_WIPDATA WHERE MRBCASENO = '" + model.CaseNumber + "'";
                cmd = new IngresCommand(query, connIngres);
                cmd.CommandTimeout = 1800;
                cmd.ExecuteNonQuery();
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "IngresService_001",
                    Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);

                trans.Rollback();
                connIngres.Close();
                throw;
            }
        }

        public async Task InsertLotListStgWithSplit(IngresModels model, IngresConnection connIngres, IngresTransaction trans)
        {
            try
            {
                int isExist = 0;
                string queryIngres = $"select 1 from cspimis.QMR_LOTLIST_STG where mrbcaseno = '{model.CaseNumber}' and lot = '{model.LotNumber}' and split = '{model.Split}'";
                IngresCommand cmdIngres = new IngresCommand(queryIngres, connIngres);
                IngresDataReader readerIngres = cmdIngres.ExecuteReader();
                while (readerIngres.Read())
                {
                    isExist = 1;
                    break;
                }
                readerIngres.Close();


                if (isExist != 1)
                {
                    // 2 - Insert to QMR_LOTLIST_STG
                    try
                    {
                        trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                        string query = $"INSERT INTO cspimis.QMR_LOTLIST_STG (mrbcaseno, lot, split) VALUES ('{model.CaseNumber}','{model.LotNumber}','{model.Split}')";
                        IngresCommand cmd = new IngresCommand(query, connIngres);
                        cmd.Transaction = trans;
                        cmd.CommandTimeout = 1800;
                        cmd.ExecuteNonQuery();
                        trans.Commit();
                    }
                    catch (Exception err)
                    {
                        var debug = new TblDebugger
                        {
                            Var1 = "IngresService_002",
                            Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber}",
                            Var4 = err
                        };
                        await _logsServices.InsertTblDebugger(debug);
                        trans.Rollback();
                        connIngres.Close();
                        throw;
                    }

                    if (model.LotNumber.StartsWith("12") && model.Split != "")
                    {
                        trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                        string query = $"INSERT INTO cspimis.QMR_LOTLIST_STG (mrbcaseno, lot, split) VALUES ('{model.CaseNumber}','{model.LotNumber.Substring(0, 7)}','{model.Split.Substring(7)}')";
                        IngresCommand cmd = new IngresCommand(query, connIngres);
                        cmd.ExecuteNonQuery();
                    }
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "IngresService_003",
                    Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                connIngres.Close();
                throw;
            }
        }

        public async Task InsertLotListStgNoSplit(IngresModels model, IngresConnection connIngres, IngresTransaction trans)
        {
            try
            {
                int isExist = 0;
                string queryIngres = $"select 1 from cspimis.QMR_LOTLIST_STG where mrbcaseno = '{model.CaseNumber}' and lot in ('{model.LotNumber}')";

                IngresCommand cmdIngres = new IngresCommand(queryIngres, connIngres);
                IngresDataReader readerIngres = cmdIngres.ExecuteReader();
                while (readerIngres.Read())
                {
                    isExist = 1;
                    break;
                }
                readerIngres.Close();

                if (isExist != 1)
                {

                    // 2 - Insert to QMR_LOTLIST_STG
                    try
                    {
                        trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                        string query = $"INSERT INTO cspimis.QMR_LOTLIST_STG (mrbcaseno, lot, split) VALUES ('{model.CaseNumber}','{model.LotNumber}','')";
                        IngresCommand cmd = new IngresCommand(query, connIngres);
                        cmd.Transaction = trans;
                        cmd.CommandTimeout = 1800;
                        cmd.ExecuteNonQuery();
                        trans.Commit();

                    }
                    catch (Exception err)
                    {
                        var debug = new TblDebugger
                        {
                            Var1 = "IngresService_004",
                            Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber}",
                            Var4 = err
                        };
                        await _logsServices.InsertTblDebugger(debug);

                        trans.Rollback();
                        connIngres.Close();
                        throw;
                    }
                }

            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "IngresService_005",
                    Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                connIngres.Close();
                throw;
            }
        }

        public async Task ExecuteProcedures(IngresModels model, IngresConnection connIngres, IngresTransaction trans)
        {
            try
            {
                //// lottrace
                int maxRetries = 30;
                int retryDelayMs = 500;
                bool isFailed1 = true;

                for (int i = 0; i < maxRetries; i++)
                {
                    if (isFailed1)
                        await connIngres.OpenAsync();

                    try
                    {
                        trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                        try
                        {
                            string query = $"EXECUTE PROCEDURE lot2trace ('" + model.CaseNumber + "')";
                            IngresCommand cmd = new IngresCommand(query, connIngres);
                            cmd.Transaction = trans;
                            cmd.CommandTimeout = 1800;
                            await cmd.ExecuteNonQueryAsync();
                            trans.Commit();
                        }
                        catch (Exception)
                        {
                            trans.Rollback();
                            connIngres.Close();
                            throw;
                        }

                        isFailed1 = false;
                        connIngres.Close();
                        break; // Success
                    }
                    catch (Exception err)
                    {
                        if (i == maxRetries - 1)
                        {
                            var debug = new TblDebugger
                            {
                                Var1 = "IngresService_006",
                                Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber} - Exceeded retry limit",
                                Var4 = err
                            };
                            await _logsServices.InsertTblDebugger(debug);
                            throw; // Re-throw after all retries
                        }
                        Thread.Sleep(retryDelayMs); // Wait before retrying
                    }
                }
                connIngres.Close();


                // 5 BACKWARD TRACING
                //// lotbacktrace
                bool isFailed2 = true;
                if (model.IncludeParent == "Y")
                {
                    for (int i = 0; i < maxRetries; i++)
                    {
                        if (isFailed2)
                            await connIngres.OpenAsync();

                        try
                        {
                            trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                            try
                            {
                                string query = $"EXECUTE PROCEDURE lot2backtrace ('" + model.CaseNumber + "')";
                                IngresCommand cmd = new IngresCommand(query, connIngres);
                                cmd.Transaction = trans;
                                cmd.CommandTimeout = 1800;
                                await cmd.ExecuteNonQueryAsync();
                                trans.Commit();
                            }
                            catch (Exception)
                            {
                                trans.Rollback();
                                connIngres.Close();
                                throw;
                            }

                            isFailed2 = false;
                            connIngres.Close();
                            break; // Success
                        }
                        catch (Exception err)
                        {
                            if (i == maxRetries - 1)
                            {
                                var debug = new TblDebugger
                                {
                                    Var1 = "IngresService_007",
                                    Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber} - Exceeded retry limit",
                                    Var4 = err
                                };
                                await _logsServices.InsertTblDebugger(debug);
                                throw; // Re-throw after all retries
                            }
                            Thread.Sleep(retryDelayMs); // Wait before retrying
                        }
                    }
                }


                //// lotinsert
                ///
                bool isFailed3 = true;
                for (int i = 0; i < maxRetries; i++)
                {
                    if (isFailed3)
                        await connIngres.OpenAsync();

                    try
                    {
                        trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                        try
                        {
                            string query = $"EXECUTE PROCEDURE lot2insert ('{model.CaseNumber}')";
                            IngresCommand cmd = new IngresCommand(query, connIngres);
                            cmd.Transaction = trans;
                            cmd.CommandTimeout = 1800;
                            await cmd.ExecuteNonQueryAsync();
                            trans.Commit();
                        }
                        catch (Exception)
                        {
                            trans.Rollback();
                            connIngres.Close();
                            throw;
                        }

                        isFailed3 = false;
                        connIngres.Close();
                        break; // Success
                    }
                    catch (Exception err)
                    {
                        if (i == maxRetries - 1)
                        {
                            var debug = new TblDebugger
                            {
                                Var1 = "IngresService_008",
                                Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber} - Exceeded retry limit",
                                Var4 = err
                            };
                            await _logsServices.InsertTblDebugger(debug);
                            throw; // Re-throw after all retries
                        }
                        Thread.Sleep(retryDelayMs); // Wait before retrying
                    }
                }

                //// lotfillpar
                ///
                bool isFailed4 = true;
                for (int i = 0; i < maxRetries; i++)
                {
                    if (isFailed4)
                        await connIngres.OpenAsync();

                    try
                    {
                        trans = connIngres.BeginTransaction(IsolationLevel.ReadCommitted);
                        try
                        {
                            string query = $"EXECUTE PROCEDURE lot2fillpar ('{model.CaseNumber}')";
                            IngresCommand cmd = new IngresCommand(query, connIngres);
                            cmd.Transaction = trans;
                            cmd.CommandTimeout = 1800;
                            await cmd.ExecuteNonQueryAsync();
                            trans.Commit();
                        }
                        catch (Exception)
                        {
                            trans.Rollback();
                            connIngres.Close();
                            throw;
                        }

                        isFailed4 = false;
                        connIngres.Close();
                        break; // Success
                    }
                    catch (Exception err)
                    {
                        if (i == maxRetries - 1)
                        {
                            var debug = new TblDebugger
                            {
                                Var1 = "IngresService_009",
                                Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber} - Exceeded retry limit",
                                Var4 = err
                            };
                            await _logsServices.InsertTblDebugger(debug);
                            throw; // Re-throw after all retries
                        }
                        Thread.Sleep(retryDelayMs); // Wait before retrying
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        public async Task<List<CaseInfo>> GetWIP(IngresModels model, IngresConnection connIngres)
        {
            try
            {
                List<CaseInfo> arrIngresLot = new List<CaseInfo>();

                var coinModel = new SearchCoinModel
                {
                    CaseNumber = model.CaseNumber,
                    TransferID = model.TransferID,
                    LotNumber = model.OriginLotNumber
                };

                await Task.Run(async () =>
                {

                    connIngres.Open();
                    int hasRecord = 0;
                    string queryIngres = $"select lot, split, mfg_loc, mfg_area, step, curr_qty, device, pkg_code, parent_lot from cspimis.QMR_WIPDATA " +
                                         $"where mrbcaseno = '{model.CaseNumber}' and mfg_area not like 'FAB%'";
                    IngresCommand cmdIngres = new IngresCommand(queryIngres, connIngres);
                    IngresDataReader readerIngres = cmdIngres.ExecuteReader();

                    while (readerIngres.Read())
                    {
                        hasRecord = 1;
                        var rl = new CaseInfo();
                        string _lotNo = readerIngres.IsDBNull(0) ? "" : readerIngres.GetInt32(0).ToString();
                        rl.LotNumber = _lotNo;
                        rl.SplitNumber = readerIngres.IsDBNull(1) ? "" : readerIngres.GetString(1);
                        rl.MfgSite = readerIngres.IsDBNull(2) ? "" : readerIngres.GetString(2);
                        rl.MfgArea = readerIngres.IsDBNull(3) ? "" : readerIngres.GetString(3);
                        rl.Step = readerIngres.IsDBNull(4) ? "" : readerIngres.GetString(4);
                        rl.Qty = readerIngres.IsDBNull(5) ? "" : readerIngres.GetInt32(5).ToString();
                        rl.Device = readerIngres.IsDBNull(6) ? "" : readerIngres.GetString(6);
                        rl.Pkg = readerIngres.IsDBNull(7) ? "" : readerIngres.GetString(7);
                        //rl.Parent_Lot = readerIngres.IsDBNull(8) ? (!string.IsNullOrEmpty(mrbServices.GetIngressParentLot(_lotNo)) ? mrbServices.GetIngressParentLot(_lotNo) : readerIngres.GetString(8)) : readerIngres.GetString(8);
                        rl.DataSource = $"Ingres-INV";
                        rl.IsFound = 1;
                        rl.LotTraceOrigin = model.OriginLotNumber;
                        rl.ORIGINAL_LOT_NO = model.OriginLotNumber;
                        rl.CMS_MRBNo = await _coinServices.GetIngresCmsMrbNumber(coinModel, _lotNo);
                        rl.ProductLine = await _coinServices.GetIngresPL(coinModel, readerIngres.GetString(6));
                        rl.MfgSiteName = await _coinServices.GetIngresSiteName(coinModel, readerIngres.GetString(2));
                        rl.TraceOrder = "2";
                        arrIngresLot.Add(rl);
                    }
                    if (hasRecord == 0)
                    {
                        string? lotno = model.LotNumber?.Replace(",", "','");
                        readerIngres.Close();
                        queryIngres = $"select lot, split, mfg_loc, mfg_area, step, curr_qty, device, pkg from mis.mfginv " +
                                      $"where (lot || split in ('{lotno}') or lot in ('{lotno}')) and mfg_area not like 'FAB%'";

                        cmdIngres = new IngresCommand(queryIngres, connIngres);
                        readerIngres = cmdIngres.ExecuteReader();
                        while (readerIngres.Read())
                        {
                            hasRecord = 1;
                            var rl = new CaseInfo();
                            string _lotNo = readerIngres.IsDBNull(0) ? "" : readerIngres.GetInt32(0).ToString();
                            rl.LotNumber = _lotNo;
                            rl.SplitNumber = readerIngres.IsDBNull(1) ? "" : readerIngres.GetString(1);
                            rl.MfgSite = readerIngres.IsDBNull(2) ? "" : readerIngres.GetString(2);
                            rl.MfgArea = readerIngres.IsDBNull(3) ? "" : readerIngres.GetString(3);
                            rl.Step = readerIngres.IsDBNull(4) ? "" : readerIngres.GetString(4);
                            rl.Qty = readerIngres.IsDBNull(5) ? "" : readerIngres.GetInt32(5).ToString();
                            rl.Device = readerIngres.IsDBNull(6) ? "" : readerIngres.GetString(6);
                            rl.Pkg = readerIngres.IsDBNull(7) ? "" : readerIngres.GetString(7);
                            rl.Parent_Lot = ////mrbServices.GetIngressParentLot(_lotNo);
                            rl.DataSource = $"Ingres-INV";
                            rl.IsFound = 1;
                            rl.LotTraceOrigin = model.OriginLotNumber;
                            rl.ORIGINAL_LOT_NO = model.OriginLotNumber;
                            rl.CMS_MRBNo = await _coinServices.GetIngresCmsMrbNumber(coinModel, _lotNo);
                            rl.ProductLine = await _coinServices.GetIngresPL(coinModel, readerIngres.GetString(6));
                            rl.MfgSiteName = await _coinServices.GetIngresSiteName(coinModel, readerIngres.GetString(2));
                            rl.TraceOrder = "2";
                            arrIngresLot.Add(rl);
                        }
                    }
                });

                return arrIngresLot;

            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "IngresService_010",
                    Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginLotNumber}, CASE #{model.CaseNumber} - Exceeded retry limit",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
            finally
            {
                connIngres.Close();
            }
        }
    }
}
