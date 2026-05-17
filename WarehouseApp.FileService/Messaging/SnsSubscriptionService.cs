using System.Net;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace WarehouseApp.FileService.Messaging;

/// <summary>
/// Служба для подписки на SNS-топик при старте приложения
/// </summary>
/// <param name="snsClient">Клиент SNS</param>
/// <param name="configuration">Конфигурация</param>
/// <param name="logger">Логгер</param>
public class SnsSubscriptionService(
    IAmazonSimpleNotificationService snsClient,
    IConfiguration configuration,
    ILogger<SnsSubscriptionService> logger)
{
    private readonly string _topicArn = configuration["AWS:Resources:SNSTopicArn"]
        ?? throw new KeyNotFoundException("SNS topic ARN was not found in configuration");

    /// <summary>
    /// Подписывает HTTP-эндпоинт текущего сервиса на SNS-топик
    /// </summary>
    public async Task SubscribeEndpoint()
    {
        logger.LogInformation("Sending subscribe request for {Topic}", _topicArn);
        var endpoint = configuration["AWS:Resources:SNSUrl"]
            ?? throw new KeyNotFoundException("SNS subscription endpoint URL was not found in configuration");

        var request = new SubscribeRequest
        {
            TopicArn = _topicArn,
            Protocol = "http",
            Endpoint = endpoint,
            ReturnSubscriptionArn = true
        };
        var response = await snsClient.SubscribeAsync(request);
        if (response.HttpStatusCode != HttpStatusCode.OK)
            logger.LogError("Failed to subscribe to {Topic}", _topicArn);
        else
            logger.LogInformation("Subscription request for {Topic} succeeded, waiting for confirmation", _topicArn);
    }
}
