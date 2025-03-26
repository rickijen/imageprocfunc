using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Net.Http.Json;

namespace Company.Function
{
    public class Payload
    {
        public required string url { get; set; }

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
            }
        }
    }
}
