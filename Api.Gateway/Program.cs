using Api.Gateway;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddServiceDiscovery();
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
builder.Services.AddOcelot()
    .AddCustomLoadBalancer((sp, _, provider) =>
        new WeightedRandom(provider.GetAsync, sp.GetRequiredService<IConfiguration>()));

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(allowedOrigins)
              .WithMethods("GET")
              .AllowAnyHeader()));

var app = builder.Build();

app.UseCors();

app.MapDefaultEndpoints();

await app.UseOcelot();

app.Run();
