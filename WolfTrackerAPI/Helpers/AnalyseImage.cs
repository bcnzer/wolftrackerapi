using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure;
using Microsoft.Azure.WebJobs.Host;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace WolfTrackerAPI.Helpers
{
    /// <summary>
    ///     Use Cognitive Services to determine if the image is a wolf
    /// </summary>
    internal static class AnalyseImage
    {
        private const double MinimumConfidence = 0.6; // Must be at least 60% confident that it's a wolf
        private static readonly string UrlBase;
        private static readonly string SubscriptionKey;

        static AnalyseImage()
        {
            UrlBase = CloudConfigurationManager.GetSetting("CognitiveServicesVisionUrl");
            SubscriptionKey = CloudConfigurationManager.GetSetting("CognitiveServicesVisionKey");
        }

        public static async Task<FullImageAnalysis> MakeAnalysisRequest(string wolfImageBlobPath, TraceWriter log)
        {
            var client = new HttpClient();

            // Setup my queue in the request header
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", SubscriptionKey);

            var imageInfo = new ImageInfo
            {
                url = wolfImageBlobPath
            };

            var content = new StringContent(JsonConvert.SerializeObject(imageInfo), Encoding.UTF8, "application/json");

            // Assemble the URI for the REST API Call. Note the parameters - I'm asking for specific pieces of info
            var url = UrlBase + "analyze?visualFeatures=Tags,Adult,Description";

            // Make the request
            var response = await client.PostAsync(url, content);

            // Parse the resulting JSON. See the file in the SampleJSON folder for an idea of what it can look like
            var contentString = await response.Content.ReadAsStringAsync();
            var cognitiveInfo = JObject.Parse(contentString);

            var analysisResults = new SimplifiedImageAnalysis
            {
                ImageDescription = (string)cognitiveInfo["description"]["captions"][0]["text"],
                ImageDescriptionConfidence = (double) cognitiveInfo["description"]["captions"][0]["confidence"],
                IsAdult = (bool) cognitiveInfo["adult"]["isAdultContent"],
                IsRacy = (bool) cognitiveInfo["adult"]["isRacyContent"]
            };

            foreach (var element in cognitiveInfo["tags"])
                if ((string) element["name"] == "wolf" && (double) element["confidence"] > MinimumConfidence)
                {
                    analysisResults.IsWolf = true;
                    analysisResults.WolfConfidence = (double) element["confidence"];
                    break;
                }

            var fullResults = new FullImageAnalysis
            {
                CognitiveJson = contentString,
                Response = analysisResults
            };

            return fullResults;
        }
    }

    /// <summary>
    ///     Small class I use to serialize to JSON, so I can use it to Cognitive Services
    /// </summary>
    internal class ImageInfo
    {
        public string url { get; set; }
    }

    /// <summary>
    ///     Used so I can return the full JSON and the simplified response - but keep them seperate
    /// </summary>
    public class FullImageAnalysis
    {
        public string CognitiveJson { get; set; }
        public SimplifiedImageAnalysis Response { get; set; }
    }

    /// <summary>
    ///     This class will be serialized to JSON and sent back as part of the body to my front end. Only contains the sub-set
    ///     of fields I care about for my UI
    /// </summary>
    public class SimplifiedImageAnalysis
    {
        public bool IsAdult { get; set; }
        public bool IsRacy { get; set; }
        public bool IsWolf { get; set; }
        public double WolfConfidence { get; set; }
        public string ImageDescription { get; set; }
        public double ImageDescriptionConfidence { get; set; }
    }
}