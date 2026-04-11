var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithRedisCommander();

var gateway = builder.AddProject<Projects.Api_Gateway>("api-gateway");

for (var i = 0; i < 5; i++)
{
    var api = builder.AddProject<Projects.WarehouseApp_Api>($"warehouseapp-api-{i}", launchProfileName: null)
        .WithHttpsEndpoint(7250 + i)
        .WithReference(cache)
        .WaitFor(cache);
    gateway.WaitFor(api);
}

builder.AddProject<Projects.Client_Wasm>("client-wasm")
    .WaitFor(gateway);

builder.Build().Run();
