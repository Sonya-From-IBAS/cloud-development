using System.Reflection;
using Amazon.SimpleNotificationService;
using LocalStack.Client.Extensions;
using WarehouseApp.FileService.Messaging;
using WarehouseApp.FileService.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    var assembly = Assembly.GetExecutingAssembly();
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{assembly.GetName().Name}.xml");
    if (File.Exists(xmlPath))
        options.IncludeXmlComments(xmlPath);
});

builder.Services.AddLocalStack(builder.Configuration);
builder.Services.AddAwsService<IAmazonSimpleNotificationService>();
builder.Services.AddScoped<SnsSubscriptionService>();

builder.AddMinioClient("warehouse-minio");
builder.Services.AddScoped<IS3Service, S3MinioService>();

var app = builder.Build();

using var scope = app.Services.CreateScope();
var s3Service = scope.ServiceProvider.GetRequiredService<IS3Service>();
await s3Service.EnsureBucketExists();

var subscriptionService = scope.ServiceProvider.GetRequiredService<SnsSubscriptionService>();
await subscriptionService.SubscribeEndpoint();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
