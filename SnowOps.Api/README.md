# SnowOps.Api MVP

Минимальный backend для одного сценария:
- загрузить фото дороги,
- определить тип дефекта покрытия.

## Stack
- ASP.NET Core Web API (.NET 9)
- SixLabors.ImageSharp
- ONNX Runtime (для инференса обученной модели)

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

## Обучение своей модели

В корне проекта есть папка `training` с полным пайплайном.

1. Установи зависимости Python:

```powershell
pip install -r training\requirements.txt
```

2. Подготовь исходный датасет по классам:

```text
D:\SnowOpsData\raw_roadcover_dataset\
  ice\
  loose_snow\
  snowdrift\
  snowbank_crosswalk\
```

3. Раздели на train/val:

```powershell
python training\split_roadcover_dataset.py --source D:\SnowOpsData\raw_roadcover_dataset --target D:\SnowOpsData\roadcover_dataset
```

4. Обучи и экспортируй ONNX прямо в API:

```powershell
python training\train_roadcover.py --dataset D:\SnowOpsData\roadcover_dataset --model-output SnowOps.Api\models\roadcover.onnx
```

По умолчанию используется более сильная базовая модель `yolov8s-cls.pt`, поэтому обучение идет дольше, но обычно лучше разделяет похожие классы вроде `ice` и `snowbank_crosswalk`.

Если хочешь именно дообучить уже полученную модель на новых примерах, укажи свои веса через `--weights`:

```powershell
python training\train_roadcover.py --weights training\runs\roadcover\weights\best.pt --dataset D:\SnowOpsData\roadcover_dataset --model-output SnowOps.Api\models\roadcover.onnx
```

Для твоего случая лучше всего добавлять больше сложных примеров, где `ice` и `loose_snow` визуально похожи, и потом прогонять обучение ещё раз на обновлённом датасете.

5. Оцени качество на валидации:

```powershell
python training\evaluate_roadcover.py --dataset D:\SnowOpsData\roadcover_dataset\val --model SnowOps.Api\models\roadcover.onnx
```

6. Перезапусти API. Если модель загружена, в ответе `POST /api/vision/analyze` будет `"engine": "onnx"`. Если модель отсутствует или невалидна, сервис автоматически переключится на `"engine": "heuristic"`.
