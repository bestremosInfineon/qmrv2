using Ingres.Client;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contexts;
using System.Data;

namespace QMRv2.Repository.Contracts
{
    public class COINServices : ICOINServices
    {
        private readonly IConfiguration _configuration;
        private readonly ILogsServices _logsServices;
        public COINServices(IConfiguration configuration, ILogsServices logsServices)
        {
            _configuration = configuration;
            _logsServices = logsServices;
        }

        public async Task<string> GetIngresPL(SearchCoinModel model, string device)
        {
            string response = string.Empty;
            try
            {
                await Task.Run(() =>
                {
                    using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                    {
                        conn.Open();

                        OracleCommand command2;
                        OracleDataReader reader2;
                        command2 = new OracleCommand($"SELECT IFX_PRODUCT_LINE FROM V_MRB_SRC_PL WHERE DEVICE = '{device}'", conn);
                        reader2 = command2.ExecuteReader();
                        while (reader2.Read())
                        {
                            response = string.IsNullOrEmpty(reader2.GetInt16(0).ToString()) ? "" : reader2.GetInt16(0).ToString();
                        }
                    }
                });

                return response;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_001",
                    Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginalLot}, CASE #{model.CaseNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);

                return response;
            }
        }

        public async Task<string> GetIngresSiteName(SearchCoinModel model, string siteCode)
        {
            string response = string.Empty;
            try
            {
                await Task.Run(() =>
                {
                    using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                    {
                        conn.Open();

                        OracleCommand command2;
                        OracleDataReader reader2;
                        command2 = new OracleCommand($"SELECT SITE_NAME FROM MRB_MFG_LOCATION WHERE SITE_CODE = '{siteCode}'", conn);
                        reader2 = command2.ExecuteReader();
                        while (reader2.Read())
                        {
                            response = reader2.IsDBNull(0) ? "" : reader2.GetString(0);
                        }
                    }
                });

                return response;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_002",
                    Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginalLot}, CASE #{model.CaseNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);

                return response;
            }
        }

        public async Task<string> GetIngresCmsMrbNumber(SearchCoinModel model, string lotNumber)
        {
            string response = string.Empty;
            try
            {
                await Task.Run(() =>
                {
                    using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                    {
                        conn.Open();

                        OracleCommand command2;
                        OracleDataReader reader2;
                        string query = $"SELECT LISTAGG(mrbcaseno, ', ') WITHIN GROUP(ORDER BY mrbcaseno) FROM (select distinct b.mrbcaseno from mrb_mrb_hold b where b.lot_no in ('{lotNumber}'))";
                        command2 = new OracleCommand(query, conn);
                        reader2 = command2.ExecuteReader();
                        while (reader2.Read())
                        {
                            response = reader2.IsDBNull(0) ? "" : reader2.GetString(0);
                        }
                    }
                });

                return response;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_003",
                    Var2 = $"TRANSFERID #{model.TransferID} LOT #{model.OriginalLot}, CASE #{model.CaseNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);

                return response;
            }
        }

        public async Task<string> GetParentChildMapping(string lotno)
        {
            List<string> allrelated_lots = new List<string>();

            string allrelatedlots = "'";

            string? sourceDb = _configuration["AppEnvironment"] == "1" ? "MV_MRB_HIST_PARENT_CHILD_LOTS@SAPHINV" : "MV_MRB_HIST_PARENT_CHILD_LOTS@SAPHINVD_QMR";
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();
                try
                {
                    string lotlist_loop = lotno.Replace("'", "").ToString();
                    string[] lotno_arr = lotlist_loop.Split(',');
                    foreach (string lot in lotno_arr)
                    {
                        List<string> initialList = new List<string>();
                        initialList.AddRange(await GetRelatedLot(sourceDb, lot, conn, allrelated_lots)); // Get Child (level 1)
                        if (initialList.Count != 0)
                        {
                            allrelated_lots.AddRange(initialList);

                            for (int i = 1; i < allrelated_lots.Count; i++)
                            {
                                initialList = new List<string>();
                                initialList.AddRange(await GetRelatedLot(sourceDb, allrelated_lots[i], conn, allrelated_lots)); // Get GrandChild (level 2)
                                if (initialList.Count != 0)
                                {
                                    foreach (var item in initialList)
                                    {
                                        if (allrelated_lots.Contains(item) == false)
                                        {
                                            allrelated_lots.AddRange(initialList);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "CoinService_004",
                        Var2 = $"LOT #{lotno}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    throw;
                }
            }

            allrelatedlots = String.Join("','", allrelated_lots.ToArray());
            return allrelatedlots;
        }

        public async Task<List<string>> GetRelatedLot(string tableName, string lot, OracleConnection conn, List<string> data)
        {
            try
            {
                OracleCommand command4 = new OracleCommand("select distinct child_lot from " + tableName + " where parent_lot like '" + lot + "%' and child_lot != '" + lot + "'", conn);
                OracleDataReader reader4 = command4.ExecuteReader();
                List<string> lots = new List<string>();

                string lv_lot = "";

                while (reader4.Read())
                {
                    if (!reader4.IsDBNull(0))
                    {
                        string result = reader4.GetString(0);

                        if (result.Contains('_') && (lv_lot == "" || !(result.Contains(lv_lot))))
                        {
                            string[] arrx = result.Split('_');

                            if (arrx.Count() > 2)
                            {
                                lots.Add(arrx[1]);
                                lv_lot = arrx[1];
                            }
                            else
                            {
                                if (lv_lot == "" && arrx[0] != lot)
                                {
                                    lots.Add(arrx[0]);
                                }

                                lv_lot = arrx[0];
                            }

                            result = lv_lot;
                        }

                        if (!data.Contains(result) && (!(result.Contains(lv_lot) && lv_lot != "")))
                        {
                            lots.Add(result);
                        }
                    }

                }
                return lots;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_005",
                    Var2 = $"LOT #{lot}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
        }
     
        public async Task<List<CaseInfo>> SearchCOINLots(string lotNumber, string caseNumber)
        {
            int hasIngresMapping = 0;
            List<CaseInfo> RRT = new List<CaseInfo>();
            lotNumber = lotNumber.Trim().ToUpper();
            string originLotNumber = lotNumber;
           
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                try
                {
                    conn.Open();
                    OracleCommand command;
                    string? sourceDb = _configuration["AppEnvironment"] == "1" ? "MV_MRB_HIST_PARENT_CHILD_LOTS@SAPHINV" : "MV_MRB_HIST_PARENT_CHILD_LOTS@SAPHINVD_QMR";
                    if (lotNumber != "")
                    {
                        List<string> allrelated_lots = new List<string>();
                        List<string> parent_lot = new List<string>();
                        List<string> child_lots = new List<string>();

                        var currparent_lot = "";
                        var currchild_lot = "";
                        int hasParent = 0;
                        int hasChild = 0;
                        allrelated_lots.Add(lotNumber);

                        //// FORWARD TRACING - GET CHILDREN OF LOT_TRACE_ORIGIN
                        string lotlist_loop = lotNumber.Replace("'", "").ToString();
                        string[] lotno_arr = lotlist_loop.Split(',');
                        foreach (string lot in lotno_arr)
                        {
                            List<string> initialList = new List<string>();
                            initialList.AddRange(await GetRelatedLot(sourceDb, lot, conn, allrelated_lots));
                            if (initialList.Count != 0)
                            {
                                hasChild = 1;
                                allrelated_lots.AddRange(initialList);
                                for (int i = 1; i < allrelated_lots.Count; i++)
                                {
                                    initialList = new List<string>();
                                    initialList.AddRange(await GetRelatedLot(sourceDb, allrelated_lots[i], conn, allrelated_lots));
                                    if (initialList.Count != 0)
                                    {
                                        foreach (var item in initialList)
                                        {
                                            if (allrelated_lots.Contains(item) == false) // do not insert related lot already on the list
                                            {
                                                allrelated_lots.AddRange(initialList);
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        //// BACKWARD TRACING - GET PARENTS OF CHILDREN LOTS 
                        using (OracleCommand command2 = new OracleCommand($"SELECT * FROM {sourceDb} WHERE child_lot in  ('{string.Join("','", allrelated_lots)}')  order by child_lot", conn))
                        {
                            using (OracleDataReader reader2 = command2.ExecuteReader())
                            {
                                while (reader2.Read())
                                {
                                    if (!reader2.IsDBNull(0))
                                    {
                                        allrelated_lots.Add(reader2.GetString(0));
                                    }
                                }
                            }
                        }
                        allrelated_lots = allrelated_lots.Distinct().ToList();

                        // 4 -  Get Inventory from COIN ==================================================================

                        string listRelatedLots = "";
                        string lotnos = "";
                        string listRelatedLotsDC = "";
                        string lotsnoforDC = "";

                        if (hasParent == 0 && hasChild == 0)
                        {
                            lotnos = $"'{lotNumber}'";
                            lotsnoforDC = $"'{lotNumber.Replace("',", "000',")}000'";
                        }
                        else
                        {
                            listRelatedLots = string.Join(",", allrelated_lots.Select(x => $"'{x}'"));
                            listRelatedLotsDC = string.Join(",", allrelated_lots.Select(x => $"'{x}000'"));

                            lotnos = listRelatedLots;
                            lotsnoforDC = listRelatedLotsDC;

                            if (lotsnoforDC == "")
                            {
                                lotsnoforDC = "''";
                            }
                        }

                        var coinModel = new SearchCoinModel
                        {
                            OracleConnection = conn,
                            LotNumbersDC = lotsnoforDC,
                            LotNumber = lotNumber,
                            LotNumbers = lotnos,
                            OriginalLot = originLotNumber,
                            CaseNumber = caseNumber
                        };

                        RRT.AddRange(await CoinResultMapping("SP_QMR_SHIPPED_TO_DC", coinModel, "1"));
                        RRT.AddRange(await CoinResultMapping("SP_QMR_SEARCH_COIN", coinModel, "3"));

                        conn.Close();
                        conn.Dispose();
                    }

                    RRT = RRT.OrderBy(x => x.ID).ToList();
                    return RRT;
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "CoinService_006",
                        Var2 = $"LOT #{lotNumber}, CASE #{caseNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    conn.Close();
                    conn.Dispose();
                    throw;
                }
            }
        }

        public async Task<List<CaseInfo>> CoinResultMapping(string query, SearchCoinModel model, string order)
        {
            try
            {
                var RRT = new List<CaseInfo>();

                using (OracleCommand cmd = new OracleCommand(query, model.OracleConnection))
                {
                    cmd.CommandType = System.Data.CommandType.StoredProcedure;
                    cmd.CommandTimeout = 1800;
                    cmd.Parameters.Add("p_lot_number", OracleDbType.Varchar2).Value = model.LotNumbers;
                    cmd.Parameters.Add("p_lot_number_zero", OracleDbType.Varchar2).Value = model.LotNumbersDC;
                    cmd.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;

                    var list = new List<string>();
                    using (OracleDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var rl = new CaseInfo();
                            var lotNo = reader.IsDBNull(0) ? "" : reader.GetString(0);
                            var splitNo = reader.IsDBNull(1) ? "" : reader.GetString(1);
                            rl.LotNumber = lotNo;
                            rl.SplitNumber = splitNo;

                            //#052023 remove space on lotsplit
                            if (rl.SplitNumber == "")
                                rl.LotNumber = rl.LotNumber;

                            var parentLotFirst = await GetIngressParentLot($"{lotNo}");
                            var parentLotFinal = (!string.IsNullOrEmpty(parentLotFirst) ? parentLotFirst : "");

                            rl.MfgSiteName = reader.IsDBNull(2) ? "" : reader.GetString(2);
                            rl.MfgSite = reader.IsDBNull(3) ? "" : reader.GetString(3);
                            rl.MfgArea = reader.IsDBNull(4) ? "" : reader.GetString(4).ToString();
                            rl.Qty = reader.IsDBNull(5) ? "" : reader.GetInt32(5).ToString();
                            rl.Step = reader.IsDBNull(6) ? "" : reader.GetString(6);
                            rl.WarehouseLoc = reader.IsDBNull(7) ? "" : reader.GetString(7);
                            rl.Device = reader.IsDBNull(8) ? "" : reader.GetString(8);
                            rl.Pkg = reader.IsDBNull(9) ? "" : reader.GetString(9);
                            rl.ShipLoc = reader.IsDBNull(10) ? "" : reader.GetString(10);
                            rl.ProductLine = reader.IsDBNull(11) ? "" : reader.GetString(11);
                            rl.CMS_MRBNo = reader.IsDBNull(12) ? "" : reader.GetString(12);
                            rl.Parent_Lot = reader.IsDBNull(13) ? parentLotFinal : reader.GetString(13).Replace(",", ", ");
                            rl.DataSource = reader.IsDBNull(14) ? "" : reader.GetString(14);
                            rl.ID = reader.IsDBNull(15) ? reader.GetInt32(17) : reader.GetInt32(15);
                            rl.IsFound = 1;
                            rl.LotTraceOrigin = model.OriginalLot;
                            rl.ORIGINAL_LOT_NO = model.OriginalLot;
                            rl.IfxLotName = reader.IsDBNull(16) ? "" : reader.GetString(16);
                            rl.MfgSiteName = await GetIngresSiteName(model, reader.IsDBNull(3) ? "" : reader.GetString(3));

                            rl.TraceOrder = order;
                            RRT.Add(rl);
                        }
                    }
                }

                return RRT;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_007",
                    Var2 = $"LOT #{model.LotNumber}, CASE #{model.CaseNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                model.OracleConnection.Close();
                model.OracleConnection.Dispose();
                throw;
            }
        }

        public async Task<string> GetIngressParentLot(string lotNo)
        {
            try
            {
                string _parentLot = "";
                string? sourceDb = _configuration["AppEnvironment"] == "1" ? "MV_MRB_HIST_PARENT_CHILD_LOTS@SAPHINV" : "MV_MRB_HIST_PARENT_CHILD_LOTS@SAPHINVD_QMR";

                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    string _sql = " select LISTAGG(parent_lot, ', ') WITHIN GROUP(ORDER BY parent_lot)  as Parent_Lot";
                    _sql = _sql + " from ( select * from ( ";
                    _sql = _sql + "          Select case when (INSTR(Parent_Lot, '_') > 0) then substr(Parent_Lot,1, INSTR(Parent_Lot, '_') - 1) else Parent_Lot end as parent_lot, child_Lot ";
                    _sql = _sql + "          from " + sourceDb;
                    _sql = _sql + "          where child_lot != parent_lot)";
                    _sql = _sql + "        Group by parent_lot, child_Lot)";
                    _sql = _sql + "  where child_lot = '" + lotNo + "'  and parent_lot != '" + lotNo + "' ";

                    using (OracleCommand command2 = new OracleCommand(_sql, conn))
                    {
                        using (OracleDataReader reader = command2.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                if (!reader.IsDBNull(0))
                                {
                                    if (_parentLot != "")
                                    {
                                        _parentLot += ", ";
                                    }
                                    _parentLot += reader.GetString(0);
                                }

                            }
                        }
                    }
                    conn.Close();
                }
                return _parentLot;

            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_008",
                    Var2 = $"LOT #{lotNo}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
        }

        public async Task<List<string>> GetLotListByCaseNumberTrace(string caseNo, string transferID)
        {
            List<string> retListValue = new List<string>();
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand("SELECT LOT_NO FROM MRB_QMRIFX_LOT_REQUESTS WHERE QMRCASENO = '" + caseNo + "' AND TRANSFER_ID = '" + transferID + "' AND IS_PROCESSED = '0' ", conn);
                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        retListValue.Add(reader.IsDBNull(0) ? "" : reader.GetString(0));
                    }
                    conn.Close();
                    conn.Dispose();
                }

                return await Task.FromResult(retListValue);
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_009",
                    Var2 = string.Join(",", retListValue).ToString(),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(retListValue);
            }
        }

        public async Task<bool> IfQMRExistsTrace(string caseNo, string lotNo, string transferID)
        {
            bool retValue = false;
            string commandString = $"SELECT LOT_NO FROM MRB_QMRIFX_LOT_REQUESTS WHERE QMRCASENO = '{caseNo}' AND LOT_NO = '{lotNo}' AND TRANSFER_ID = '{transferID}' and IS_PROCESSED = 0 ";
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
                    Var1 = "CoinService_010",
                    Var2 = $"TRANSFERID #{transferID} LOT #{lotNo} CASE #{caseNo}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(false);
            }
        }

        public async Task<List<IfxResult>> GetLotListTraceDefault(int requestCount)
        {
            List<IfxResult> retListValue = new List<IfxResult>();
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    //OracleCommand command = new OracleCommand($"SELECT TRANSFER_ID, QMRCASENO, LOT_NO, ID FROM MRB_QMRIFX_LOT_REQUESTS WHERE TRANSFER_ID IN ( SELECT TRANSFER_ID FROM MRB_QMRIFX_LOT_REQUESTS WHERE IS_PROCESSED = '0' AND IS_SENT = '0' AND BEGIN_TRACING IS NULL ORDER BY EVENT_TIMESTAMP  FETCH FIRST {requestCount} ROWS ONLY ) ORDER BY TRANSFER_ID FETCH FIRST {requestRowsCount} ROWS ONLY", conn);
                    OracleCommand command = new OracleCommand($"SELECT TRANSFER_ID, QMRCASENO, LOT_NO, ID FROM MRB_QMRIFX_LOT_REQUESTS WHERE IS_PROCESSED = '0' AND IS_SENT = '0' AND BEGIN_TRACING IS NULL AND END_TRACING IS NULL AND SENT_DATE IS NULL ORDER BY EVENT_TIMESTAMP ASC FETCH FIRST {requestCount} ROWS ONLY ", conn);

                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var list = new IfxResult
                        {
                            TransferID = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            CaseNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            LotNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Id = reader.IsDBNull(3) ? "" : reader.GetString(3),
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
                    Var1 = "CoinService_011",
                    Var2 = string.Join(",", retListValue),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(retListValue);
            }
            return await Task.FromResult(retListValue);
        }

        public async Task<List<ActionModel>> GetUnsentResultsHead()
        {
            List<ActionModel> retListValue = new List<ActionModel>();
            try
            {
                await Task.Run(() =>
                {
                    using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                    {
                        conn.Open();
                        var execString = $"SELECT DISTINCT b.TRANSFER_ID, b.CaseNumber  FROM MRB_QMRIFX_LOT_REQUESTS b " +
                                         $"WHERE b.IS_PROCESSED = '1' AND b.IS_SENT = '0' AND b.BEGIN_TRACING IS NOT NULL AND b.END_TRACING IS NOT NULL";

                        OracleCommand command = new OracleCommand(execString, conn);
                        command.ExecuteNonQuery();
                        OracleDataReader reader = command.ExecuteReader();
                        while (reader.Read())
                        {
                            var details = new ActionModel
                            {
                                TransferId = reader.IsDBNull(0) ? "" : reader.GetString(0),
                                CaseNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            };

                            retListValue.Add(details);
                        }
                        conn.Close();
                    }
                });
                return retListValue;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_017",
                    Var2 = string.Join(",", retListValue),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return retListValue;
            }
        }

        public async Task<List<LotResultsDetails>> GetUnsentResultsDetails(ActionModel models)
        {
            List<LotResultsDetails> retListValue = new List<LotResultsDetails>();
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    var execString = $"SELECT * FROM MRB_QMRIFX_LOT_RESULT WHERE MRBCASENO = '{models.CaseNumber}' AND TRANSFER_ID = '{models.TransferId}' AND IS_FOUND IS NULL";

                    OracleCommand command = new OracleCommand(execString, conn);
                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var lotNo = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        var split = reader.IsDBNull(12) ? string.Empty : reader.GetString(12);
                        var details = new LotResultsDetails
                        {
                            LotNumber = $"{lotNo}{split}",
                            ParentLotNumber = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                            MfgSiteCode = reader.IsDBNull(4) ? "" : reader.GetString(4),
                            MfgArea = reader.IsDBNull(5) ? "" : reader.GetString(5),
                            ShipLoc = reader.IsDBNull(6) ? "" : reader.GetString(6),
                            Quantity = reader.IsDBNull(7) ? "" : reader.GetInt32(7).ToString(),
                            Device = reader.IsDBNull(8) ? "" : reader.GetString(8),
                            PackageCode = reader.IsDBNull(9) ? "" : reader.GetString(9),
                            ProductionLine = reader.IsDBNull(10) ? "" : reader.GetString(10),
                            SplitNo = split,
                            ID = reader.IsDBNull(13) ? "" : reader.GetString(13),
                            LotTraceOrigin = reader.IsDBNull(14) ? "" : reader.GetString(14),
                            OrigLotNumber = reader.IsDBNull(17) ? "" : reader.GetString(17),
                            Status = "Updated",
                            LotStatus = null
                        };

                        retListValue.Add(details);
                    }
                    conn.Close();
                }

                return retListValue;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_018",
                    Var2 = JsonConvert.SerializeObject(new { models }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return retListValue;
            }
        }

        public async Task<bool> IfQMRResultsExistsCOIN(ActionModel model)
        {
            bool retValue = false;
            string commandString = $"SELECT LOT_NO FROM MRB_QMRIFX_LOT_RESULT WHERE MRBCASENO = '{model.CaseNumber}' " +
                                    $"AND LOT_NO||SPLIT_NO = '{model.LotNumber}' " +
                                    $"AND TRANSFER_ID = '{model.TransferId}' " +
                                    $"AND LOT_TRACE_ORIGIN = '{model.LotTraceOrigin}' ";
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
                    Var1 = "CoinService_019",
                    Var2 = JsonConvert.SerializeObject(new { model }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(false);
            }
        }

        public async Task<List<IfxResult>> GetStuckLots()
        {

            var retListValue = new List<IfxResult>();
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                try
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand("SP_QMR_GET_STUCK_LOTS", conn);
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var list = new IfxResult
                        {
                            TransferID = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            CaseNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            LotNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Timestamp = reader.GetDateTime(3),
                            Flag = reader.IsDBNull(4) ? "" : reader.GetString(4)
                        };

                        retListValue.Add(list);
                    }

                    conn.Close();
                    return retListValue;
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "CoinService_020",
                        Var2 = JsonConvert.SerializeObject(new { retListValue }),
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    return retListValue;
                }
            }

        }

        public async Task<string> GetRequestCount()
        {
            string retValue = string.Empty;
            string commandString = $"SELECT COUNT(1) FROM MRB_QMRIFX_LOT_REQUESTS WHERE IS_PROCESSED = '2' AND IS_SENT = '0' AND BEGIN_TRACING IS NOT NULL AND END_TRACING IS NULL";
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand(commandString, conn);
                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                        retValue = reader.GetInt32(0).ToString();

                    conn.Close();
                    return await Task.FromResult(retValue);
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_021",
                    Var2 = JsonConvert.SerializeObject(new { retValue }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(retValue);
            }
        }

        public async Task<List<IfxResult>> GetLotsWithUnchangedStatusDoneTracing()
        {

            var retListValue = new List<IfxResult>();
            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                try
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand("SP_QMR_STATUS_RESET", conn);
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.Add("p_result", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var list = new IfxResult
                        {
                            TransferID = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            CaseNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            LotNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Timestamp = reader.GetDateTime(3),
                            Flag = reader.IsDBNull(4) ? "" : reader.GetString(4)
                        };

                        retListValue.Add(list);
                    }

                    conn.Close();
                    return retListValue;
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "CoinService_022",
                        Var2 = JsonConvert.SerializeObject(new { retListValue }),
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    return retListValue;
                }
            }
        }

        public async Task<List<IfxResult>> GetLotListTraceUsers(int requestCount)
        {
            List<IfxResult> retListValue = new List<IfxResult>();
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand($"SELECT TRANSFER_ID, QMRCASENO, LOT_NO, ID FROM MRB_QMRIFX_LOT_REQUESTS WHERE IS_PROCESSED = '0' AND IS_SENT = '0' AND BEGIN_TRACING IS NULL AND END_TRACING IS NULL AND SENT_DATE IS NULL AND USERNAME IS NOT NULL AND USERNAME NOT IN ('SYSTEM') ORDER BY EVENT_TIMESTAMP ASC FETCH FIRST {requestCount} ROWS ONLY ", conn);

                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        var list = new IfxResult
                        {
                            TransferID = reader.IsDBNull(0) ? "" : reader.GetString(0),
                            CaseNumber = reader.IsDBNull(1) ? "" : reader.GetString(1),
                            LotNumber = reader.IsDBNull(2) ? "" : reader.GetString(2),
                            Id = reader.IsDBNull(3) ? "" : reader.GetString(3),
                        };

                        retListValue.Add(list);
                    }
                    conn.Close();
                }

                return await Task.FromResult(retListValue);
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_023",
                    Var2 = JsonConvert.SerializeObject(new { retListValue }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return await Task.FromResult(retListValue);
            }
        }

        public async Task<int> CheckIfMailSent(SearchCoinLotQuery details)
        {
            int response = 0;
            try
            {
                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    OracleCommand command = new OracleCommand($"SELECT COUNT(*) FROM MRB_QMRIFX_LOT_REQUESTS " +
                        $"WHERE TRANSFER_ID = '{details.TransferID}' " +
                        $"AND QMRCASENO = '{details.CaseNumber}'" +
                        $"AND LOT_NO = '{details.LotNumber}' " +
                        $"AND IS_EMAIL_SENT = '1' " +
                        $"AND SENT_EMAIL_DATE IS NOT NULL ", conn);

                    command.ExecuteNonQuery();
                    OracleDataReader reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        response = reader.GetInt32(0);
                    }

                    conn.Close();
                }

                return response;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_024",
                    Var2 = JsonConvert.SerializeObject(new { details }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return -1;
            }
        }

        public async Task<bool> VerifyLotExisting(string lot)
        {
            try
            {
                bool res = false;
                string tableName = _configuration["AppEnvironment"] == "1" ? "MV_MRB_HIST_PARENT_CHILD_LOTS@SAPHINV" : "MV_MRB_HIST_PARENT_CHILD_LOTS@SAPHINVD_QMR";

                using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
                {
                    conn.Open();
                    OracleCommand command4 = new OracleCommand("select * from " + tableName + " where parent_lot = '" + lot + "' or child_lot = '" + lot + "'", conn);
                    OracleDataReader reader4 = command4.ExecuteReader();

                    while (reader4.Read())
                    {
                        res = true;
                        break;
                    }
                    conn.Close();
                }

                if (!res)
                {
                    using (IngresConnection conn = new IngresConnection(_configuration["ConnectionStrings:Ingres1"]))
                    {
                        conn.Open();
                        IngresCommand command4 = new IngresCommand("select * from mis.mfginv where concat(lot, split) like '%" + lot + "%'", conn);
                        IngresDataReader reader4 = command4.ExecuteReader();

                        while (reader4.Read())
                        {
                            res = true;
                            break;
                        }
                        conn.Close();
                    }
                }

                return res;

            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_025",
                    Var2 = lot,
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
        }

        public async Task<string> GetIfxPl(string device)
        {
            string productLine = "";
            try
            {

                //substring the device until it reach the first character
                List<string> listsOfDevice = new List<string>();
                for (int i = device.Length; i >= 1; i--)
                {
                    listsOfDevice.Add("'" + device.Substring(0, i) + "'");
                }

                List<string> listsOfQuery = new List<string>
                {
                    "SELECT DISTINCT PL FROM MRB_LOT_SAVED WHERE DEVICE IN (" + String.Join(",", listsOfDevice) + ") AND PL IS NOT NULL",
                    "SELECT DISTINCT PL FROM MRB_MRB_HOLD WHERE DEVICE IN (" + String.Join(",", listsOfDevice) + ") AND PL IS NOT NULL",
                    "SELECT DISTINCT IFX_PRODUCT_LINE FROM DNA_REPORTS.V_DNA_PRODUCT_BY_OPN@TO_DNA WHERE OPN_NAME IN (" + String.Join(",", listsOfDevice) + ") AND IFX_PRODUCT_LINE IS NOT NULL",
                    "SELECT DISTINCT IFX_PRODUCT_LINE FROM DNA_REPORTS.V_DNA_PRODUCT_BY_OPN@TO_DNA WHERE BACKEND_PART_NAME IN (" + String.Join(",", listsOfDevice) + ") AND IFX_PRODUCT_LINE IS NOT NULL",
                    "SELECT DISTINCT IFX_PRODUCT_LINE FROM DNA_REPORTS.V_DNA_PRODUCT_BY_OPN@TO_DNA WHERE DIE_PART_NAME IN (" + String.Join(",", listsOfDevice) + ") AND IFX_PRODUCT_LINE IS NOT NULL",
                };


                foreach (var query in listsOfQuery)
                {
                    productLine = PLRunScript(query);

                    if (!string.IsNullOrEmpty(productLine))
                        break;
                }

                return productLine;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "CoinService_026",
                    Var2 = device,
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return productLine;
            }
        }

        public string PLRunScript(string query)
        {
            string resPlValue = "";

            using (OracleConnection conn = new OracleConnection(_configuration["ConnectionStrings:COIN"]))
            {
                conn.Open();

                OracleCommand cmd = new OracleCommand(query, conn);
                OracleDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    resPlValue = reader.IsDBNull(0) ? "" : reader.GetString(0);

                    if (resPlValue != "")
                        break;
                }

                reader.Close();
                conn.Close();
            }
            return resPlValue;
        }
    }
}
