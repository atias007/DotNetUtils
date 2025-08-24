using HttpMultipartParser;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net.Mime;

namespace DemoAzureFunc;

public class FileUpload(ILogger<FileUpload> logger)
{
    private readonly ILogger<FileUpload> _logger = logger;

    [Function("FileUpload")]
    public async Task<HttpResponseData> Upload([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("C# HTTP trigger function {Function} processed a request.", nameof(Upload));

            var parsedFormBody = await MultipartFormDataParser.ParseAsync(req.Body);
            var file = parsedFormBody.Files[0];

            using var memoryStream = new MemoryStream();
            await file.Data.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            var base64 = Convert.ToBase64String(fileBytes);

            var chunks = base64.Chunk(100_000);

            var dbFile = chunks.Select((c, i) => new Models.File
            {
                Filename = file.FileName,
                Content = new string(c),
                Chunk = i
            });

            await CustomsData.SaveFile(dbFile);

            var response = req.CreateResponse(System.Net.HttpStatusCode.Created);
            await response.WriteStringAsync(file.FileName);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving file to database");
            var err = req.CreateResponse(System.Net.HttpStatusCode.InternalServerError);
            await err.WriteStringAsync(ex.ToString());
            return err;
        }
    }

    [Function("FileRead")]
    public async Task<HttpResponseData> Read([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function {Function} processed a request.", nameof(Read));
        var filename = req.Query["filename"];
        if (string.IsNullOrWhiteSpace(filename))
        {
            return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        }

        var chunk = req.Query["chunk"];
        if (string.IsNullOrWhiteSpace(chunk))
        {
            return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        }

        if (!int.TryParse(chunk, out var chunkNum))
        {
            return req.CreateResponse(System.Net.HttpStatusCode.BadRequest);
        }

        var result = await CustomsData.GetFile(filename, chunkNum);
        if (result == null)
        {
            return req.CreateResponse(System.Net.HttpStatusCode.NotFound);
        }

        var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
        res.Headers.Add("Content-Type", MediaTypeNames.Text.Plain);

        await res.WriteStringAsync(result.Content);
        return res;
    }
}