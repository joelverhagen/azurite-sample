using System;
using System.Threading.Tasks;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Xunit;

namespace TestWithAzurite;

public class StorageTests
{
    private readonly string _connectionString;
    private readonly string _storagePrefix;

    public StorageTests()
    {
        // this uses the same host (127.0.0.1) and port defaults as Azurite
        _connectionString = "UseDevelopmentStorage=true";

        // provide isolation for multiple test runs
        _storagePrefix = "t" + DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    [Fact]
    public async Task Blobs()
    {
        // arrange
        BlobServiceClient client = new BlobServiceClient(_connectionString);
        BlobContainerClient container = client.GetBlobContainerClient(_storagePrefix + "foo");
        await container.CreateAsync();
        BlobClient blob = container.GetBlobClient("myblob");

        // act
        string expected = "my blob content";
        await blob.UploadAsync(new BinaryData(expected));
        
        // assert
        BlobDownloadResult actual = await blob.DownloadContentAsync();
        Assert.Equal(expected, actual.Content.ToString());
    }

    [Fact]
    public async Task Queues()
    {
        // arrange
        QueueServiceClient client = new QueueServiceClient(_connectionString);
        QueueClient queue = client.GetQueueClient(_storagePrefix + "foo");
        await queue.CreateAsync();

        // act
        string expected = "my queue content";
        await queue.SendMessageAsync(expected);

        // assert
        QueueMessage actual = await queue.ReceiveMessageAsync();
        Assert.Equal(expected, actual.Body.ToString());
    }

    [Fact]
    public async Task Tables()
    {
        // arrange
        TableServiceClient client = new TableServiceClient(_connectionString);
        TableClient table = client.GetTableClient(_storagePrefix + "foo");
        await table.CreateAsync();

        // act
        string expected = "my table content";
        TableEntity entity = new TableEntity("pk", "rk");
        entity["MyProperty"] = expected;
        await table.AddEntityAsync(entity);

        // assert
        TableEntity actual = await table.GetEntityAsync<TableEntity>("pk", "rk");
        Assert.Equal(expected, actual["MyProperty"]);
    }
}