using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure;
using Newtonsoft.Json;

namespace WolfTrackerAPI.Helpers
{
    internal static class SmartResizeImage
    {
        private static readonly string UrlBase;
        private static readonly string SubscriptionKey;

        static SmartResizeImage()
        {
            UrlBase = CloudConfigurationManager.GetSetting("CognitiveServicesVisionUrl");
            SubscriptionKey = CloudConfigurationManager.GetSetting("CognitiveServicesVisionKey");
        }

        public static async Task<string> MakeThumbNailRequest(string wolfImageBlobUrl)
        {
            var client = new HttpClient();

            // Setup my queue in the request header
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);

            var imageInfo = new ImageInfo
            {
                url = wolfImageBlobUrl
            };

            var content = new StringContent(JsonConvert.SerializeObject(imageInfo), Encoding.UTF8, "application/json");

            // Note the smartCropping parameter which will use cognitive services to intelligently crop using Cognitive Services
            var cognitiveUrl = UrlBase + "generateThumbnail?width=400&height=300&smartCropping=true";

            var response = await client.PostAsync(cognitiveUrl, content);

            if (!response.IsSuccessStatusCode) return null;

            var resizedImageBlob = await AzureStorageHelper.UploadResizedWolfImageAsync(response.Content, wolfImageBlobUrl);
            return resizedImageBlob.Uri.ToString();
        }
    }
}