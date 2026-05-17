using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;
using WarehouseApp.FileService.Storage;

namespace WarehouseApp.FileService.Controllers;

/// <summary>
/// Контроллер для чтения файлов из S3-бакета
/// </summary>
/// <param name="s3Service">Служба для работы с S3</param>
/// <param name="logger">Логгер</param>
[ApiController]
[Route("api/s3")]
public class S3StorageController(
    IS3Service s3Service,
    ILogger<S3StorageController> logger) : ControllerBase
{
    /// <summary>
    /// Возвращает список ключей файлов в бакете
    /// </summary>
    [HttpGet]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<List<string>>> ListFiles()
    {
        logger.LogInformation("Method {Method} was called", nameof(ListFiles));
        try
        {
            var list = await s3Service.GetFileList();
            logger.LogInformation("Got a list of {Count} files from bucket", list.Count);
            return Ok(list);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during {Method}", nameof(ListFiles));
            return BadRequest(ex.Message);
        }
    }

    /// <summary>
    /// Возвращает JSON-документ по ключу
    /// </summary>
    /// <param name="key">Ключ файла в бакете</param>
    [HttpGet("{key}")]
    [ProducesResponseType(200)]
    [ProducesResponseType(500)]
    public async Task<ActionResult<JsonNode>> GetFile(string key)
    {
        logger.LogInformation("Method {Method} was called with key {Key}", nameof(GetFile), key);
        try
        {
            var node = await s3Service.DownloadFile(key);
            logger.LogInformation("Received json of {Size} bytes",
                Encoding.UTF8.GetByteCount(node.ToJsonString()));
            return Ok(node);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Exception occurred during {Method}", nameof(GetFile));
            return BadRequest(ex.Message);
        }
    }
}
