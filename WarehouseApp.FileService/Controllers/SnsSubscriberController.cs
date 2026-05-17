using System.Text;
using Amazon.SimpleNotificationService.Util;
using Microsoft.AspNetCore.Mvc;
using WarehouseApp.FileService.Storage;

namespace WarehouseApp.FileService.Controllers;

/// <summary>
/// Контроллер для приёма SNS-уведомлений и подтверждения подписки
/// </summary>
/// <param name="s3Service">Служба для работы с S3</param>
/// <param name="configuration">Конфигурация</param>
/// <param name="logger">Логгер</param>
[ApiController]
[Route("api/sns")]
public class SnsSubscriberController(
    IS3Service s3Service,
    IConfiguration configuration,
    ILogger<SnsSubscriberController> logger) : ControllerBase
{
    private readonly string _localstackHost = configuration["AWS:Resources:LocalStackHost"]
        ?? throw new KeyNotFoundException("LocalStack host was not found in configuration");

    private readonly int _localstackPort = configuration.GetValue<int?>("AWS:Resources:LocalStackPort")
        ?? throw new KeyNotFoundException("LocalStack port was not found in configuration");

    /// <summary>
    /// Вебхук для приёма сообщений из SNS-топика. Также используется
    /// для подтверждения подписки при инициализации информационного обмена
    /// </summary>
    /// <remarks>В любом случае возвращает 200</remarks>
    [HttpPost]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ReceiveMessage()
    {
        logger.LogInformation("SNS webhook was called");
        try
        {
            using var reader = new StreamReader(Request.Body, Encoding.UTF8);
            var jsonContent = await reader.ReadToEndAsync();

            var snsMessage = Message.ParseMessage(jsonContent);

            if (snsMessage.Type == "SubscriptionConfirmation")
            {
                logger.LogInformation("SubscriptionConfirmation was received");
                using var httpClient = new HttpClient();
                var builder = new UriBuilder(new Uri(snsMessage.SubscribeURL))
                {
                    Scheme = "http",
                    Host = _localstackHost,
                    Port = _localstackPort
                };
                var response = await httpClient.GetAsync(builder.Uri);
                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    throw new InvalidOperationException(
                        $"SubscriptionConfirmation returned {response.StatusCode}: {body}");
                }
                logger.LogInformation("Subscription was successfully confirmed");
                return Ok();
            }

            if (snsMessage.Type == "Notification")
            {
                await s3Service.UploadFile(snsMessage.MessageText);
                logger.LogInformation("Notification was successfully processed");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred while processing SNS notification");
        }
        return Ok();
    }
}
