using Ingres.Client;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Newtonsoft.Json;
using Oracle.ManagedDataAccess.Client;
using QMRv2.Common;
using QMRv2.Models.DAO;
using QMRv2.Models.DTO;
using QMRv2.Repository.Contracts;
using System;
using v2.Repository.Contexts;

namespace QMRv2.Repository.Contexts
{
    public interface IDispositionServices
    {
        Task<string> InsertDispositionRequests(LotRequest query);
        Task RunDispoJob();
        Task DispositionProcess(IfxBlockResult cases);
        Task<bool> ExecuteDispo(IfxBlockResult blockModel);
        Task<List<IfxBlockResult>> GetLotListDisposition();
        Task<bool> IfQMRExistsDispo(string caseNo, string lotNo, string transferID);
    }
}
