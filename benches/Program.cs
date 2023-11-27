using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Extensions;
using BenchmarkDotNet.Running;
using System.Diagnostics;
using System.Net;
using System.Text;

var _ = BenchmarkRunner.Run<AzuriteInMemoryVsDisk>();

[CategoriesColumn]
[GroupBenchmarksBy(BenchmarkLogicalGroupRule.ByCategory)]
[IterationTime(1000)]
[WarmupCount(5)]
[IterationCount(30)]
public class AzuriteInMemoryVsDisk
{
    public int DataSize { get; set; } = 8 * 1024;
    private byte[] Data = new byte[0];

    private Process? _azuriteProcess;
    private BlobServiceClient? _blobServiceClient;
    private BlobContainerClient? _blobContainer;
    private QueueServiceClient? _queueServiceClient;
    private QueueClient? _queue;
    private TableServiceClient? _tableServiceClient;
    private TableClient? _table;

    private async Task InitializeAzuriteAsync(string arguments)
    {
        Data = new byte[DataSize];
        new Random(0).NextBytes(Data);
        Data = Encoding.ASCII.GetBytes(Convert.ToBase64String(Data)).Take(DataSize).ToArray();

        await StartAzuriteAsync(arguments);
        await InitializeClientsAsync();
    }

    private async Task InitializeClientsAsync()
    {
        var connectionString = "UseDevelopmentStorage=true";
        _blobServiceClient = new BlobServiceClient(connectionString, new BlobClientOptions { Retry = { NetworkTimeout = TimeSpan.FromSeconds(1), MaxRetries = 0 } });
        _blobContainer = _blobServiceClient.GetBlobContainerClient("testcontainer");
        await _blobContainer.CreateIfNotExistsAsync();

        _queueServiceClient = new QueueServiceClient(connectionString, new QueueClientOptions { Retry = { NetworkTimeout = TimeSpan.FromSeconds(1), MaxRetries = 0 } });
        _queue = _queueServiceClient.GetQueueClient("testqueue");
        await _queue.CreateIfNotExistsAsync();

        _tableServiceClient = new TableServiceClient(connectionString, new TableClientOptions { Retry = { NetworkTimeout = TimeSpan.FromSeconds(1), MaxRetries = 0 } });
        _table = _tableServiceClient.GetTableClient("testtable");
        await _table.CreateIfNotExistsAsync();
    }

    private async Task StartAzuriteAsync(string arguments)
    {
        _azuriteProcess = new Process
        {
            StartInfo =
            {
                FileName = "azurite",
                UseShellExecute = true,
                Arguments = arguments,
            },
        };

        Console.WriteLine($"Runing '{_azuriteProcess.StartInfo.FileName} {_azuriteProcess.StartInfo.Arguments}'...");
        _azuriteProcess.Start();

        var sw = Stopwatch.StartNew();
        using var httpClient = new HttpClient();
        while (sw.Elapsed < TimeSpan.FromSeconds(30))
        {
            await Task.Delay(TimeSpan.FromSeconds(1));

            var ready = true;
            foreach (var port in Enumerable.Range(10000, 3))
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, $"http://127.0.0.1:{port}/");
                try
                {
                    using var response = await httpClient.SendAsync(request);
                    if (response.StatusCode != HttpStatusCode.BadRequest)
                    {
                        response.EnsureSuccessStatusCode();
                    }

                    Console.WriteLine($"Port {port} is ready.");
                }
                catch
                {
                    Console.WriteLine($"Port {port} is not ready yet.");
                    ready = false;
                    break;
                }
            }

            if (ready)
            {
                break;
            }
        }
    }


    [GlobalSetup(Targets = [nameof(InMemory_Blob), nameof(InMemory_Queue), nameof(InMemory_Table)])]
    public Task InMemory_Setup() => InitializeAzuriteAsync("--silent --inMemoryPersistence");

    [GlobalCleanup(Targets = [nameof(InMemory_Blob), nameof(InMemory_Queue), nameof(InMemory_Table), nameof(Disk_Blob), nameof(Disk_Queue), nameof(Disk_Table)])]
    public void InMemory_Cleanup() => _azuriteProcess?.KillTree();

    [GlobalSetup(Targets = [nameof(Disk_Blob), nameof(Disk_Queue), nameof(Disk_Table)])]
    public Task Disk_Setup() => InitializeAzuriteAsync($"--silent --location \"{Path.GetFullPath("azurite-data")}\"");

    [BenchmarkCategory("Blob"), Benchmark(Baseline = true)]
    public Task Disk_Blob() => Blob();

    [BenchmarkCategory("Blob"), Benchmark]
    public Task InMemory_Blob() => Blob();

    [BenchmarkCategory("Queue"), Benchmark(Baseline = true)]
    public Task Disk_Queue() => Queue();

    [BenchmarkCategory("Queue"), Benchmark]
    public Task InMemory_Queue() => Queue();

    [BenchmarkCategory("Table"), Benchmark(Baseline = true)]
    public Task Disk_Table() => Table();

    [BenchmarkCategory("Table"), Benchmark]
    public Task InMemory_Table() => Table();

    private async Task Blob()
    {
        try
        {
            var blob = _blobContainer!.GetBlobClient("testblob");
            await blob.UploadAsync(new BinaryData(Data), overwrite: true);
            await blob.SetMetadataAsync(new Dictionary<string, string> { { "foo", "bar" } });
            await blob.DeleteAsync();
        }
        catch (TaskCanceledException)
        {
            return;
        }
    }

    private async Task Queue()
    {
        try
        {
            await _queue!.SendMessageAsync(new BinaryData(Data));
            QueueMessage message = await _queue.ReceiveMessageAsync();
            await _queue.DeleteMessageAsync(message.MessageId, message.PopReceipt);
        }
        catch (TaskCanceledException)
        {
            return;
        }
    }

    private async Task Table()
    {
        try
        {
            var entity = new TableEntity("pk", "rk");
            entity["Data"] = Data;
            await _table!.UpsertEntityAsync(entity);
            var results = _table.QueryAsync<TableEntity>(x => x.PartitionKey == "pk");
            await foreach (var x in results)
            {
            }
        }
        catch (TaskCanceledException)
        {
            return;
        }
    }
}