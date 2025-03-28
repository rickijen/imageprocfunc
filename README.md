<p align="center">
<img title="" src="https://learn.microsoft.com/en-us/azure/architecture/ai-ml/idea/_images/architecture-intelligent-apps-image-processing.png?raw=true" alt="main-pic" data-align="center">
</p>
This is an Azure Function App triggered by Blob storage events from images uploaded into a container:
1. Once the Function is triggred, it will send the image URL as JSON payload to AI Vision backend for OCR. The AI VISION is running on Azure container instance.
2. The HTTP POST operation to AI VISON is asynchronous to allow backend sufficient time to process OCR. Then issue another HTTP GET to retrieve the result in JSON.
3. Once the JSON from OCR is returned, the JSON data is store into a C# class to be inserted into Cosmos DB.
4. With the NoSQL database and container created already, we will use the CosmosClient to create a new item (JSON from step 3) in the Cosmos DB container.
5. Now that the OCR output JSON is persisted in Cosmos DB, we will be able to query with SQL syntax, e.g.: SELECT * FROM c WHERE c.createdDateTime = "2025-03-28T21:58:14Z"
