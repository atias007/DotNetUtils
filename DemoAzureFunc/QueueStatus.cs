using DemoAzureFunc.Models;
using DemoAzureFunc.Utils;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using RepoDb;
using System.Net;
using System.Text;

namespace DemoAzureFunc;

public class QueueStatus(ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<QueueStatus>();

    [Function("SaveQueueStatus")]
    public async Task<HttpResponseData> SaveQueueStatus([HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
    {
        var date = DateTimeOffset.UtcNow;
        var body = await req.ReadFromJsonAsync<List<QueueDetails>>();
        ArgumentNullException.ThrowIfNull(body);
        if (body.Count > 200)
        {
            var validation = req.CreateResponse(HttpStatusCode.BadRequest);
            validation.Headers.Add("Content-Type", "text/plain; charset=utf-8");
            validation.WriteString($"Too many items: {body.Count}. Max items count: 200");
            return validation;
        }

        body.ForEach(x => { x.UpdateDate = date; });

        await CustomsData.SaveQueueDetails(body);

        var response = req.CreateResponse(HttpStatusCode.Created);
        response.Headers.Add("Content-Type", "text/plain; charset=utf-8");
        response.WriteString($"{body.Count} items saved");
        return response;
    }

    [Function("GetQueueStatus")]
    public async Task<HttpResponseData> GetQueueStatus([HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
    {
        _ = CustomsData.ClearOldQueueDetails();
        var result = await CustomsData.GetQueueDetails();
        var response = req.CreateResponse(HttpStatusCode.OK);

        if (!result.Any()) { return response; }

        var displayItems = result
            .OrderByDescending(i => i.Total)
            .Select(result => new
            {
                result.QueueName,
                result.Total,
                result.Unack,
                result.Consumers
            });

        var headers = $"<tr><td>Name</td><td>Total</td><td>Unack</td><td>Consumers</td></tr>";
        var rows = displayItems.Select(d => $"<tr><td>{d.QueueName}</td><td>{d.Total}</td><td>{d.Unack}</td><td>{d.Consumers}</td></tr>");

        var updateDate = result.Max(r => r.UpdateDate);
        var delta = DateTimeOffset.UtcNow - updateDate;
        var deltaText = delta.TotalMinutes < 1
            ? "Just now"
            : $"{delta.TotalMinutes:N0} minutes ago";

        var lastUpdate = $"<div class=\"last-update\">Last update: {deltaText}</div>";

        var output = HtmlFormatter.Format(deltaText, headers, rows);
        response.Headers.Add("Content-Type", "text/html; charset=utf-8");
        await response.WriteBytesAsync(Encoding.UTF8.GetBytes(output));
        return response;
    }
}