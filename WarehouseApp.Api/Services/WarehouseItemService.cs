using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using WarehouseApp.Api.Generation;
using WarehouseApp.Api.Models;

namespace WarehouseApp.Api.Services;

/// <inheritdoc cref="IWarehouseItemService"/>
/// <param name="cache">Кэш</param>
/// <param name="logger">Логгер</param>
/// <param name="configuration">Конфигурация приложения</param>
public class WarehouseItemService(IDistributedCache cache, ILogger<WarehouseItemService> logger, IConfiguration configuration)
    : IWarehouseItemService
{
    private const string KeyPrefix = "warehouse-item:";

    private readonly int _cacheExpirationMinutes = configuration.GetValue("CacheExpirationMinutes", 10);

    /// <inheritdoc/>
    public async Task<WarehouseItem> GetOrGenerate(int id)
    {
        var cached = await TryGetFromCache(id);
        if (cached is not null)
            return cached;

        logger.LogInformation("Cache miss for item {Id}, generating...", id);

        var item = WarehouseItemGenerator.Generate(id);
        logger.LogInformation("Successfully generated item {Id}", id);

        await TrySaveToCache(id, item);
        return item;
    }

    /// <summary>
    /// Пытается получить товар из кэша
    /// </summary>
    /// <param name="id">Идентификатор товара в системе</param>
    /// <returns>Товар из кэша или <see langword="null"/>, если запись отсутствует или произошла ошибка</returns>
    private async Task<WarehouseItem?> TryGetFromCache(int id)
    {
        try
        {
            var data = await cache.GetStringAsync(KeyPrefix + id);

            if (data is null)
                return null;

            logger.LogInformation("Cache hit for item {Id}", id);
            return JsonSerializer.Deserialize<WarehouseItem>(data);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to get item {Id} from cache: {Error}", id, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Сохраняет товар в кэш
    /// </summary>
    /// <param name="id">Идентификатор товара в системе</param>
    /// <param name="item">Товар для сохранения</param>
    private async Task TrySaveToCache(int id, WarehouseItem item)
    {
        try
        {
            var data = JsonSerializer.Serialize(item);
            await cache.SetStringAsync(KeyPrefix + id, data, new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_cacheExpirationMinutes)
            });

            logger.LogInformation("Item {Id} saved to cache", id);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save item {Id} to cache: {Error}", id, ex.Message);
        }
    }

}
