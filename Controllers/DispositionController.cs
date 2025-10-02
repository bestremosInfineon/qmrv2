using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contexts;
using QMRv2.Repository.Contracts;

namespace QMRv2.Controllers
{
    [ApiController]
    public class DispositionController : Controller
    {
        private readonly ILogsServices _logsServices;
        private readonly IDispositionServices _dispositionServices;

        public DispositionController(ILogsServices logsServices,  IDispositionServices dispositionServices)
        {
            _logsServices = logsServices;
            _dispositionServices = dispositionServices;
        }


        [HttpPost]
        [Route("submitLCYDispoRequest")]
        public async Task<IActionResult> DispositionLotRequests([FromBody] LotRequest query)
        {
            try
            {
                string responseJson = query != null ? JsonConvert.SerializeObject(new { query }) : "null";
                var inserted = await _dispositionServices.InsertDispositionRequests(query);
                if (inserted.Equals("200"))
                {
                    await Task.Run(() =>
                    {
                        _dispositionServices.RunDispoJob();
                    });
                }

                return Ok();
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "DispositionController_001",
                    Var2 = JsonConvert.SerializeObject(new { query }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return BadRequest($"{err.Message} {err.StackTrace}");
            }
        }
    }
}
