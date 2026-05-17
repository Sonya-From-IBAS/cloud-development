using Ocelot.LoadBalancer.Interfaces;
using Ocelot.Responses;
using Ocelot.Values;

namespace Api.Gateway;

/// <summary>
/// Балансировщик нагрузки «Взвешенный случайный выбор» (Weighted Random).
/// Каждой реплике присваивается целочисленный вес из секции <c>LoadBalancer:Weights</c>.
/// При поступлении запроса реплика выбирается случайно — вероятность выбора пропорциональна весу
/// (вес реплики / сумма всех весов).
/// </summary>
/// <param name="services">Делегат для получения списка доступных реплик сервиса.</param>
/// <param name="configuration">
/// Конфигурация приложения с секцией <c>LoadBalancer:Weights</c> —
/// массив <c>int</c>, где индекс соответствует номеру реплики, а значение — относительному весу.
/// </param>
public class WeightedRandom(
    Func<Task<List<Service>>> services,
    IConfiguration configuration) : ILoadBalancer
{
    public string Type => nameof(WeightedRandom);

    private readonly int[] _weights = configuration.GetSection("LoadBalancer:Weights").Get<int[]>() ?? [];

    public async Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext context)
    {
        var pool = await services();

        if (pool.Count == 0)
            throw new InvalidOperationException("No available downstream services");

        var total = 0;
        for (var i = 0; i < pool.Count; i++)
            total += Weight(i);

        var value = Random.Shared.Next(total);

        for (var i = 0; i < pool.Count; i++)
        {
            value -= Weight(i);
            if (value < 0)
                return new OkResponse<ServiceHostAndPort>(pool[i].HostAndPort);
        }

        return new OkResponse<ServiceHostAndPort>(pool[^1].HostAndPort);
    }

    public void Release(ServiceHostAndPort hostAndPort) { }

    /// <summary>
    /// Возвращает вес реплики по индексу.
    /// Если вес не задан или не положителен — возвращает 1.
    /// </summary>
    private int Weight(int i) => i < _weights.Length && _weights[i] > 0 ? _weights[i] : 1;
}
