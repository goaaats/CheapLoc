using Newtonsoft.Json;

namespace CheapLoc
{
    public class LocEntry
    {
        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }
    }
}
