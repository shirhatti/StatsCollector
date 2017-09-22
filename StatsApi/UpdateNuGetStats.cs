using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using System.Net.Http;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Linq;

namespace StatsApi
{
    public static class UpdateNuGetStats
    {
        private static HttpClient Client = new HttpClient();
        private static readonly Uri PackageCsvUri = new Uri("https://raw.githubusercontent.com/aspnet/Coherence/dev/packages/packages.csv");
        private static readonly string NuGetStatsUri = "https://www.nuget.org/stats/reports/packages/{0}";

        [FunctionName("UpdateNuGetStats")]
        public static async System.Threading.Tasks.Task RunAsync([TimerTrigger("0 0 0 * * ?")]TimerInfo myTimer, TraceWriter log)
        {
            log.Info("Retrieveing packages.csv at ", DateTime.Now.ToString());
            var packageNames = new List<string>();
            var packageStats = new Dictionary<Package, int>();

            /********* START *********/

            // Get list of packages that we ship from the packages.csv file in GitHub
            using (var response = await Client.GetAsync(PackageCsvUri, HttpCompletionOption.ResponseHeadersRead))
            {
                if (!response.IsSuccessStatusCode)
                {
                    log.Error("Unable to retrieve list of packages");
                    throw new Exception("Unable to retrieve list of packages");
                }

                using (var stream = await response.Content.ReadAsStreamAsync())
                using (var streamReader = new StreamReader(stream))
                {
                    while (true)
                    {
                        var line = streamReader.ReadLine();
                        if (line == null)
                        {
                            break;
                        }
                        var lineSegments = line.Split(',');
                        if (lineSegments.Length != 3)
                        {
                            throw new Exception("Malformed data in packages.csv");
                        }

                        // TODO: Need a better way of handling this
                        if (string.Equals(lineSegments[0], "Microsoft.AspNetCore.Protocols.Abstractions") ||
                            string.Equals(lineSegments[0], "Microsoft.Extensions.Hosting"))
                        {
                            continue;
                        }

                        if (string.Equals(lineSegments[1], "ship"))
                        {
                            packageNames.Add(lineSegments[0]);
                        }
                    }
                }
            }

            // Query NuGet to download stats for every package that we ship
            log.Info("Starting NuGet stat generation at ", DateTime.Now.ToString());
            foreach (var packageName in packageNames)
            {
                var Uri = new Uri(string.Format(NuGetStatsUri, packageName));

                using (var response = await Client.GetAsync(Uri, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        log.Error("Unable to retrieve list of packages");
                        throw new Exception("Unable to retrieve list of packages");
                    }

                    using (var stream = await response.Content.ReadAsStreamAsync())
                    using (var streamReader = new StreamReader(stream))
                    using (var jsonReader = new JsonTextReader(streamReader))
                    {
                        try
                        {
                            JObject nugetStatsResponse = (JObject)JObject.ReadFrom(jsonReader);
                            IList<JToken> statTokens = nugetStatsResponse["Facts"].Children().ToList();

                            foreach (var statToken in statTokens)
                            {
                                var downloadCount = (int)statToken["Amount"];
                                var package = new Package
                                {
                                    PackageName = packageName,
                                    Version = (string)statToken["Dimensions"]["Version"]
                                };
                                if (packageStats.ContainsKey(package))
                                {
                                    packageStats[package] += downloadCount;
                                }
                                else
                                {
                                    packageStats.Add(package, downloadCount);
                                }
                            }
                        }
                        catch (Exception e)
                        {
                            log.Error(e.Message);
                            continue;
                        }

                    }
                }
            }

            JsonSerializerSettings settings = new JsonSerializerSettings { Converters = new[] { new PackageDictionaryJsonConverter() } };
            string json = JsonConvert.SerializeObject(packageStats, settings);

            // Archive this data in blob storage
            log.Info("Saving to Azure blob storage at ", DateTime.Now.ToString());
            string storageConnectionString = Environment.GetEnvironmentVariable("StorageConnectionString");

            CloudStorageAccount account = CloudStorageAccount.Parse(storageConnectionString);
            CloudBlobClient serviceClient = account.CreateCloudBlobClient();

            var container = serviceClient.GetContainerReference("nugetstats");
            await container.CreateIfNotExistsAsync();

            CloudBlockBlob blob = container.GetBlockBlobReference(DateTime.Now.ToShortDateString() + ".json");
            await blob.UploadTextAsync(json);
            /********** END **********/
            return;
        }
    }
}
