using DemoAzureFunc.Models;
using Microsoft.Data.SqlClient;
using RepoDb;

namespace DemoAzureFunc;

internal class CustomsData
{
    private const string connString = "Server=tcp:customs.database.windows.net,1433;Initial Catalog=Customs;Persist Security Info=False;User ID=atias007;Password=Cactus007!;MultipleActiveResultSets=False;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;";

    public static async Task SaveQueueDetails(IEnumerable<QueueDetails> customsDetails)
    {
        using var conn = new SqlConnection(connString);
        await conn.MergeAllAsync(customsDetails);
    }

    public static async Task<IEnumerable<QueueDetails>> GetQueueDetails()
    {
        using var conn = new SqlConnection(connString);
        var result = await conn.QueryAllAsync<QueueDetails>();
        return result;
    }

    public static async Task ClearOldQueueDetails()
    {
        using var conn = new SqlConnection(connString);
        var date = await conn.ExecuteScalarAsync<DateTimeOffset>("SELECT MAX(UpdateDate) FROM QueueDetails");
        await conn.DeleteAsync<QueueDetails>(q => q.UpdateDate < date);
    }

    public static async Task SaveFile(IEnumerable<Models.File> file)
    {
        using var conn = new SqlConnection(connString);
        await conn.InsertAllAsync(file);
    }

    public static async Task<Models.File?> GetFile(string name, int chunk)
    {
        using var conn = new SqlConnection(connString);
        var result = await conn.QueryAsync<Models.File>(f => f.Filename == name && f.Chunk == chunk);
        return result.FirstOrDefault();
    }
}