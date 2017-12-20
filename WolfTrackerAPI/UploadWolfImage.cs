using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;
using WolfTrackerAPI.Helpers;

namespace WolfTrackerAPI
{
    public static class UploadWolfImage
    {
        [FunctionName("UploadWolfImage")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = null)] HttpRequestMessage req,
            TraceWriter log)
        {
            log.Info("Starting process of uploading a Wolf image");

            if (!req.Content.IsMimeMultipartContent())
            {
                log.Error("Please pass a name in the request body");

                return req.CreateResponse(HttpStatusCode.UnsupportedMediaType,
                    "Please pass a name in the request body");
            }

            // Confirm the JWT is valid
            var tokenResult = await SecurityJWT.ValidateTokenAsync(req.Headers.Authorization);
            if (tokenResult == null)
            {
                log.Error("Invalid JWT");
                return req.CreateResponse(HttpStatusCode.Unauthorized);
            }
            log.Info("JWT is all good");

            // Extract the username from the token
            var userName = tokenResult.FindFirst(SecurityJWT.Predicate).Value;

            // Get file info from the request
            var provider = new MultipartFormDataStreamProvider(Path.GetTempPath());
            await req.Content.ReadAsMultipartAsync(provider);

            if (provider.FileData.Count != 1)
                return req.CreateErrorResponse(HttpStatusCode.BadRequest, "Please only submit one file");

            // Parse the connection string and return a reference to the storage account
            log.Info("Uploading image to blob storage");
            var blockBlob = await AzureStorageHelper.UploadWolfImageAsync(provider.FileData[0]);

            // Run the cognitive services vision API to confirm whether or not it's a wolf. This can take a few seconds
            log.Info("Submitting request for image analysis");
            var analysisResponse = await AnalyseImage.MakeAnalysisRequest(blockBlob.Uri.ToString(), log);

            // Write info about the image to table storage
            log.Info("Writing info in table storage");
            var id = Path.GetFileNameWithoutExtension(blockBlob.Uri.ToString());
            var entity = new WolfImageEntity(analysisResponse.Response.IsWolf, id)
            {
                UserName = userName,
                Id = id,
                CognitiveJson = analysisResponse.CognitiveJson,
                WolfConfidence = analysisResponse.Response.WolfConfidence,
                ImageDescription = analysisResponse.Response.ImageDescription,
                ImageDescriptionConfidence = analysisResponse.Response.ImageDescriptionConfidence,
                IsAdult = analysisResponse.Response.IsAdult,
                IsRacy = analysisResponse.Response.IsRacy,
                OriginalFileUrl = blockBlob.Uri.ToString()
            };
            await AzureStorageHelper.InsertPictureInfo(entity);

            if (!analysisResponse.Response.IsWolf || analysisResponse.Response.IsAdult ||
                analysisResponse.Response.IsRacy)
            {
                // Please note: 
                // ADULT = are things kids shouldn't see, such as violence
                // RACY = something like a Victoria Secrets (lingerie) model scores very high. Trust me. I tested. Extensively.
                log.Info("Image was not a wolf");
            }
            else
            {
                log.Info("Image was a wolf");

                // It's a wolf image so submit a message in the queue so that we can resize the image with Cognitive Services. 
                // The main reason for queuing this, instead of using blob triggers, is that I want a steady rate of 
                // processing as I have a free account and therefore limited resource. I don't mind if it takes a bit longer.
                await AzureStorageHelper.QueueImageResize(blockBlob.Uri.ToString());
            }

            // Even though I return a 200 it's possible that the image is not a wolf. Will leave it to the UI layer to 
            // break the news that their dog picture won't make it to the list
            return req.CreateResponse(HttpStatusCode.OK, JsonConvert.SerializeObject(analysisResponse.Response));
        }
    }
}