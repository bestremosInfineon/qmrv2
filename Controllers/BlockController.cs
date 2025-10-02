using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contexts;

namespace QMRv2.Controllers
{
    [ApiController]
    public class BlockController : Controller
    {
        private readonly ILogsServices _logsServices;
        private readonly IBlockServices _blockServices;

        public BlockController(ILogsServices logsServices, IBlockServices blockServices)
        {
            _logsServices = logsServices;
            _blockServices = blockServices;
        }


        [HttpPost]
        [Route("submitLCYBlockRequest")]

        public async Task<IActionResult> BlockLotsRequest([FromBody] LotRequest query)
        {
            try
            {
                string responseJson = query != null ? JsonConvert.SerializeObject(new { query }) : "null";
                var inserted = await _blockServices.InsertBlockRequests(query);
                if (inserted.Equals("200"))
                {
                    await Task.Run(() =>
                    {
                        _blockServices.RunBlockJob();
                    });
                }

                return Ok();
            }
            catch (Exception err)
            {
                var debug = new TblDebugger
                {
                    Var1 = "BlockController_001",
                    Var2 = JsonConvert.SerializeObject(new { query }),
                    Var4 = err
                };
                await _logsServices.InsertTblDebugger(debug);
                return BadRequest($"{err.Message} {err.StackTrace}");
            }
        }
    }
}
