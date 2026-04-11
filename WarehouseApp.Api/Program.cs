using WarehouseApp.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.AddRedisDistributedCache("cache");

builder.Services.AddScoped<IWarehouseItemService, WarehouseItemService>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/api/warehouse-item", async (int id, IWarehouseItemService service) =>
    Results.Ok(await service.GetOrGenerate(id)));

app.Run();
