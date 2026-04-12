# SnowOps.Api MVP

Минимальный backend для одного сценария:
- загрузить фото дороги,
- определить тип дефекта покрытия.

## Stack
- ASP.NET Core Web API (.NET 9)
- SixLabors.ImageSharp

## Запуск

```powershell
dotnet run
```

После запуска открой:
- `http://localhost:5104/` - веб-интерфейс загрузки фото
- `http://localhost:5104/api/health` - health check

## API

- `GET /api/health`
- `POST /api/vision/analyze` (`multipart/form-data`, поле `photo`)

Пример:

```powershell
curl -X POST http://localhost:5104/api/vision/analyze ^
  -F "photo=@road.jpg"
```

Пример ответа:

```json
{
  "label": "Гололед",
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
