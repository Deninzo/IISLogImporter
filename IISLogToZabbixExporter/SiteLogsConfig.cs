using Newtonsoft.Json;

namespace IISLogToZabbixExporter
{
    [JsonObject]
    public class SiteLogsConfig
    {
        [JsonProperty("siteName")]
        public string SiteName { get; set; }
        
        [JsonProperty]
        public string Path { get; set; }
        
        [JsonProperty]
        public int IndexOfStatusCode { get; set; }
        
        [JsonProperty]
        public int CurrentRow { get; set; }
    }
}