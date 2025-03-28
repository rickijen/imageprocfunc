using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Net.Http.Json;
using Microsoft.Azure.Cosmos;

namespace Company.Function
{
    public class Payload
    {
        public required string url { get; set; }

    }

    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);

    public class AnalyzeResult
    {
        public string version { get; set; }
        public string modelVersion { get; set; }
        public List<ReadResult> readResults { get; set; }
    }

    public class Appearance
    {
        public Style style { get; set; }
    }

    public class Line
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public Appearance appearance { get; set; }
        public List<Word> words { get; set; }
    }

    public class ReadResult
    {
        public int page { get; set; }
        public double angle { get; set; }
        public int width { get; set; }
        public int height { get; set; }
        public string unit { get; set; }
        public List<Line> lines { get; set; }
    }

    public class Root
    {
        public string status { get; set; }
        public DateTime createdDateTime { get; set; }
        public DateTime lastUpdatedDateTime { get; set; }
        public AnalyzeResult analyzeResult { get; set; }
    }

    public class Style
    {
        public string name { get; set; }
        public double confidence { get; set; }
    }

    public class Word
    {
        public List<int> boundingBox { get; set; }
        public string text { get; set; }
        public double confidence { get; set; }
    }

    public class BlobTrigger1
    {
        private readonly ILogger<BlobTrigger1> _logger;

        public BlobTrigger1(ILogger<BlobTrigger1> logger)
        {
            _logger = logger;
        }

        [Function(nameof(BlobTrigger1))]
        public async Task Run([BlobTrigger("images/{name}", Connection = "saimageproc2025_STORAGE")] Stream stream, string name)
        {
            using var blobStreamReader = new StreamReader(stream);
            var content = await blobStreamReader.ReadToEndAsync();
            
            var connectionString = Environment.GetEnvironmentVariable("saimageproc2025_STORAGE");
            BlobServiceClient blobServiceClient = new BlobServiceClient(connectionString);

            // Get a BlobContainerClient for the container
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("images");

            // Get a BlobClient for the blob
            BlobClient blobClient = containerClient.GetBlobClient(name);

            // Create a SAS token that's valid for one day
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = containerClient.Name,
                BlobName = blobClient.Name,
                Resource = "b"
            };

            sasBuilder.ExpiresOn = DateTimeOffset.UtcNow.AddDays(1);
            sasBuilder.SetPermissions(BlobContainerSasPermissions.Read);

            Uri sasURI = blobClient.GenerateSasUri(sasBuilder);

            // Create the HttpClient
            var client = new HttpClient();
            var client_result = new HttpClient();
            var targetUrl = Environment.GetEnvironmentVariable("saimageproc2025_AIVISION");

            // Set the base address to simplify maintenance & requests
            client.BaseAddress = new Uri(targetUrl);

            // Create an object
            var payload = new Payload() { url = sasURI.AbsoluteUri};

            var response = await client.PostAsJsonAsync(client.BaseAddress, payload);

            if (response.Headers.Contains("Operation-Location"))
            {
                var operationlocation = response.Headers.GetValues("Operation-Location").First();

                _logger.LogInformation($"C# Blob trigger function Processed image\n Name: {sasURI}\n To: {targetUrl}\n Response: {operationlocation}");

                // Delay 5 secs to allow backend processing
                await Task.Delay(5000);

                client_result.BaseAddress = new Uri(operationlocation);

                Root? json_root = await client.GetFromJsonAsync<Root>(client_result.BaseAddress);

                CosmosClient cosmosClient = new CosmosClient(
                            Environment.GetEnvironmentVariable("CosmosDBConnectionString"), 
                            new CosmosClientOptions()
                            {
                                ApplicationRegion = Regions.WestUS2,
                            });

                var CosmosDb = Environment.GetEnvironmentVariable("CosmosDb");
                var CosmosContainer = Environment.GetEnvironmentVariable("CosmosContainer");
                //var container = cosmosClient.GetContainer(Environment.GetEnvironmentVariable("CosmosDb"), Environment.GetEnvironmentVariable("CosmosContainer"));
                var container = cosmosClient.GetContainer(CosmosDb, CosmosContainer);
                var cosmosresponse = await container.UpsertItemAsync(json_root);
            }
        }
    }
}
