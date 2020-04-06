using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
