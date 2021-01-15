using System.Collections.Generic;

namespace IISLogToZabbixExporter
{
    public class ParseResult
    {
        public string SiteName { get; set; }
        public Dictionary<string, int> CountOfCodes { get; set; }
    }
}