using Bogus;
using WarehouseApp.Api.Models;

namespace WarehouseApp.Api.Generation;

/// <summary>
/// Генератор товаров на складе на основе Bogus
/// </summary>
public static class WarehouseItemGenerator
{
    private static readonly Faker<WarehouseItem> _faker = new Faker<WarehouseItem>()
        .RuleFor(x => x.ProductName, f => f.Commerce.ProductName())
        .RuleFor(x => x.Category, f => f.Commerce.Categories(1)[0])
        .RuleFor(x => x.Quantity, f => f.Random.Int(0, 1000))
        .RuleFor(x => x.PricePerUnit, f => Math.Round(f.Random.Decimal(1m, 10000m), 2))
        .RuleFor(x => x.WeightPerUnit, f => Math.Round(f.Random.Double(0.1, 500.0), 2))
        .RuleFor(x => x.Dimensions, f =>
            $"{f.Random.Int(1, 99)}х{f.Random.Int(1, 99)}х{f.Random.Int(1, 99)} см")
        .RuleFor(x => x.IsFragile, f => f.Random.Bool())
        .RuleFor(x => x.LastDeliveryDate, f =>
            DateOnly.FromDateTime(f.Date.Past(2, DateTime.Today)))
        .RuleFor(x => x.NextDeliveryDate, (f, item) => 
            f.Date.BetweenDateOnly(item.LastDeliveryDate, DateOnly.FromDateTime(DateTime.Today.AddYears(1)))
        );

    /// <summary>
    /// Генерирует товар на складе с указанным идентификатором
    /// </summary>
    /// <param name="id">Идентификатор товара в системе</param>
    /// <returns>Сгенерированный товар на складе</returns>
    public static WarehouseItem Generate(int id)
    {
        var item = _faker.Generate();
        item.Id = id;
        return item;
    }
}
