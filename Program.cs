using Microsoft.Data.SqlClient;
using Dapper;
using Microsoft.Extensions.Hosting.WindowsServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "CaddyAuthService";
});

var app = builder.Build();

app.MapGet("/validate", async (HttpContext context) =>
{
    var apiKey = context.Request.Headers["X-API-Key"].FirstOrDefault();

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
            @"SELECT COUNT(1) 
              FROM [dbo].[APIAccess] 
              WHERE [Key] = @Key AND isActive = 1",
            new { Key = apiKey });

        return exists > 0 ? Results.Ok() : Results.Unauthorized();
    }
    catch
    {
        return Results.StatusCode(500);
    }
});

app.Run();