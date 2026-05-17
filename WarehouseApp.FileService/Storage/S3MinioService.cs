using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Minio;
using Minio.DataModel.Args;

namespace WarehouseApp.FileService.Storage;

/// <inheritdoc cref="IS3Service"/>
/// <param name="client">Клиент Minio</param>
/// <param name="configuration">Конфигурация</param>
/// <param name="logger">Логгер</param>
public class S3MinioService(
    IMinioClient client,
    IConfiguration configuration,
    ILogger<S3MinioService> logger) : IS3Service
{
    private readonly string _bucketName = configuration["AWS:Resources:MinioBucketName"]
        ?? throw new KeyNotFoundException("Minio bucket name was not found in configuration");

    /// <inheritdoc/>
    public async Task<List<string>> GetFileList()
    {
        var list = new List<string>();
        var request = new ListObjectsArgs()
            .WithBucket(_bucketName)
            .WithPrefix("")
            .WithRecursive(true);

        logger.LogInformation("Began listing files in {Bucket}", _bucketName);
        var responseList = client.ListObjectsEnumAsync(request);

        if (responseList is null)
        {
            logger.LogWarning("Received null response from {Bucket}", _bucketName);
            return list;
        }

        await foreach (var response in responseList)
            list.Add(response.Key);
        return list;
    }

    /// <inheritdoc/>
    public async Task<bool> UploadFile(string fileData)
    {
        var rootNode = JsonNode.Parse(fileData) ?? throw new ArgumentException("Passed string is not a valid JSON");
        var id = rootNode["id"]?.GetValue<int>()
            ?? rootNode["Id"]?.GetValue<int>()
            ?? throw new ArgumentException("Passed JSON has no 'id' property");

        var bytes = Encoding.UTF8.GetBytes(fileData);
        using var stream = new MemoryStream(bytes);
        stream.Seek(0, SeekOrigin.Begin);

        logger.LogInformation("Began uploading warehouse item {Id} into {Bucket}", id, _bucketName);
        var request = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithStreamData(stream)
            .WithObjectSize(bytes.Length)
            .WithObject($"warehouse_{id}.json");

        var response = await client.PutObjectAsync(request);

        if (response.ResponseStatusCode != HttpStatusCode.OK)
        {
            logger.LogError("Failed to upload warehouse item {Id}: {Code}", id, response.ResponseStatusCode);
            return false;
        }
        logger.LogInformation("Finished uploading warehouse item {Id} to {Bucket}", id, _bucketName);
        return true;
    }

    /// <inheritdoc/>
    public async Task<JsonNode> DownloadFile(string key)
    {
        logger.LogInformation("Began downloading {Key} from {Bucket}", key, _bucketName);

        try
        {
            var memoryStream = new MemoryStream();
            var request = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(key)
                .WithCallbackStream(async (stream, cancellationToken) =>
                {
                    await stream.CopyToAsync(memoryStream, cancellationToken);
                    memoryStream.Seek(0, SeekOrigin.Begin);
                });

            var response = await client.GetObjectAsync(request);
            if (response is null)
            {
                logger.LogError("Failed to download {Key}", key);
                throw new InvalidOperationException($"Error occurred downloading {key} — object is null");
            }

            using var reader = new StreamReader(memoryStream, Encoding.UTF8);
            return JsonNode.Parse(reader.ReadToEnd())
                ?? throw new InvalidOperationException("Downloaded document is not a valid JSON");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during {Key} downloading", key);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task EnsureBucketExists()
    {
        logger.LogInformation("Checking whether {Bucket} exists", _bucketName);
        try
        {
            var request = new BucketExistsArgs().WithBucket(_bucketName);
            var exists = await client.BucketExistsAsync(request);
            if (!exists)
            {
                logger.LogInformation("Creating {Bucket}", _bucketName);
                var createRequest = new MakeBucketArgs().WithBucket(_bucketName);
                await client.MakeBucketAsync(createRequest);
                return;
            }
            logger.LogInformation("{Bucket} already exists", _bucketName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception occurred during {Bucket} check", _bucketName);
            throw;
        }
    }
}
