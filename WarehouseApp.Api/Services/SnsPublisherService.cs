using System.Net;
using System.Text.Json;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using WarehouseApp.Api.Models;

namespace WarehouseApp.Api.Services;

/// <inheritdoc cref="ISnsPublisherService"/>
/// <param name="client">Клиент SNS</param>
/// <param name="configuration">Конфигурация приложения</param>
/// <param name="logger">Логгер</param>
public class SnsPublisherService(
    IAmazonSimpleNotificationService client,
    IConfiguration configuration,
    ILogger<SnsPublisherService> logger) : ISnsPublisherService
{
    private readonly string _topicArn = configuration["AWS:Resources:SNSTopicArn"]
        ?? throw new KeyNotFoundException("SNS topic ARN was not found in configuration");

    /// <inheritdoc/>
    public async Task PublishAsync(WarehouseItem item)
    {
        try
        {
            var json = JsonSerializer.Serialize(item);
            var request = new PublishRequest
            {
                Message = json,
                TopicArn = _topicArn
            };
            var response = await client.PublishAsync(request);
            if (response.HttpStatusCode == HttpStatusCode.OK)
                logger.LogInformation("Warehouse item {Id} was published to SNS", item.Id);
            else
                throw new InvalidOperationException($"SNS returned {response.HttpStatusCode}");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to publish warehouse item {Id} to SNS topic", item.Id);
        }
    }
}
