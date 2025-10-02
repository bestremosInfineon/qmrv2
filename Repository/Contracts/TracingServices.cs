using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QMRv2.Common;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using QMRv2.Models.Enums;
using QMRv2.Repository.Contexts;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using v2.Repository.Contexts;

namespace QMRv2.Repository.Contracts
{
    public class TracingServices : ITracingServices
    {
        private readonly IConfiguration _configuration;
        private readonly ILogsServices _logsServices;
        private readonly ICOINServices _coinServices;
        private readonly IIngresServices _ingresServices;
        private readonly IActionServices _actionServices;
        private readonly Converters converter = new Converters();
        public TracingServices(IConfiguration configuration, ILogsServices logsServices, ICOINServices coinServices, IIngresServices ingresServices, IActionServices actionServices)
        {
            _configuration = configuration;
            _logsServices = logsServices;
            _coinServices = coinServices;
            _ingresServices = ingresServices;
            _actionServices = actionServices;
        }

        public string transferIdList = string.Empty;
        public string caseNumberList = string.Empty;
        public string lotNumberList = string.Empty;

        public async Task<LotRequest> InsertRequests(LotRequest query)
        {
            try
            {
                var response = new LotRequest();
                var ctrExists = 0;
                Dictionary<string, string> dicStatus = new Dictionary<string, string>();

                if (query.LotList?.Count != 0)
                {
                    List<string>? lotNumbers = query.LotList.Select(q => q.LotNumber).Distinct().ToList();
                    foreach (var lot in lotNumbers)
                    {
                        //// if lot exists validation
                        if (await _coinServices.IfQMRExistsTrace(query.QmrNumber, lot, query.TransferID))
                        {
                            ctrExists++;
                            dicStatus.Add(lot, "EXISTING");
                        }
                        else
                        {
                            //// if not exists, insert records 
                            await _actionServices.InsertRequestTrace(new IfxResult
                            {
                                Id = string.IsNullOrEmpty(query.ApiKey) ? Guid.NewGuid().ToString() : query.ApiKey,
                                TransferID = query.TransferID,
                                CaseNumber = query.QmrNumber,
                                LotNumber = lot,
                                Username = query.CaseManager
                            });

                            dicStatus.Add(lot, "INSERTED");
                        }
                    }

                    //// get inserted 
                    List<string> totalInserted = await _coinServices.GetLotListByCaseNumberTrace(query.QmrNumber, query.TransferID);
                    List<Lot> lotList = totalInserted
                                .Select(q => new
                                {
                                    LotNumber = q,
                                    HasValue = dicStatus.TryGetValue(q, out var temp),
                                    Status = temp
                                })
                                .Where(x => x.HasValue)
                                .Select(x => new Lot
                                {
                                    LotNumber = x.LotNumber,
                                    Status = x.Status == "INSERTED" ? Status.Inserted : Status.None,
                                    LotStatus = x.Status
                                })
                                .ToList();

                    var tempLotList = lotList.Select(lot => new IfxLot
                    {
                        LotNumber = lot.LotNumber,
                        Status = lot.LotStatus
                    }).ToList();


                    response = new LotRequest
                    {
                        QmrNumber = query.QmrNumber.ToString(),
                        TransferID = query.TransferID,
                        TransferCount = dicStatus.Count(q => q.Value.Contains("INSERTED")),
                        Status = "200",
                        LotList = tempLotList,
                        ActionCode = "INSERTED",
                        ApiKey = query.ApiKey,
                        CaseManager = query.CaseManager?.Trim(),
                    };
                }
                else
                {
                    response = new LotRequest
                    {
                        QmrNumber = query.QmrNumber,
                        TransferID = query.TransferID,
                        TransferCount = query.TransferCount,
                        Status = "500",
                        LotList = null,
                        ActionCode = "EMPTY",
                        ApiKey = query.ApiKey
                    };
                }

                //// logs
                var logs = new Logs
                {
                    Message = "REQUEST_LOTS",
                    Reference = $"TransferID #{query.TransferID}, Case #{query.QmrNumber}",
                    RequestData = JsonConvert.SerializeObject(query),
                    ResponseData = JsonConvert.SerializeObject(response),
                    Verb = "POST",
                    ResponseCode = response.Status,
                };
                await _logsServices.LogsTrace(logs);

                return response;
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_001",
                    Var2 = $"TRANSFERID #{query.TransferID} CASE #{query.QmrNumber} LotNumbers: {JsonConvert.SerializeObject(new { query.LotList })}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
        }

        public async Task RunTraceJob()
        {
            try
            {

                ////// CHECK IF THERE ARE NO MORE STUCK LOTS (IS_PROCESSED = 2 & BEGIN_TRACING IS NULL)
                await UpdateStuckLots();

                ////// ACTIVE LOTS - LIMIT LOTS 
                var requestCount = 0;
                var requestLimit = 30;////int.Parse(ConfigurationManager.AppSettings["TraceLimit"].ToString());
                var activeCount = int.Parse(await _coinServices.GetRequestCount());
                requestCount = (activeCount == 0) ? requestLimit : (activeCount >= requestLimit ? 0 : Math.Abs(activeCount - requestLimit));


                ////// USER VS. SYSTEM
                var defaultList = await _coinServices.GetLotListTraceDefault(requestCount);
                var usersList = await _coinServices.GetLotListTraceUsers(requestCount);
                ////// BEGIN 
                List<IfxResult> lotRequest = usersList.Count > 0 ? usersList : defaultList;
                if (lotRequest.Count > 0)
                {
                    var transferIdList = string.Join(", ", lotRequest.Select(result => $"'{result.TransferID}'").Distinct().OrderBy(id => id).ToList());
                    var caseNumberList = string.Join(", ", lotRequest.Select(result => $"'{result.CaseNumber}'").Distinct().OrderBy(caseNo => caseNo).ToList());
                    var lotNumberList = string.Join(", ", lotRequest.Select(result => $"'{result.LotNumber}'").Distinct().OrderBy(lots => lots).ToList());

                    //// UPDATE STATUS OF REQUEST LOTS TO PROCESSING
                    await _actionServices.BulkUpdateRequestProcessingTrace(transferIdList, caseNumberList, lotNumberList);

                    //// BEGIN TRACING
                    var tasks = lotRequest.Select(cases => TracingProcessInsert(cases));
                    await Task.WhenAll(tasks);
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_002",
                    Var2 = "STOP-TRACE",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
        }

        public async Task TracingProcessInsert(IfxResult cases)
        {
            string errorMessage = string.Empty;
            var query = new SearchCoinLotQuery
            {
                TransferID = cases.TransferID,
                CaseNumber = cases.CaseNumber,
                LotNumber = cases.LotNumber,
                ApiKey = cases.Id
            };
            List<LotResultsDetails> lotResult = new List<LotResultsDetails>();

            var insertAction = new ActionModel
            {
                CaseNumber = cases.CaseNumber,
                LotNumber = cases.LotNumber,
                TransferId = cases.TransferID,
            };

            try
            {
                using (var ctoken = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
                {
                    await _actionServices.UpdateBeginTracing(insertAction);

                    await Task.Run(async () =>
                    {
                        lotResult = await LotTrace(query);
                    }, ctoken.Token);

                    //// INSERT TRACE RESULTS TO RESULTS TABLE
                    if (lotResult.Count > 0)
                    {
                        await _actionServices.UpdateEndTracing(insertAction);

                        //// UPDATE REQUEST TABLE, FLAG IS_PROCEESED = 1
                        insertAction.Status = RequestStatus.Traced;
                        await _actionServices.UpdateRequestStatusTrace(insertAction);

                        var logItems = new List<LotResultsDetails>();
                        foreach (var items in lotResult.OrderBy(q => int.Parse(q.TraceOrder)).ToList())
                        {
                            var existsAction = new ActionModel
                            {
                                CaseNumber = cases.CaseNumber,
                                LotNumber = $"{items.LotNumber}{items.SplitNo}",
                                TransferId = cases.TransferID,
                                LotTraceOrigin = items.LotTraceOrigin
                            };

                            var flagCoin = await _coinServices.IfQMRResultsExistsCOIN(existsAction);
                            if (!string.IsNullOrEmpty(items.Source))
                            {
                                if (items.Source.Equals("COIN") && flagCoin)
                                {
                                    continue;
                                }
                            }

                            bool insertRequest = await _actionServices.InsertResults(items, cases.CaseNumber, cases.TransferID);
                            if (!insertRequest)
                            {
                                insertAction.Status = RequestStatus.ForTracing;
                                await _actionServices.UpdateRequestStatusTrace(insertAction);
                            }
                            else
                            {
                                logItems.Add(items);
                            }
                        }

                        ///// LOGS
                        var logs = new Logs
                        {
                            Message = "TRACE_DONE",
                            Reference = $"TransferID #{cases.TransferID} Case #{cases.CaseNumber}",
                            RequestData = cases.LotNumber,
                            ResponseData = JsonConvert.SerializeObject(new { logItems })
                        };
                        await _logsServices.LogsTrace(logs);

                        ///// EMAIL LOTS WITH MISSING PL
                        MissingPLEmail(lotResult, query);
                    }
                    else
                    {
                        ///// LOGS
                        var logs = new Logs
                        {
                            Message = "TRACE_BLANK",
                            Reference = $"TransferID #{cases.TransferID} Case #{cases.CaseNumber}",
                            RequestData = cases.LotNumber,
                            ResponseData = JsonConvert.SerializeObject(new { lotResult }),
                        };
                        await _logsServices.LogsTrace(logs);
                    }

                    ctoken.Cancel();
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_003",
                    Var2 = $"TRANSFERID #{cases.TransferID} CASE #{cases.CaseNumber} LOT #{cases.LotNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                await TerminateProcess(query, err);
            }
        }

        public async Task<List<LotResultsDetails>> LotTrace(SearchCoinLotQuery param)
        {
            var caseList = new List<LotResultsDetails>();

            //// begin trace
            List<CaseInfo> resultGet = new List<CaseInfo>();
            try
            {
                List<CaseInfo> resultIngres = [];
                List<CaseInfo> resultCOIN = [];
                string resultParentChildMapping = string.Empty;

                if ("123456789".Contains(param.LotNumber[0].ToString()))
                {
                    resultParentChildMapping = "'" + param.LotNumber + "'"; //'123456'
                }
                else
                {
                    resultParentChildMapping = await _coinServices.GetParentChildMapping(param.LotNumber);
                    if (resultParentChildMapping != "" && resultParentChildMapping != param.LotNumber)
                        resultParentChildMapping = "'" + resultParentChildMapping + "','" + param.LotNumber + "'"; //'123456Q2','123456'
                    else
                        resultParentChildMapping = "'" + param.LotNumber + "'"; //'123456'
                }


                DateTime startTime = DateTime.Now;

                using (var cts = new CancellationTokenSource(TimeSpan.FromMinutes(30)))
                {
                    try
                    {
                        //COIN
                        resultCOIN = await _coinServices.SearchCOINLots(param.LotNumber, $"{param.CaseNumber}/{param.TransferID}");
                        var coinResultsLogs = new Logs
                        {
                            Message = "COIN_TRACE",
                            Reference = $"TransferID #{param.TransferID}, Case #{param.CaseNumber}",
                            RequestData = param.LotNumber,
                            ResponseData = JsonConvert.SerializeObject(new { resultCOIN }),
                        };
                        await _logsServices.LogsTrace(coinResultsLogs);

                        if (cts.IsCancellationRequested)
                            throw new OperationCanceledException();

                        if (resultCOIN?.Any() == true)
                            resultGet.AddRange(resultCOIN);

                        //INGRES
                        var ingresModel = new IngresModels
                        {
                            IncludeParent = "Y",
                            LotNumber = resultParentChildMapping,
                            CaseNumber = $"{param.TransferID}/{param.LotNumber}",
                            OriginLotNumber = param.LotNumber,
                            TransferID = param.TransferID
                        };
                        resultIngres = await _ingresServices.SearchIngresLots(ingresModel);

                        var ingresResultsLogs = new Logs
                        {
                            Message = "INGRES_TRACE",
                            Reference = $"TransferID #{param.TransferID}, Case #{param.CaseNumber}",
                            RequestData = param.LotNumber,
                            ResponseData = JsonConvert.SerializeObject(new { resultIngres }),
                        };
                        await _logsServices.LogsTrace(ingresResultsLogs);

                        if (cts.IsCancellationRequested)
                            throw new OperationCanceledException();

                        if (resultIngres?.Any() == true)
                            resultGet.AddRange(resultIngres);
                    }
                    catch (Exception err)
                    {
                        var debug = new TblDebugger
                        {
                            Var1 = "QmrService_004",
                            Var2 = $"TRANSFERID #{param.TransferID} CASE #{param.CaseNumber} LOT #{param.LotNumber}",
                            Var4 = err
                        };
                        await _logsServices.InsertTblDebugger(debug);
                        await TerminateProcess(param, err);
                        throw;
                    }

                    #region for default record if qty > 0 of original lot

                    List<CaseInfo> LotNo_NotFound = new List<CaseInfo>();
                    List<string> Lotnos = new List<string>();

                    //--- Get the Lot No from result or with records found ---//
                    var lotNosFoundIngres = resultIngres.Select(x => x.LotNumber + x.SplitNumber).ToList();
                    var lotNosFoundCOIN = resultCOIN.Select(x => x.LotNumber + x.SplitNumber).ToList();
                    //var lotNosFoundGPN = resultGPN.Select(x => x.LotNo + x.SplitNo).ToList();

                    Lotnos = param.LotNumber.Replace("'", "").Split(',').ToList();

                    //--- Get those lot no from excel that is no record found ---//
                    var _NotFoundList = Lotnos.Except(lotNosFoundCOIN).ToList().Except(lotNosFoundIngres).ToList();
                    List<CaseInfo> tempHold = new List<CaseInfo>();

                    foreach (var item in _NotFoundList)
                    {
                        bool verifyLot = await _coinServices.VerifyLotExisting(item);
                        tempHold.Add(new CaseInfo { LotNumber = item, IsFound = verifyLot ? 1 : 0, Qty = "0", LotTraceOrigin = param.LotNumber, ORIGINAL_LOT_NO = param.LotNumber, TraceOrder = "4" });
                        if (tempHold?.Any() == true)
                            resultGet.AddRange(tempHold);
                    }
                    #endregion
                }

                List<LotResultsDetails> details = new List<LotResultsDetails>();
                if (resultGet.Count > 0)
                {
                    foreach (var q in resultGet)
                    {
                        details.Add(new LotResultsDetails
                        {
                            Quantity = q.Qty?.Trim(),
                            Device = q.Device?.Trim(),
                            MfgArea = q.MfgArea?.Trim(),
                            OrigLotNumber = q.ORIGINAL_LOT_NO?.Trim(),
                            LotNumber = string.IsNullOrEmpty(q.IfxLotName) ? q.LotNumber?.Trim() : q.IfxLotName?.Trim(),
                            LotStatus = null,
                            MfgSiteCode = q.MfgSite?.Trim(),
                            ParentLotNumber = q.Parent_Lot?.Trim(),
                            PackageCode = q.Pkg?.Trim(),
                            ProductionLine = !string.IsNullOrEmpty(q.ProductLine) ? q.ProductLine?.Trim() : (!string.IsNullOrEmpty(q.Device) ? await _coinServices.GetIfxPl(q.Device) : q.ProductLine?.Trim()),
                            Status = "UPDATED",
                            LotTraceOrigin = q.LotTraceOrigin?.Trim(),
                            SplitNo = q.SplitNumber?.Trim(),
                            ShipLoc = q.ShipLoc?.Trim(),
                            ID = param.ApiKey?.Trim(),
                            Source = q.DataSource?.Trim(),
                            TraceOrder = q.TraceOrder?.Trim(),
                            IfxLotName = q.IfxLotName?.Trim()
                        });
                    }
                }


                return details;
            }
            catch (Exception)
            {
                return caseList;
            }
        }

        public async Task<string> SendToIFX(ActionModel headDetails, List<LotResultsDetails> childDetails, string action)
        {
            try
            {
                HttpClient httpClient = new HttpClient();
                var apiResult = string.Empty;

                var lotResults = new LotResults
                {
                    CaseNumber = headDetails.CaseNumber,
                    TransferID = headDetails.TransferId,
                    TransferCount = childDetails.Count,
                    ActionCode = action,
                    LotList = childDetails,
                    ApiKey = Guid.NewGuid().ToString(),
                };

                //// REMOVE OUTER OBJECT
                string jsonRequest = JsonConvert.SerializeObject(new { lotResults });
                JObject jObject = JObject.Parse(jsonRequest);
                JObject innerObject = (JObject)jObject["lotResults"];
                string transformedJson = innerObject.ToString();

                var apiEndpoint = _configuration["IfxUrls:WS_QMRUpdateLot"];
                var content = new StringContent(transformedJson, Encoding.UTF8, "application/json");
                var lotNumber = childDetails.Select(q => q.LotTraceOrigin).Distinct().FirstOrDefault();

                try
                {
                    //// SEND TO IFX
                    apiResult = httpClient.PostAsync(apiEndpoint, content).Result.Content.ReadAsStringAsync().Result;
                    var minifiedJson = Regex.Replace(apiResult, @"\s+", "");
                    var serializedResponse = JsonConvert.DeserializeObject<LotResults>(minifiedJson);

                    var logs = new Logs
                    {
                        Message = "SEND_TO_IFX",
                        Reference = $"TransferID #{headDetails.TransferId}, Case #{headDetails.CaseNumber}",
                        RequestData = transformedJson,
                        ResponseData = minifiedJson,
                        Verb = "POST"
                    };

                    if (serializedResponse.LotList.Count > 0)
                    {
                        //// if response is correct
                        logs.ResponseCode = "200";
                        await _logsServices.LogsTrace(logs);
                        return "1";
                    }
                    else
                    {
                        var errorResponse = JsonConvert.DeserializeObject<SendError>(apiResult);
                        logs.ResponseCode = "400";
                        await _logsServices.LogsTrace(logs);

                        //// if the response has this, "Error: qmrnumber not found"
                        if (!string.IsNullOrEmpty(errorResponse.Error) && !string.IsNullOrEmpty(errorResponse.CaseNumber))
                        {
                            var debug = new TblDebugger
                            {
                                Var1 = "QmrService_005",
                                Var2 = $"TRANSFERID #{headDetails.TransferId} CASE #{headDetails.CaseNumber} LOT #{headDetails.LotNumber}",
                                Var4 = new Exception($"{apiResult.Replace("\'", "\"")}")
                            };
                            await _logsServices.InsertTblDebugger(debug);
                            return errorResponse.Error;
                        }
                        else
                        {
                            //// if the response has this, "{LotList[0].Quantity:[The input was not valid.]} etc.."
                            var debug = new TblDebugger
                            {
                                Var1 = "QmrService_006",
                                Var2 = $"TRANSFERID #{headDetails.TransferId} CASE #{headDetails.CaseNumber} LOT #{headDetails.LotNumber}",
                                Var4 = new Exception($"{apiResult.Replace("\'", "\"")}")
                            };
                            await _logsServices.InsertTblDebugger(debug);
                            return apiResult;
                        }
                    }
                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "QmrService_007",
                        Var2 = $"TRANSFERID #{headDetails.TransferId} CASE #{headDetails.CaseNumber} LOT #{headDetails.LotNumber}",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                    return apiResult;
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_008",
                    Var2 = $"TRANSFERID #{headDetails.TransferId} CASE #{headDetails.CaseNumber} LOT #{headDetails.LotNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return err.Message;
            }
        }

        public async Task SendTraceResults()
        {
            var actionOut = new ActionModel();

            try
            {
                var failed = new List<EmailModel>();
                var head = await _coinServices.GetUnsentResultsHead();

                if (head.Count > 0)
                {
                    var perennialLog = new Logs
                    {
                        Message = "BATCH_SEND_TO_IFX",
                        RequestData = JsonConvert.SerializeObject(new { head })
                    };
                    await _logsServices.LogsTrace(perennialLog);


                    foreach (var item in head)
                    {
                        var query = new ActionModel
                        {
                            TransferId = item.TransferId,
                            CaseNumber = item.CaseNumber,
                            LotNumber = item.LotNumber,
                            ID = item.ID
                        };

                        actionOut = query;
                        var unsent = await _coinServices.GetUnsentResultsDetails(query); ////// get lots by transferid, caseno, id

                        if (unsent.Count > 0)
                        {
                            var apiResponse = await SendToIFX(query, unsent, "TRACE");
                            if (apiResponse == "1")
                            {
                                //// UPDATE REQUEST TABLE, FLAG IS_SENT = 1 AND ADD SENT_DATE 
                                foreach (var origLot in unsent.Select(q => q.LotTraceOrigin).Distinct().ToList())
                                {
                                    query.SentStatus = SentStatus.Sent;
                                    query.LotNumber = origLot;
                                    await _actionServices.UpdateRequestSentStatusTrace(query);
                                }

                                //// UPDATE RESULTS TABLE, FLAG IS_FOUND = 1
                                foreach (var lot in unsent)
                                    await _actionServices.UpdateResultsIsSent(query, lot);
                            }
                            else
                            {
                                //// email failed qmrcase and transferid to recipients
                                var email = new EmailModel
                                {
                                    CaseNumber = query.CaseNumber,
                                    TransferID = query.TransferId,
                                    LotNumber = query.LotNumber,
                                    ErrorMessage = apiResponse
                                };
                                //emailService.FailedLots(email);

                                //// update RequestTable for failed lots 
                                foreach (var fLots in unsent.Select(q => q.LotTraceOrigin).Distinct().ToList())
                                {
                                    query.Status = RequestStatus.Failed;
                                    query.LotNumber = fLots;
                                    await _actionServices.UpdateRequestStatusTrace(query);
                                    await _actionServices.UpdateRequestEmailSent(query);
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
                    Var1 = "QmrService_009",
                    Var2 = $"TRANSFERID #{actionOut.TransferId} CASE #{actionOut.CaseNumber} LOT #{actionOut.LotNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                throw;
            }
        }

        public async Task CancelPerennialLots(string var1, string var2, string var3)
        {
            try
            {
                var models = new ActionModel
                {
                    TransferId = var1,
                    CaseNumber = var2,
                    LotNumber = var3,
                    Status = RequestStatus.Perennial
                };
                await _actionServices.UpdateRequestStatusTrace(models);
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_010",
                    Var2 = $"TRANSFERID #{var1} CASE #{var2} LOT #{var3}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
            }
        }

        public async Task CancelPerennialLotsBatch(string var1, string var2)
        {
            try
            {
                var models = new ActionModel
                {
                    TransferId = var1,
                    CaseNumber = var2,
                    Status = RequestStatus.Perennial
                };

                await _actionServices.UpdateRequestStatusPerennialBatch(models);
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_011",
                    Var2 = $"TRANSFERID #{var1} CASE #{var2}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);

            }
        }

        public async Task CancelFailedLots(string var1, string var2)
        {
            try
            {
                var models = new ActionModel
                {
                    TransferId = var1,
                    CaseNumber = var2,
                    Status = RequestStatus.Failed
                };

                await _actionServices.UpdateRequestStatusTraceBatch(models);
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_012",
                    Var2 = $"TRANSFERID #{var1} CASE #{var2}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
            }
        }

        public async Task MissingPLEmail(List<LotResultsDetails> lotResult, SearchCoinLotQuery query)
        {
            try
            {
                var noPLLots = lotResult.Where(q => (string.IsNullOrEmpty(q.ProductionLine) || q.ProductionLine == "null")
                                        && (!string.IsNullOrEmpty(q.Device) && !string.IsNullOrEmpty(q.Quantity) && !string.IsNullOrEmpty(q.MfgArea)))
                            .Select(lots => new LotResultsDetails
                            {
                                LotNumber = lots.LotNumber,
                                LotTraceOrigin = lots.LotTraceOrigin,
                                OrigLotNumber = lots.OrigLotNumber,
                                ParentLotNumber = lots.ParentLotNumber,
                                Device = lots.Device,
                            }).ToList();

                if (noPLLots.Count > 0)
                {
                    var emailList = noPLLots
                        .GroupBy(grp => new { grp.LotNumber, grp.Device, grp.OrigLotNumber })
                        .Select(k => new EmailModel
                        {
                            Device = k.Key.Device,
                            OriginalLotNumber = k.Key.OrigLotNumber,
                            LotNumber = k.Key.LotNumber,
                            CaseNumber = query.CaseNumber,
                            TransferID = query.TransferID
                        }).ToList();

                    //emailService.MissingProductLineLots(emailList.Distinct().ToList());
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_013",
                    Var2 = $"TransferID #{query.TransferID} Case #{query.CaseNumber} Lot #{query.LotNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);

            }
        }

        public async Task TerminateProcess(SearchCoinLotQuery query, Exception errorMessage)
        {
            try
            {
                if (await _coinServices.CheckIfMailSent(query) == 0)
                {
                    var email = new EmailModel
                    {
                        LotNumber = query.LotNumber,
                        TransferID = query.TransferID,
                        CaseNumber = query.CaseNumber,
                        ErrorMessage = $"Message : {errorMessage.Message.Replace("\'", "\"")} StackTrace : {(string.IsNullOrEmpty(errorMessage.StackTrace) ? string.Empty : errorMessage.StackTrace.Replace("\'", "\""))}"
                    };
                    //emailService.PerennialLotsTimeout(email);
                }

                //// update request table
                var models = new ActionModel
                {
                    TransferId = query.TransferID,
                    CaseNumber = query.CaseNumber,
                    LotNumber = query.LotNumber,
                    Status = RequestStatus.Perennial
                };
                await _actionServices.UpdateRequestEmailSent(models);
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_014",
                    Var2 = $"TransferID #{query.TransferID} Case #{query.CaseNumber} Lot #{query.LotNumber}",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
            }
        }

        public async Task UpdateReqeustResetSentLots()
        {
            var allUnchangedStatusIsSent = await _coinServices.GetLotsWithUnchangedStatusDoneTracing();
            try
            {
                if (allUnchangedStatusIsSent.Count > 0)
                {
                    foreach (var lots in allUnchangedStatusIsSent)
                    {
                        var model = new ActionModel
                        {
                            TransferId = lots.TransferID,
                            CaseNumber = lots.CaseNumber,
                            LotNumber = lots.LotNumber
                        };

                        await _actionServices.UpdateRequestResetIsSentLots(model);
                    }

                    var logs = new Logs
                    {
                        Message = "UNCHANGED_STATUS",
                        Reference = $"DONE",
                        RequestData = JsonConvert.SerializeObject(allUnchangedStatusIsSent),
                    };
                    await _logsServices.LogsTrace(logs);
                }
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "QmrService_015",
                    Var2 = JsonConvert.SerializeObject(allUnchangedStatusIsSent),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
            }
        }

        public async Task UpdateStuckLots()
        {
            List<IfxResult> stuckLots = await _coinServices.GetStuckLots();
            if (stuckLots.Count > 0)
            {
                try
                {
                    var logs = new Logs
                    {
                        Message = "STUCK_LOTS",
                        Reference = $"Restarting stuck lots.",
                        RequestData = JsonConvert.SerializeObject(new { stuckLots })
                    };
                    await _logsServices.LogsTrace(logs);

                    foreach (var lots in stuckLots)
                    {
                        var model = new ActionModel
                        {
                            TransferId = lots.TransferID,
                            CaseNumber = lots.CaseNumber,
                            LotNumber = lots.LotNumber
                        };
                        await _actionServices.UpdateRequestResetLots(model);
                    }

                }
                catch (Exception err)
                {
                    var debug = new TblDebugger
                    {
                        Var1 = "QmrService_016",
                        Var2 = "Error on UpdateRequestResetLots",
                        Var4 = err
                    };
                    await _logsServices.InsertTblDebugger(debug);
                }
            }
        }
    }
}
