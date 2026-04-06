# SnowOps.Api

C# backend for monitoring snow cover, cleaning zones, and employee work reports.

## Stack
- ASP.NET Core Web API (.NET 9)
- PostgreSQL
- Dapper + Npgsql

## Run PostgreSQL locally (Docker)

```powershell
docker run --name snowops-postgres `
  -e POSTGRES_USER=postgres `
  -e POSTGRES_PASSWORD=postgres `
  -e POSTGRES_DB=snowops `
  -p 5432:5432 -d postgres:16
```

## Configure connection string

Default connection string is in:
- appsettings.json
- appsettings.Development.json

## Start API

```powershell
dotnet run
```

On startup API auto-creates schema and adds demo seed data.

## Endpoints

- `GET /api/health`
- `POST /api/users/register`
- `POST /api/users/login`
- `GET /api/users/me` with Bearer token
- `GET /api/users/me/profile` with Bearer token
- `GET /api/zones`
- `POST /api/zones`
- `GET /api/tasks?snowOrIce=true&status=work&owner=brnvika&type=Гололёд&minRisk=3&maxRisk=10`
- `GET /api/tasks/{id}`
- `POST /api/tasks?createdBy=dispatcher`
- `POST /api/tasks/{id}/take`
- `POST /api/tasks/{id}/fix`
- `GET /api/work-reports?hours=24`
- `GET /api/reports/summary?hours=8&snowOrIce=true`
- `POST /api/vision/analyze` with `multipart/form-data` and field `photo`

## User registration

Register a user before using the app:

```json
{
  "login": "brnvika",
  "password": "secret123"
}
```

Example request:

```powershell
curl -X POST http://localhost:5104/api/users/register ^
  -H "Content-Type: application/json" ^
  -d "{\"login\":\"brnvika\",\"password\":\"secret123\"}"
```

Login:

```json
{
  "login": "brnvika",
  "password": "secret123"
}
```

Use the returned `accessToken` as `Authorization: Bearer <token>` when taking a defect into work, uploading photos, or marking it fixed.

## Example payloads

Create task:

```json
{
  "zoneId": 1,
  "type": "Гололёд",
  "coveragePercent": 80,
  "photos": ["https://example.com/photo1.jpg"]
}
```

Take task in work:

```json
{
  "employee": "brnvika"
}
```

Fix task:

```json
{
  "employee": "brnvika",
  "comment": "Убрали наледь, посыпали реагентом",
  "photoUrls": ["https://example.com/fix1.jpg"]
}
```

Photo analysis:

```powershell
curl -X POST http://localhost:5104/api/vision/analyze ^
  -F "photo=@road.jpg"
```

Response example:

```json
{
  "label": "Гололёд",
  "confidence": 0.74,
  "iceScore": 0.81,
  "looseSnowScore": 0.22,
  "snowdriftScore": 0.18,
  "snowBankAtCrosswalkScore": 0.11,
  "width": 512,
  "height": 384,
  "features": {
    "brightness": 0.41
  }
}
```
