# Caddy Auth Service

A lightweight Windows Service that validates API keys against SQL Server for use with Caddy's `forward_auth` directive.

## Purpose

This service allows you to protect services behind Caddy (such as Ollama, local APIs, etc.) using dynamic API keys stored in your `EncryptionDB` database, instead of hardcoding credentials in the Caddyfile.

Caddy forwards authentication requests to this service. If the key is valid and active, access is granted.

## How It Works

1. Client sends a request with header `X-API-Key: your-key` (or querystring `?key=your-key`)
2. Caddy calls this service via `forward_auth`
3. Service validates the key using the `CheckApiKey` stored procedure
4. Returns `200 OK` → Caddy allows the request
5. Returns `401` → Caddy blocks the request

## Prerequisites

- Windows Server or Windows 10/11
- .NET 8+ (or .NET 10/11)
- SQL Server access to `EncryptionDB`
- Caddy (as reverse proxy)

## Database Requirements

The service calls the `CheckApiKey` stored procedure with the API key as the `@Key` input parameter.

### Stored Procedure Creation

Create this stored procedure in your `EncryptionDB` database:

```sql
CREATE PROCEDURE [dbo].[CheckAPIKey]
    @Key NVARCHAR(88)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT COUNT(1) 
    FROM [dbo].[APIAccess] 
    WHERE [Key] = @Key AND isActive = 1
    AND ([SCOPE]='Permanent' or [SCOPE]='Permanent,Read' or [SCOPE]='Permanent,Write' or [SCOPE]='Permanent,Read,Write'
    or ([SCOPE]='Temporary' and GETDATE() < DATEADD(HOUR, 1, DateUpdated)));
END
```

- **`@Key`** → The API key value to validate (sent from `X-API-Key` header or `?key=` querystring)
- Returns **`1`** if the key is valid and active
- Returns **`0`** if the key is invalid or expired

## Configuration

### Connection String (Recommended)

Set as a **system environment variable**:

```powershell
setx CADDY_AUTH_CONN "Server=YOUR_SERVER;Database=EncryptionDB;Trusted_Connection=True;TrustServerCertificate=True;" /M
```
or
```powershell 
setx CADDY_AUTH_CONN "Server=YOUR_SERVER;Database=EncryptionDB;User Id=YOUR_USER;Password=YOUR_PASS;TrustServerCertificate=True;" /M
```

Then restart the Windows service.

## Building & Publishing

```bash
dotnet publish -c Release -o publish
```

or

```bash
dotnet publish -c Release -o publish --self-contained true -p:PublishSingleFile=true
```

## Installing as Windows Service

```powershell
# Run as Administrator
sc.exe create CaddyAuthService binPath= "C:\Path\To\publish\CaddyAuthService.exe" start= auto
sc.exe description CaddyAuthService "Validates API keys for Caddy forward_auth"
sc.exe start CaddyAuthService
```

To update the service after changes, stop it, replace the files, then start it again.

## Caddy Configuration Example


```caddyfile
mydomain.com {
    forward_auth https://localhost:11400 {
        uri /validate{query}
        copy_headers X-API-Key
    }

    reverse_proxy localhost:11434
}
```

Client requests must include the header:
```
X-API-Key: your-public-key-value
```

Or use the querystring fallback: (key must be url encoded)
```
?key=your-public-key-value
```

Or Use a user:passport_control:
```
?pass=your-public-key-value-encoded-base64

## Security Notes

- Never hardcode the connection string in source code.
- Use the environment variable method or restrict `appsettings.json` file permissions.
- Run the Windows service under a dedicated low-privilege account when possible.
- Only expose this auth service internally (localhost or private network).

## Endpoints

| Endpoint     | Method | Purpose                     |
|--------------|--------|-----------------------------|
| `/validate`  | GET    | Validates `X-API-Key` header or `?key=` querystring |

## License

Internal use only.
