using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using WolfTrackerAPI.Helpers;

namespace WolfTrackerAPI
{
    public static class ResizeImagesQueued
    {
        [FunctionName("ResizeImagesQueued")]
        public static async Task Run(
            [QueueTrigger("queue-resizeimages", Connection = "AzureWebJobsStorage")] string imageToResize,
            TraceWriter log)
        {
            log.Info($"Queue trigger function processing: {imageToResize}");

            // Resize the image using Cognitive Services
            var resizedImagePath = await SmartResizeImage.MakeThumbNailRequest(imageToResize);

            // Get the previous info of the wolf image from table storage. All I'm doing is changing partition key 
            // and entering the URL of the resized image
            log.Info("Wolf image resized");


            var wolfImageInfoResizing = await AzureStorageHelper.GetPictureInfo(
                WolfImageEntity.PartitionKey_AwaitResizing,
                Path.GetFileNameWithoutExtension(imageToResize));
            var wolfImageInfo = new WolfImageEntity
            {
                PartitionKey = WolfImageEntity.PartitionKey_WolfImage,
                ResizedFileUrl = resizedImagePath,
                OriginalFileUrl = wolfImageInfoResizing.OriginalFileUrl,
                WolfConfidence = wolfImageInfoResizing.WolfConfidence,
                IsAdult = wolfImageInfoResizing.IsAdult,
                IsWolf = wolfImageInfoResizing.IsWolf,
                RowKey = wolfImageInfoResizing.RowKey,
                IsRacy = wolfImageInfoResizing.IsRacy,
                ImageDescription = wolfImageInfoResizing.ImageDescription,
                ImageDescriptionConfidence = wolfImageInfoResizing.ImageDescriptionConfidence,
                UserName = wolfImageInfoResizing.UserName,
                CognitiveJson = wolfImageInfoResizing.CognitiveJson,
                Id = wolfImageInfoResizing.Id
            };

            // Write info table storage. This is the final version
            log.Info("Inserting final table storage record about the image");
            await AzureStorageHelper.InsertPictureInfo(wolfImageInfo);

            log.Info("Wolf image successfully resized");
        }
    }
}