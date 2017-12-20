using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using Microsoft.WindowsAzure.Storage.Queue;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace WolfTrackerAPI.Helpers
{
    public static class AzureStorageHelper
    {
        private static readonly CloudBlobContainer _originalImageContainer;
        private static readonly CloudBlobContainer _resizedImageContainer;
        private static readonly CloudQueue _queueResizeImage;
        private static readonly CloudTable _tableImageInfo;

        /// <summary>
        ///     Constructor which will create and store bunch of connections - once!
        /// </summary>
        static AzureStorageHelper()
        {
            var storageAccount = CloudStorageAccount.Parse(
                CloudConfigurationManager.GetSetting("AzureWebJobsStorage"));

            // Get the blob containers and set them up if they don't exist
            var blobClient = storageAccount.CreateCloudBlobClient();
            _originalImageContainer = blobClient.GetContainerReference("wolfpictures-originals");
            _originalImageContainer.CreateIfNotExists();
            _resizedImageContainer = blobClient.GetContainerReference("wolfpictures-resized");
            _resizedImageContainer.CreateIfNotExists();

            // Get the queues and set them up if they don't exist
            var queueClient = storageAccount.CreateCloudQueueClient();
            _queueResizeImage = queueClient.GetQueueReference("queue-resizeimages");
            _queueResizeImage.CreateIfNotExists();

            // Table storage info
            var tableStorageClient = storageAccount.CreateCloudTableClient();
            _tableImageInfo = tableStorageClient.GetTableReference("WolfPicturesInfo");
            _tableImageInfo.CreateIfNotExists();
        }

        /// <summary>
        ///     Upload an image to blob storage but also generate a unique filename
        /// </summary>
        /// <param name="wolfImageData">MultipartFileData passed as part of the POST request</param>
        /// <returns>CloubBlockBlob which has info about the file</returns>
        public static async Task<CloudBlockBlob> UploadWolfImageAsync(MultipartFileData wolfImageData)
        {
            var newFilename = Guid.NewGuid() + Path.GetExtension(wolfImageData.Headers.ContentDisposition.FileName.Replace("\"", ""));

            var blockBlob = _originalImageContainer.GetBlockBlobReference(newFilename);
            await blockBlob.UploadFromFileAsync(wolfImageData.LocalFileName);

            return blockBlob;
        }

        /// <summary>
        ///     Upload the resized version of the image
        /// </summary>
        /// <param name="content"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public static async Task<CloudBlockBlob> UploadResizedWolfImageAsync(HttpContent content, string fileName)
        {
            var blockBlob = _resizedImageContainer.GetBlockBlobReference(Path.GetFileName(fileName));

            using (var stream = await content.ReadAsStreamAsync())
            {
                await blockBlob.UploadFromStreamAsync(stream);
            }

            return blockBlob;
        }

        /// <summary>
        ///     Writes info to table storage about the wolf picture that was uploaded (assuming the picture was allowed)
        /// </summary>
        /// <param name="entity"></param>
        /// <returns></returns>
        public static async Task InsertPictureInfo(WolfImageEntity entity)
        {
            var insertOperation = TableOperation.Insert(entity);
            await _tableImageInfo.ExecuteAsync(insertOperation);
        }

        /// <summary>
        ///     Get the row info for a specific picture
        /// </summary>
        /// <param name="partitionKey"></param>
        /// <param name="fileName"></param>
        /// <returns></returns>
        /// <remarks>Remember we have different parition keys - see entity</remarks>
        public static async Task<WolfImageEntity> GetPictureInfo(string partitionKey, string fileName)
        {
            var getOperation = TableOperation.Retrieve<WolfImageEntity>(partitionKey, fileName);
            var row = await _tableImageInfo.ExecuteAsync(getOperation);

            return (WolfImageEntity) row.Result;
        }

        /// <summary>
        ///     Submit an image, by URL, to request a resize operation
        /// </summary>
        /// <param name="imageUrl"></param>
        /// <returns></returns>
        public static async Task QueueImageResize(string imageUrl)
        {
            var msg = new CloudQueueMessage(imageUrl);
            await _queueResizeImage.AddMessageAsync(msg);
        }

        /// <summary>
        ///     Returns the top 100 accepted wolf images
        /// </summary>
        /// <returns></returns>
        public static List<WolfImageUrlsEntity> GetLatest100Pictures()
        {
            // NOTE: I can't easily do an Order By with Table storage. You need to grab all the data and
            // then sort client side, which is a PITA. Furthermore you can only grab data in 1,000 row chunks at
            // a time. That's ok for this demo app but something you'd have to deal with in a real-world app
            var wolfInfo = (from entity in _tableImageInfo.CreateQuery<WolfImageEntity>()
                where entity.PartitionKey == WolfImageEntity.PartitionKey_WolfImage
                select new WolfImageUrlsEntity
                {
                    UrlOriginal = entity.OriginalFileUrl,
                    UrlResized = entity.ResizedFileUrl,
                    Ticks = entity.Timestamp.UtcTicks
                })
                .ToList();

            wolfInfo = wolfInfo.OrderByDescending(x => x.Ticks).Take(100).ToList();

            return wolfInfo;
        }

        /// <summary>
        ///     Small POCO used to help me return a type, which will eventually get serialzed to JSON
        /// </summary>
        public class WolfImageUrlsEntity
        {
            public string UrlOriginal { get; set; }
            public string UrlResized { get; set; }
            public long Ticks { get; set; }
        }
    }
}