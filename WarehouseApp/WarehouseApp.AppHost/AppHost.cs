using Amazon;
using Aspire.Hosting.LocalStack.Container;

var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithRedisCommander();

var gateway = builder.AddProject<Projects.Api_Gateway>("api-gateway");

var awsConfig = builder.AddAWSSDKConfig()
    .WithProfile("default")
    .WithRegion(RegionEndpoint.EUCentral1);

var localstack = builder
    .AddLocalStack("warehouse-localstack", awsConfig: awsConfig, configureContainer: container =>
    {
        container.Lifetime = ContainerLifetime.Session;
        container.DebugLevel = 1;
        container.LogLevel = LocalStackLogLevel.Debug;
        container.Port = 4566;
        container.AdditionalEnvironmentVariables
            .Add("DEBUG", "1");
        container.AdditionalEnvironmentVariables
            .Add("SNS_CERT_URL_HOST", "sns.eu-central-1.amazonaws.com");
    });

var awsResources = builder
    .AddAWSCloudFormationTemplate("resources", "CloudFormation/warehouse-template-sns.yaml", "warehouse")
    .WithReference(awsConfig);

var minio = builder.AddMinioContainer("warehouse-minio");

for (var i = 0; i < 5; i++)
{
    var api = builder.AddProject<Projects.WarehouseApp_Api>($"warehouseapp-api-{i}", launchProfileName: null)
        .WithHttpsEndpoint(7250 + i)
        .WithReference(cache)
        .WithReference(awsResources)
        .WaitFor(cache)
        .WaitFor(awsResources);
    gateway.WaitFor(api);
}

builder.AddProject<Projects.Client_Wasm>("client-wasm")
    .WaitFor(gateway);

builder.AddProject<Projects.WarehouseApp_FileService>("warehouseapp-fileservice")
    .WithReference(awsResources)
    .WithReference(minio)
    .WithEnvironment("AWS__Resources__SNSUrl", "http://host.docker.internal:5280/api/sns")
    .WithEnvironment("AWS__Resources__MinioBucketName", "warehouse-bucket")
    .WaitFor(awsResources)
    .WaitFor(minio);

builder.UseLocalStack(localstack);

builder.Build().Run();
