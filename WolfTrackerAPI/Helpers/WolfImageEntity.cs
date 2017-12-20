using Microsoft.WindowsAzure.Storage.Table;

namespace WolfTrackerAPI.Helpers
{
    /// <inheritdoc />
    /// <summary>
    ///     Entity (similar to an Entity Framework POCO) used by Table Storage to store all the details about the uploaded
    ///     images
    /// </summary>
    /// <remarks>
    ///     I have three versions of the entity. The first records images that aren't wolves or something naughty (adult/racy).
    ///     The second is a record for an original image but one that has yet to be resized. The last is the final version
    ///     which I will use when getting a list of all my wolves
    /// </remarks>
    public class WolfImageEntity : TableEntity
    {
        public const string PartitionKey_WolfImage = "WolfImage"; // Has original and resized
        public const string PartitionKey_AwaitResizing = "WolfImageNotResized";
        private const string PartitionKey_NotWolfImage = "NotWolfImage";

        public WolfImageEntity()
        {
            // This is mandatory but unused
        }

        /// <summary>
        ///     Constructor used when we're uploading the original
        /// </summary>
        /// <param name="isWolfImage"></param>
        /// <param name="fileName"></param>
        public WolfImageEntity(bool isWolfImage, string fileName)
        {
            PartitionKey = isWolfImage ? PartitionKey_AwaitResizing : PartitionKey_NotWolfImage;
            RowKey = fileName;
            IsWolf = isWolfImage;
        }

        public string Id { get; set; } // It's a GUID, which is also used in the filename
        public string UserName { get; set; } // From Auth0 i.e. twitter|2349543590

        public string OriginalFileUrl { get; set; }
        public string ResizedFileUrl { get; set; }

        // The key pieces of info that came from Cognitive Services
        public bool IsWolf { get; set; }

        public double WolfConfidence { get; set; }
        public string ImageDescription { get; set; }
        public double ImageDescriptionConfidence { get; set; }
        public bool IsAdult { get; set; }
        public bool IsRacy { get; set; }

        // The full JSON from Cognitive Services
        public string CognitiveJson { get; set; }
    }
}