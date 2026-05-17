using System.Text.Json;
using Aspire.Hosting;
using Microsoft.Extensions.Logging;
using WarehouseApp.Api.Models;

namespace WarehouseApp.AppHost.Tests;

/// <summary>
/// Интеграционные тесты
/// </summary>
public class IntegrationTest : IAsyncLifetime
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private DistributedApplication? _app;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        var cancellationToken = CancellationToken.None;
        IDistributedApplicationTestingBuilder builder = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.WarehouseApp_AppHost>(cancellationToken);

        builder.Configuration["DcpPublisher:RandomizePorts"] = "false";

        builder.Services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(LogLevel.Debug);
            logging.AddFilter("Aspire.Hosting.Dcp", LogLevel.Debug);
            logging.AddFilter("Aspire.Hosting", LogLevel.Debug);
        });

        _app = await builder.BuildAsync(cancellationToken);
        await _app.StartAsync(cancellationToken);
    }

    /// <summary>
    /// Вызов GET /warehouse-item?id={id} у шлюза кладёт в бакет файл
    /// warehouse_{id}.json с тем же содержимым, что и ответ шлюза
    /// </summary>
    [Fact]
    public async Task GatewayCall_ProducesFileInS3WithSameId()
    {
        var cancellationToken = CancellationToken.None;
        var id = Random.Shared.Next(1, 1000);

        using var gatewayClient = _app!.CreateHttpClient("api-gateway", "http");
        using var gatewayResponse = await gatewayClient.GetAsync($"/warehouse-item?id={id}", cancellationToken);
        gatewayResponse.EnsureSuccessStatusCode();

        var gatewayItem = JsonSerializer.Deserialize<WarehouseItem>(
            await gatewayResponse.Content.ReadAsStringAsync(cancellationToken),
            _jsonOptions);
        Assert.NotNull(gatewayItem);

        using var sinkClient = _app!.CreateHttpClient("warehouseapp-fileservice", "http");

        WarehouseItem? s3Item = null;
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var s3Response = await sinkClient.GetAsync($"/api/s3/warehouse_{id}.json", cancellationToken);
            if (s3Response.IsSuccessStatusCode)
            {
                s3Item = JsonSerializer.Deserialize<WarehouseItem>(
                    await s3Response.Content.ReadAsStringAsync(cancellationToken),
                    _jsonOptions);
                if (s3Item is not null)
                    break;
            }
            await Task.Delay(1000, cancellationToken);
        }

        Assert.NotNull(s3Item);
        Assert.Equal(id, s3Item!.Id);
        Assert.Equivalent(gatewayItem, s3Item);
    }

    /// <summary>
    /// Для нескольких запросов с разными id список ключей бакета содержит
    /// отдельный warehouse_{id}.json для каждого id
    /// </summary>
    [Fact]
    public async Task MultipleGatewayCalls_ProduceDistinctFilesInS3()
    {
        var cancellationToken = CancellationToken.None;
        var ids = Enumerable.Range(0, 3)
            .Select(_ => Random.Shared.Next(1000, 1000000))
            .Distinct()
            .ToArray();

        using var gatewayClient = _app!.CreateHttpClient("api-gateway", "http");
        foreach (var id in ids)
        {
            using var response = await gatewayClient.GetAsync($"/warehouse-item?id={id}", cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        using var sinkClient = _app!.CreateHttpClient("warehouseapp-fileservice", "http");
        var expectedKeys = ids.Select(id => $"warehouse_{id}.json").ToHashSet();

        HashSet<string> bucketKeys = [];
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var listResponse = await sinkClient.GetAsync("/api/s3", cancellationToken);
            if (listResponse.IsSuccessStatusCode)
            {
                var keys = JsonSerializer.Deserialize<List<string>>(
                    await listResponse.Content.ReadAsStringAsync(cancellationToken),
                    _jsonOptions);
                bucketKeys = keys?.ToHashSet() ?? [];
                if (expectedKeys.IsSubsetOf(bucketKeys))
                    break;
            }
            await Task.Delay(1000, cancellationToken);
        }

        Assert.Subset(bucketKeys, expectedKeys);
    }

    /// <inheritdoc/>
    public async Task DisposeAsync()
    {
        if (_app is not null)
        {
            await _app.StopAsync();
            await _app.DisposeAsync();
        }
    }
}
