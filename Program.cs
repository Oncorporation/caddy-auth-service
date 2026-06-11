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

var connectionString = Environment.GetEnvironmentVariable("CADDY_AUTH_CONN");
if (string.IsNullOrWhiteSpace(connectionString))
{
    Console.Error.WriteLine("connection string not found");
    Environment.Exit(1);
}

connectionString = connectionString.Trim().Trim('"');

app.MapGet("/validate", async (HttpContext context) =>
{
    var apiKey = GetApiKey(context);

    if (string.IsNullOrWhiteSpace(apiKey))
        return Results.Unauthorized();

    try
    {
        return await ValidateApiKeyAsync(connectionString, apiKey) ? Results.Ok() : Results.Unauthorized();
    }
    catch
    {
        return Results.StatusCode(500);
    }
});

app.MapGet("/validatelog", async (HttpContext context) =>
{
    var apiKey = GetApiKey(context);

    if (string.IsNullOrWhiteSpace(apiKey))
    {
        await WriteLogAsync(apiKey, connectionString, null, "apiKey missing");
        return Results.Unauthorized();
    }

    try
    {
        var exists = await ValidateApiKeyAsync(connectionString, apiKey);
        await WriteLogAsync(apiKey, connectionString, exists, null);

        return exists ? Results.Ok() : Results.Unauthorized();
    }
    catch (Exception ex)
    {
        await WriteLogAsync(apiKey, connectionString, null, ex.Message);
        return Results.StatusCode(500);
    }
});

app.Run();

static string? GetApiKey(HttpContext context)
{
    return context.Request.Headers["X-API-Key"].FirstOrDefault()
        ?? context.Request.Query["key"].FirstOrDefault()
        ?? context.Request.Cookies["key"]
        ?? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(context.Request.Query["pass"].FirstOrDefault() ?? string.Empty));
}

static async Task<bool> ValidateApiKeyAsync(string connectionString, string apiKey)
{
    await using var conn = new SqlConnection(connectionString);

    await conn.OpenAsync();

    var exists = await conn.ExecuteScalarAsync<int>(
        "CheckApiKey",
        new { Key = apiKey },
        commandType: CommandType.StoredProcedure);

    return exists > 0;
}

static async Task WriteLogAsync(string? apiKey, string connectionString, bool? exists, string? errorMessage)
{
    var logLine = $"{DateTimeOffset.UtcNow:O}\tapiKey={apiKey ?? string.Empty}\tconnectionString={RedactPassword(connectionString)}\texists={exists?.ToString() ?? string.Empty}\terrorMessage={errorMessage ?? string.Empty}{Environment.NewLine}";
    var logPath = Path.Combine(AppContext.BaseDirectory, "log.txt");

    await File.AppendAllTextAsync(logPath, logLine);
}

static string RedactPassword(string connectionString)
{
    // Replace password/pwd values with a fixed placeholder to avoid leaking password length
    var redacted = connectionString;
    var patterns = new[] { "password=", "pwd=" };
    foreach (var pattern in patterns)
    {
        var lower = redacted.ToLowerInvariant();
        var index = lower.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (index >= 0)
        {
            var valueStart = index + pattern.Length;
            var valueEnd = redacted.IndexOf(';', valueStart);
            var before = redacted.Substring(0, valueStart);
            var after = valueEnd >= 0 ? redacted.Substring(valueEnd) : "";
            redacted = before + "[REDACTED]" + after;
        }
    }
    return redacted;
}