using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using Serilog;
using Serilog.Core;

namespace IISLogToZabbixExporter
{
    class Program
    {
        private static Logger _logger;
        private static IConfiguration _configuration;
        private static List<SiteLogsConfig> _sites;
        private static CancellationTokenSource _cancelTokenSource;
        
        static async Task Main(string[] args)
        {
            try
            {
                Console.CancelKeyPress += (obj, e) => SavePositions();
                
                _configuration = new ConfigurationBuilder()
                    .AddJsonFile("appsettings.json", true, true)
                    .Build();
                
                _logger = new LoggerConfiguration()
                    .WriteTo.File("logs.log")
                    .CreateLogger();
                
                _cancelTokenSource = new CancellationTokenSource();
                
                _sites = _configuration.GetSection("sites")
                    .Get<List<SiteLogsConfig>>();
                
                _logger.Information("Старт чтения логов");
                
                var result = _sites
                    .Select(async c => await ReadLogs(c, _cancelTokenSource.Token))
                    .ToList();
                
                while (!result.All(t => t.IsCompleted))
                {
                    if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Escape)
                    {
                        _cancelTokenSource.Cancel();
                    }
                }
                
                var parseResults = await Task.WhenAll(result);
                
                Console.WriteLine(JsonConvert.SerializeObject(parseResults.Select(ConvertToDictionary), Formatting.Indented));
                
                SavePositions();
            }
            catch (Exception e)
            {
                _logger.Error(e.Message);
            }
        }
        
        private static async Task<ParseResult> ReadLogs(SiteLogsConfig config, CancellationToken token)
        {
            var stream = GetStreamReaderLogFile(config);
            
            await SkipRows(config.CurrentRow, stream);
            
            var result = new ParseResult
            {
                SiteName = config.SiteName,
                CountOfCodes = new Dictionary<string, int>()
            };
            
            while (!stream.EndOfStream)
            {
                try
                {
                    if (token.IsCancellationRequested)
                    {
                        stream.Close();
                        return result;
                    }

                    var line = await stream.ReadLineAsync();
                    config.CurrentRow++;

                    if (line == null)
                    {
                        _logger.Warning("Получена null-строка");
                        continue;
                    }

                    if (line.StartsWith('#'))
                        continue;

                    var split = line.Split(' ');

                    var statusCode = split[config.IndexOfStatusCode][0] + "XX";

                    if (result.CountOfCodes.TryGetValue(statusCode, out var value))
                    {
                        result.CountOfCodes[statusCode] = ++value;
                    }
                    else
                    {
                        result.CountOfCodes.Add(statusCode, 1);
                    }
                }
                catch (Exception e)
                {
                    _logger.Error(e.Message);
                }
            }
            
            config.CurrentRow = 0;
            stream.Close();
            return result;
        }

        private static StreamReader GetStreamReaderLogFile(SiteLogsConfig config)
        {
            var logFile = new DirectoryInfo(config.Path)
                .GetFiles()
                .OrderByDescending(f => f.LastWriteTime)
                .FirstOrDefault();
            
            if(logFile == null)
            {
                throw new NullReferenceException("В каталоге с логами сайта нет файлов!");
            }
            
            return new StreamReader(File.Open(logFile.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        }

        private static async Task SkipRows(int count, StreamReader reader)
        {
            for (int i = 0; i < count; i++)
            {
                await reader.ReadLineAsync();
            }
        }

        private static void SavePositions()
        {
            _cancelTokenSource.Cancel();
            
            _logger.Information("Завершение чтения логов");

            var baseDirectory = AppContext.BaseDirectory + "appsettings.json";

            var serializedString = JsonConvert.SerializeObject(new
            {
                Sites = _sites
            }, Formatting.Indented);
            
            File.WriteAllText(baseDirectory, serializedString);
        }

        private static Dictionary<string, int> ConvertToDictionary(ParseResult parseResult)
        {
            var resultDict = new Dictionary<string, int>();
            
            if (parseResult.CountOfCodes.TryGetValue("2XX", out var value2xx))
            {
                resultDict.Add(parseResult.SiteName + "_2XX_count_codes", value2xx);
            }
            
            if (parseResult.CountOfCodes.TryGetValue("3XX", out var value3xx))
            {
                resultDict.Add(parseResult.SiteName + "_3XX_count_codes", value3xx);
            }
            
            if (parseResult.CountOfCodes.TryGetValue("4XX", out var value4xx))
            {
                resultDict.Add(parseResult.SiteName + "_4XX_count_codes", value4xx);
            }
            
            if (parseResult.CountOfCodes.TryGetValue("5XX", out var value5xx))
            {
                resultDict.Add(parseResult.SiteName + "_5XX_count_codes", value5xx);
            }

            return resultDict;
        }
    }
}
