# Caddy Auth Service

A lightweight Windows Service that validates API keys against SQL Server for use with Caddy's `forward_auth` directive.

## Purpose

This service allows you to protect services behind Caddy (such as Ollama, local APIs, etc.) using dynamic API keys stored in your `EncryptionDB` database, instead of hardcoding credentials in the Caddyfile.

Caddy forwards authentication requests to this service. If the key is valid and active, access is granted.

## How It Works

1. Client sends a request with header `X-API-Key: your-key` (or querystring `?key=your-key`)
2. Caddy calls this service via `forward_auth`
3. Service checks the key against the `[Key]` column in `dbo.APIAccess` where `isActive = 1`
4. Returns `200 OK` → Caddy allows the request
5. Returns `401` → Caddy blocks the request

## Prerequisites

- Windows Server or Windows 10/11
- .NET 8+ (or .NET 10/11)
- SQL Server access to `EncryptionDB`
- Caddy (as reverse proxy)

## Database Requirements

The service queries this table:

```sql
SELECT [Key], isActive 
FROM [dbo].[APIAccess]
```

- `[Key]` → The API key value (matched against `X-API-Key` header)
- `isActive` → Must be `1` (true) for the key to be valid

## Configuration

### Connection String (Recommended)

Set as a **system environment variable**:

```powershell
setx CADDY_AUTH_CONN "Server=YOUR_SERVER;Database=EncryptionDB;Trusted_Connection=True;TrustServerCertificate=True;" /M
```

Then restart the Windows service.

## Building & Publishing

```bash
dotnet publish -c Release -o publish
```

## Installing as Windows Service

```powershell
# Run as Administrator
sc create CaddyAuthService binPath= "C:\Path\To\publish\CaddyAuthService.exe"
sc description CaddyAuthService "Validates API keys for Caddy forward_auth"
sc start CaddyAuthService
```

To update the service after changes, stop it, replace the files, then start it again.

## Caddy Configuration Example

```caddyfile
yourdomain.com {
    # Forward auth to this service
    forward_auth localhost:5000 {
        uri /validate
    }

    reverse_proxy localhost:11434   # Your backend (e.g. Ollama)
}
```

Client requests must include the header:
```
X-API-Key: your-secret-key-value
```

Or use the querystring fallback:
```
?key=your-secret-key-value
```

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