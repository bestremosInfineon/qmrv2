using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contexts;

namespace QMRv2.Controllers
{
    [ApiController]
    public class TracingController : Controller
    {
        private readonly ILogsServices _logsServices;
        private readonly ITracingServices _tracingServices;

        public TracingController(ILogsServices logsServices, ITracingServices tracingServices, IBlockServices blockServices)
        {
            _logsServices = logsServices;
            _tracingServices = tracingServices;
        }


        [HttpPost]
        [Route("submitLCYLotRequest")]
        public async Task<IActionResult> InsertQmrRequest([FromBody] LotRequest query)
        {
            LotRequest inserted = new LotRequest();
            try
            {
                inserted = await _tracingServices.InsertRequests(query);
                if (inserted.Status.Equals("200"))
                {
                    await Task.Run(() =>
                    {
                        _tracingServices.RunTraceJob();
                    });
                }

                return Ok(inserted);
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "TracingController_001",
                    Var2 = JsonConvert.SerializeObject(new { query }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return BadRequest(err);
            }
        }

        [HttpPost]
        [Route("runTraceJob")]
        public async Task<IActionResult> RunTracing()
        {
            try
            {
                await Task.Run(() =>
                {
                    _tracingServices.RunTraceJob();
                });
                return Ok();
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "TracingController_002",
                    Var2 = "runTraceJob",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return BadRequest($"{err.Message} {err.StackTrace}");
            }
        }


        [HttpGet]
        [Route("lotTraceOnly")]
        public async Task<IActionResult> LotTraceOnly([FromBody] SearchCoinLotQuery param)
        {
            try
            {
                var cts = new CancellationTokenSource(TimeSpan.FromHours(1));
                return Ok(await _tracingServices.LotTrace(param));
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "TracingController_003",
                    Var2 = JsonConvert.SerializeObject(new { param }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return BadRequest(err.Message);
            }
        }


        [HttpGet]
        [Route("batchSend")]
        public async Task<IActionResult> BatchSend()
        {
            try
            {
                await Task.Run(() =>
                {
                    _tracingServices.SendTraceResults();
                });

                return Ok("Sending trace results now...");
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "TracingController_004",
                    Var2 = "batchSend",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return BadRequest(err.Message);
            }
        }

        [HttpPost]
        [Route("updateUnfinishedStatus")]
        public async Task<IActionResult> UpdateUnfinishedStatus()
        {
            try
            {
                await _tracingServices.UpdateReqeustResetSentLots();
                return Ok();
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "TracingController_005",
                    Var2 = "updateUnfinishedStatus",
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return BadRequest(err.Message);
            }
        }
    }
}
