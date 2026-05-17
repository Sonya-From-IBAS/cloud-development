using WarehouseApp.Api.Models;

namespace WarehouseApp.Api.Services;

/// <summary>
/// Сервис публикации товара в SNS-топик
/// </summary>
public interface ISnsPublisherService
{
    /// <summary>
    /// Публикует сериализованный товар в SNS-топик
    /// </summary>
    /// <param name="item">Товар на складе</param>
    public Task PublishAsync(WarehouseItem item);
}
