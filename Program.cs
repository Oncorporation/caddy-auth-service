using System.Data;
using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "CaddyAuthService";
});

builder.WebHost.UseUrls("http://localhost:11400");
var app = builder.Build();

app.MapGet("/validate", async (HttpContext context) =>
{
    var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault()
        ?? context.Request.Query["key"];

    if (string.IsNullOrWhiteSpace(apiKey))
        return Results.Unauthorized();

    try
    {
        var connectionString = Environment.GetEnvironmentVariable("CADDY_AUTH_CONN");

        if (string.IsNullOrEmpty(connectionString))
            throw new Exception("Connection string not found");

        await using var conn = new SqlConnection(connectionString);

        await conn.OpenAsync();

        var exists = await conn.ExecuteScalarAsync<int>(
            "CheckApiKey",
            new { Key = apiKey },
            commandType: CommandType.StoredProcedure);

        return exists > 0 ? Results.Ok() : Results.Unauthorized();
    }
    catch
    {
        return Results.StatusCode(500);
    }
});

app.Run();