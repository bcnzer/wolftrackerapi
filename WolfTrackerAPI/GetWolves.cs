using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using WolfTrackerAPI.Helpers;

namespace WolfTrackerAPI
{
    public static class GetWolves
    {
        [FunctionName("GetWolves")]
        public static HttpResponseMessage Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequestMessage req,
            TraceWriter log)
        {
            log.Info("Request for wolves received");
            List<AzureStorageHelper.WolfImageUrlsEntity> wolfInfo;
            try
            {
                wolfInfo = AzureStorageHelper.GetLatest100Pictures();
            }
            catch (Exception ex)
            {
                log.Error("Error retrieving wolves", ex);
                return req.CreateErrorResponse(HttpStatusCode.InternalServerError, ex);
            }

            log.Info($"Wolf info retrieved. {wolfInfo.Count} records found (only top 100 grabbed)");
            return req.CreateResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(wolfInfo));
        }
    }
}