using Microsoft.EntityFrameworkCore;
using SnowOps.Api.Contracts;
using SnowOps.Api.Data;
using SnowOps.Api.Domain;
using SnowOps.Api.Domain.Entities;
using SnowOps.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connStr = builder.Configuration.GetConnectionString("Postgres")!;
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseNpgsql(connStr));

builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<WeatherService>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddSingleton<RoadCoverVisionService>();

builder.Services.AddHttpClient<WeatherService>();

builder.Services.AddCors(options =>
{
    options.AddPolicy("frontend", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors("frontend");

// Run EF Core migrations on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

// ── Health ──────────────────────────────────────────────────────────────────
app.MapGet("/api/health", () => Results.Ok(new { status = "ok", service = "SnowOps.Api" }));

// ── Districts ───────────────────────────────────────────────────────────────
app.MapGet("/api/districts", async (AppDbContext db) =>
{
    var districts = await db.Districts
        .OrderBy(d => d.Name)
        .Select(d => new DistrictDto
        {
            Id = d.Id,
            Name = d.Name,
            CenterLat = d.CenterLat,
            CenterLon = d.CenterLon,
            Timezone = d.Timezone,
            CreatedAt = d.CreatedAt
        })
        .ToListAsync();
    return Results.Ok(districts);
});

app.MapPost("/api/districts", async (CreateDistrictRequest req, AppDbContext db, CancellationToken ct) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return Results.BadRequest(new { error = "Name обязателен" });

    var district = new District
    {
        Id = Guid.NewGuid(),
        Name = req.Name,
        CenterLat = req.CenterLat,
        CenterLon = req.CenterLon,
        CreatedAt = DateTimeOffset.UtcNow
    };
    db.Districts.Add(district);
    await db.SaveChangesAsync(ct);

    var dto = new DistrictDto
    {
        Id = district.Id,
        Name = district.Name,
        CenterLat = district.CenterLat,
        CenterLon = district.CenterLon,
        Timezone = district.Timezone,
        CreatedAt = district.CreatedAt
    };
    return Results.Created($"/api/districts/{district.Id}", dto);
});

app.MapGet("/api/districts/{districtId:guid}/weather/latest", async (
    Guid districtId, WeatherService weatherService, CancellationToken ct) =>
{
    var obs = await weatherService.GetLatestAsync(districtId, ct);
    if (obs is null) return Results.NotFound(new { error = "Район не найден или данные погоды недоступны" });

    return Results.Ok(new WeatherDto
    {
        Id = obs.Id,
        DistrictId = obs.DistrictId,
        ObservedAt = obs.ObservedAt,
        TempC = obs.TempC,
        Precipitation3hMm = obs.Precipitation3hMm,
        Snowfall3hMm = obs.Snowfall3hMm,
        SnowOrIceLast3h = obs.SnowOrIceLast3h,
        Source = obs.Source
    });
});

// ── Defects ──────────────────────────────────────────────────────────────────
app.MapGet("/api/defects", async (
    Guid? districtId,
    string? status,
    int? type,
    int? riskMin,
    int? riskMax,
    DateTimeOffset? from,
    DateTimeOffset? to,
    bool? onlyWithPhotos,
    string? owner,
    string? bbox,
    HttpContext httpContext,
    AppDbContext db,
    UserService userService,
    CancellationToken ct) =>
{
    if (districtId is null)
        return Results.BadRequest(new { error = "districtId обязателен" });

    var query = db.Defects
        .Include(d => d.Owner)
        .Include(d => d.District)
        .Where(d => d.DistrictId == districtId.Value);

    if (status is not null)
    {
        var statuses = status.Split(',').Select(s => s.Trim()).ToList();
        var statusValues = statuses
            .Select(s => Enum.TryParse<DefectStatus>(s, true, out var v) ? (DefectStatus?)v : null)
            .Where(v => v is not null)
            .Select(v => v!.Value)
            .ToList();
        if (statusValues.Count > 0)
            query = query.Where(d => statusValues.Contains(d.Status));
    }

    if (type is not null)
        query = query.Where(d => (int)d.DefectType == type.Value);

    if (riskMin is not null)
        query = query.Where(d => d.RiskR >= riskMin.Value);

    if (riskMax is not null)
        query = query.Where(d => d.RiskR <= riskMax.Value);

    if (from is not null)
        query = query.Where(d => d.FoundAt >= from.Value);

    if (to is not null)
        query = query.Where(d => d.FoundAt <= to.Value);

    if (onlyWithPhotos == true)
        query = query.Where(d => d.Photos.Any());

    if (owner is not null)
    {
        if (owner.Equals("unassigned", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(d => d.OwnerUserId == null);
        }
        else if (owner.Equals("me", StringComparison.OrdinalIgnoreCase))
        {
            var login = httpContext.Request.Headers["X-User-Login"].ToString();
            if (!string.IsNullOrWhiteSpace(login))
            {
                var me = await userService.GetOrCreateAsync(login, ct);
                query = query.Where(d => d.OwnerUserId == me.Id);
            }
        }
        else
        {
            if (Guid.TryParse(owner, out var ownerId))
                query = query.Where(d => d.OwnerUserId == ownerId);
        }
    }

    if (!string.IsNullOrWhiteSpace(bbox))
    {
        var parts = bbox.Split(',');
        if (parts.Length == 4 &&
            double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minLat) &&
            double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var minLon) &&
            double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var maxLat) &&
            double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var maxLon))
        {
            query = query.Where(d => d.Lat >= minLat && d.Lat <= maxLat && d.Lon >= minLon && d.Lon <= maxLon);
        }
    }

    var defects = await query
        .OrderByDescending(d => d.FoundAt)
        .Select(d => new DefectDto
        {
            Id = d.Id,
            PublicId = d.PublicId,
            DistrictId = d.DistrictId,
            DistrictName = d.District.Name,
            Lat = d.Lat,
            Lon = d.Lon,
            AreaCode = d.AreaCode,
            AreaName = d.AreaName,
            DefectType = (int)d.DefectType,
            DefectTypeName = d.DefectType == DefectType.Ice ? "Гололёд"
                : d.DefectType == DefectType.LooseSnow ? "Рыхлый снег/сугробы"
                : d.DefectType == DefectType.SnowBankAtCrosswalk ? "Снежный вал у перехода"
                : "Норма",
            CoveragePercent = d.CoveragePercent,
            LocationType = (int)d.LocationType,
            LocationTypeName = d.LocationType == LocationType.SchoolClinic ? "Школа/поликлиника"
                : d.LocationType == LocationType.BusStop ? "Остановка"
                : "Обычный переход",
            Status = (int)d.Status,
            StatusName = d.Status == DefectStatus.Found ? "Found"
                : d.Status == DefectStatus.InProgress ? "InProgress"
                : "Fixed",
            RiskR = d.RiskR,
            OwnerUserId = d.OwnerUserId,
            OwnerLogin = d.Owner != null ? d.Owner.Login : null,
            FoundAt = d.FoundAt,
            FixedAt = d.FixedAt,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        })
        .ToListAsync(ct);

    return Results.Ok(defects);
});

app.MapGet("/api/defects/{id:guid}", async (Guid id, AppDbContext db, CancellationToken ct) =>
{
    var d = await db.Defects
        .Include(x => x.Owner)
        .Include(x => x.District)
        .Include(x => x.Photos).ThenInclude(p => p.UploadedBy)
        .Include(x => x.Events).ThenInclude(e => e.Actor)
        .Include(x => x.FixProof).ThenInclude(fp => fp!.ConfirmedBy)
        .FirstOrDefaultAsync(x => x.Id == id, ct);

    if (d is null) return Results.NotFound();

    var dto = new DefectDto
    {
        Id = d.Id,
        PublicId = d.PublicId,
        DistrictId = d.DistrictId,
        DistrictName = d.District.Name,
        Lat = d.Lat,
        Lon = d.Lon,
        AreaCode = d.AreaCode,
        AreaName = d.AreaName,
        DefectType = (int)d.DefectType,
        DefectTypeName = d.DefectType == DefectType.Ice ? "Гололёд"
            : d.DefectType == DefectType.LooseSnow ? "Рыхлый снег/сугробы"
            : d.DefectType == DefectType.SnowBankAtCrosswalk ? "Снежный вал у перехода"
            : "Норма",
        CoveragePercent = d.CoveragePercent,
        LocationType = (int)d.LocationType,
        LocationTypeName = d.LocationType == LocationType.SchoolClinic ? "Школа/поликлиника"
            : d.LocationType == LocationType.BusStop ? "Остановка"
            : "Обычный переход",
        Status = (int)d.Status,
        StatusName = d.Status == DefectStatus.Found ? "Found"
            : d.Status == DefectStatus.InProgress ? "InProgress"
            : "Fixed",
        RiskR = d.RiskR,
        OwnerUserId = d.OwnerUserId,
        OwnerLogin = d.Owner?.Login,
        FoundAt = d.FoundAt,
        FixedAt = d.FixedAt,
        CreatedAt = d.CreatedAt,
        UpdatedAt = d.UpdatedAt,
        Photos = d.Photos.Select(p => new DefectPhotoDto
        {
            Id = p.Id,
            Kind = (int)p.Kind,
            Url = p.Url,
            ContentType = p.ContentType,
            SizeBytes = p.SizeBytes,
            UploadedAt = p.UploadedAt,
            UploadedByLogin = p.UploadedBy.Login
        }).ToList(),
        Events = d.Events.OrderBy(e => e.CreatedAt).Select(e => new DefectEventDto
        {
            Id = e.Id,
            EventType = (int)e.EventType,
            FromStatus = e.FromStatus.HasValue ? (int)e.FromStatus.Value : null,
            ToStatus = e.ToStatus.HasValue ? (int)e.ToStatus.Value : null,
            ActorLogin = e.Actor.Login,
            Comment = e.Comment,
            CreatedAt = e.CreatedAt
        }).ToList(),
        FixProof = d.FixProof is null ? null : new DefectFixProofDto
        {
            Comment = d.FixProof.Comment,
            ConfirmedByLogin = d.FixProof.ConfirmedBy.Login,
            ConfirmedAt = d.FixProof.ConfirmedAt,
            AfterPhotosCount = d.FixProof.AfterPhotosCount
        }
    };

    return Results.Ok(dto);
});

app.MapPost("/api/defects", async (
    CreateDefectRequest req,
    HttpContext httpContext,
    AppDbContext db,
    UserService userService,
    WeatherService weatherService,
    CancellationToken ct) =>
{
    var login = httpContext.Request.Headers["X-User-Login"].ToString();
    if (string.IsNullOrWhiteSpace(login))
        return Results.Unauthorized();

    var user = await userService.GetOrCreateAsync(login, ct);

    var district = await db.Districts.FindAsync([req.DistrictId], ct);
    if (district is null)
        return Results.BadRequest(new { error = "Район не найден" });

    if (req.CoveragePercent < 0 || req.CoveragePercent > 100)
        return Results.BadRequest(new { error = "CoveragePercent должен быть 0–100" });

    var defectType = (DefectType)req.DefectType;
    var locationType = (LocationType)req.LocationType;

    var weather = await weatherService.GetLatestAsync(req.DistrictId, ct);
    bool snowOrIce = weather?.SnowOrIceLast3h ?? false;

    var risk = RiskCalculator.Calculate(defectType, req.CoveragePercent, locationType, snowOrIce);
    var now = DateTimeOffset.UtcNow;

    var defect = new Defect
    {
        Id = Guid.NewGuid(),
        DistrictId = req.DistrictId,
        Lat = req.Lat,
        Lon = req.Lon,
        AreaCode = req.AreaCode,
        AreaName = req.AreaName,
        DefectType = defectType,
        CoveragePercent = (short)req.CoveragePercent,
        LocationType = locationType,
        Status = DefectStatus.Found,
        RiskR = risk,
        FoundAt = now,
        CreatedAt = now,
        UpdatedAt = now
    };

    db.Defects.Add(defect);

    db.DefectEvents.Add(new DefectEvent
    {
        Id = Guid.NewGuid(),
        DefectId = defect.Id,
        EventType = EventType.Created,
        ToStatus = DefectStatus.Found,
        ActorUserId = user.Id,
        CreatedAt = now
    });

    await db.SaveChangesAsync(ct);

    return Results.Created($"/api/defects/{defect.Id}", new { id = defect.Id, publicId = defect.PublicId });
});

app.MapPost("/api/defects/{id:guid}/take", async (
    Guid id,
    HttpContext httpContext,
    AppDbContext db,
    UserService userService,
    CancellationToken ct) =>
{
    var login = httpContext.Request.Headers["X-User-Login"].ToString();
    if (string.IsNullOrWhiteSpace(login))
        return Results.Unauthorized();

    var user = await userService.GetOrCreateAsync(login, ct);

    var defect = await db.Defects.FindAsync([id], ct);
    if (defect is null) return Results.NotFound();
    if (defect.Status != DefectStatus.Found)
        return Results.Conflict(new { error = "Дефект не в статусе Found" });

    var now = DateTimeOffset.UtcNow;
    var fromStatus = defect.Status;
    defect.Status = DefectStatus.InProgress;
    defect.OwnerUserId = user.Id;
    defect.UpdatedAt = now;

    db.DefectEvents.Add(new DefectEvent
    {
        Id = Guid.NewGuid(),
        DefectId = defect.Id,
        EventType = EventType.TakenInWork,
        FromStatus = fromStatus,
        ToStatus = DefectStatus.InProgress,
        ActorUserId = user.Id,
        CreatedAt = now
    });

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { id = defect.Id, status = "InProgress" });
});

app.MapPost("/api/defects/{id:guid}/photos", async (
    Guid id,
    HttpContext httpContext,
    AppDbContext db,
    UserService userService,
    PhotoService photoService,
    CancellationToken ct) =>
{
    var login = httpContext.Request.Headers["X-User-Login"].ToString();
    if (string.IsNullOrWhiteSpace(login))
        return Results.Unauthorized();

    var user = await userService.GetOrCreateAsync(login, ct);

    var defect = await db.Defects.FindAsync([id], ct);
    if (defect is null) return Results.NotFound();

    var form = await httpContext.Request.ReadFormAsync(ct);
    var kindStr = form["kind"].ToString();
    if (!Enum.TryParse<PhotoKind>(kindStr, true, out var kind))
        return Results.BadRequest(new { error = "kind должен быть Before или After" });

    var files = form.Files;
    if (files.Count == 0)
        return Results.BadRequest(new { error = "Файлы не предоставлены" });

    var photos = new List<DefectPhoto>();
    foreach (var file in files)
    {
        var photo = await photoService.SaveAsync(file, id, kind, user.Id, ct);
        photos.Add(photo);
        db.DefectPhotos.Add(photo);

        db.DefectEvents.Add(new DefectEvent
        {
            Id = Guid.NewGuid(),
            DefectId = defect.Id,
            EventType = EventType.PhotoAdded,
            ActorUserId = user.Id,
            Comment = $"Photo added: {kind}",
            CreatedAt = DateTimeOffset.UtcNow
        });
    }

    defect.UpdatedAt = DateTimeOffset.UtcNow;
    await db.SaveChangesAsync(ct);

    return Results.Ok(photos.Select(p => new DefectPhotoDto
    {
        Id = p.Id,
        Kind = (int)p.Kind,
        Url = p.Url,
        ContentType = p.ContentType,
        SizeBytes = p.SizeBytes,
        UploadedAt = p.UploadedAt
    }).ToList());
});

app.MapPost("/api/defects/{id:guid}/fix", async (
    Guid id,
    FixDefectRequest req,
    HttpContext httpContext,
    AppDbContext db,
    UserService userService,
    CancellationToken ct) =>
{
    var login = httpContext.Request.Headers["X-User-Login"].ToString();
    if (string.IsNullOrWhiteSpace(login))
        return Results.Unauthorized();

    var user = await userService.GetOrCreateAsync(login, ct);

    var defect = await db.Defects
        .Include(d => d.Photos)
        .FirstOrDefaultAsync(d => d.Id == id, ct);

    if (defect is null) return Results.NotFound();
    if (defect.Status != DefectStatus.InProgress)
        return Results.Conflict(new { error = "Дефект не в статусе InProgress" });
    if (defect.OwnerUserId != user.Id)
        return Results.Forbid();

    if (string.IsNullOrWhiteSpace(req.Comment))
        return Results.BadRequest(new { error = "Comment обязателен" });

    var afterPhotos = defect.Photos.Where(p => p.Kind == PhotoKind.After).ToList();
    if (afterPhotos.Count == 0)
        return Results.BadRequest(new { error = "Необходимо минимум одно AFTER-фото" });

    var now = DateTimeOffset.UtcNow;
    var fromStatus = defect.Status;
    defect.Status = DefectStatus.Fixed;
    defect.FixedAt = now;
    defect.UpdatedAt = now;

    db.DefectFixProofs.Add(new DefectFixProof
    {
        DefectId = defect.Id,
        Comment = req.Comment,
        ConfirmedByUserId = user.Id,
        ConfirmedAt = now,
        AfterPhotosCount = afterPhotos.Count
    });

    db.DefectEvents.Add(new DefectEvent
    {
        Id = Guid.NewGuid(),
        DefectId = defect.Id,
        EventType = EventType.Fixed,
        FromStatus = fromStatus,
        ToStatus = DefectStatus.Fixed,
        ActorUserId = user.Id,
        Comment = req.Comment,
        CreatedAt = now
    });

    await db.SaveChangesAsync(ct);
    return Results.Ok(new { id = defect.Id, status = "Fixed", fixedAt = now });
});

// ── Reports ──────────────────────────────────────────────────────────────────
app.MapGet("/api/reports/summary", async (
    Guid? districtId,
    DateTimeOffset? from,
    DateTimeOffset? to,
    AppDbContext db,
    CancellationToken ct) =>
{
    var now = DateTimeOffset.UtcNow;
    var rangeFrom = from ?? now.AddHours(-24);
    var rangeTo = to ?? now;

    var query = db.Defects.AsQueryable();
    if (districtId.HasValue)
        query = query.Where(d => d.DistrictId == districtId.Value);

    query = query.Where(d => d.FoundAt >= rangeFrom && d.FoundAt <= rangeTo);

    var defects = await query
        .Include(d => d.Photos)
        .ToListAsync(ct);

    var fixTimes = defects
        .Where(d => d.FixedAt.HasValue)
        .Select(d => (d.FixedAt!.Value - d.FoundAt).TotalMinutes)
        .OrderBy(x => x)
        .ToList();

    double? median = null;
    if (fixTimes.Count > 0)
    {
        int mid = fixTimes.Count / 2;
        median = fixTimes.Count % 2 == 0
            ? (fixTimes[mid - 1] + fixTimes[mid]) / 2.0
            : fixTimes[mid];
    }

    var byType = defects
        .GroupBy(d => d.DefectType.ToString())
        .ToDictionary(g => g.Key, g => g.Count());

    var mapItems = defects.Select(d => new MapItemDto
    {
        Id = d.Id,
        PublicId = d.PublicId,
        Lat = d.Lat,
        Lon = d.Lon,
        Status = (int)d.Status,
        RiskR = d.RiskR,
        DefectType = (int)d.DefectType
    }).ToList();

    return Results.Ok(new ReportSummaryDto
    {
        TotalDefects = defects.Count,
        Found = defects.Count(d => d.Status == DefectStatus.Found),
        InProgress = defects.Count(d => d.Status == DefectStatus.InProgress),
        Fixed = defects.Count(d => d.Status == DefectStatus.Fixed),
        AvgFixTimeMinutes = fixTimes.Count > 0 ? fixTimes.Average() : null,
        MedianFixTimeMinutes = median,
        ByType = byType,
        MapItems = mapItems
    });
});

// ── Journal (last 24h with photos) ─────────────────────────────────────────
app.MapGet("/api/journal", async (AppDbContext db, CancellationToken ct) =>
{
    var since = DateTimeOffset.UtcNow.AddHours(-24);
    var defects = await db.Defects
        .Include(d => d.Owner)
        .Include(d => d.District)
        .Include(d => d.Photos)
        .Where(d => d.FoundAt >= since && d.Photos.Any())
        .OrderByDescending(d => d.FoundAt)
        .Select(d => new DefectDto
        {
            Id = d.Id,
            PublicId = d.PublicId,
            DistrictId = d.DistrictId,
            DistrictName = d.District.Name,
            Lat = d.Lat,
            Lon = d.Lon,
            AreaCode = d.AreaCode,
            AreaName = d.AreaName,
            DefectType = (int)d.DefectType,
            CoveragePercent = d.CoveragePercent,
            LocationType = (int)d.LocationType,
            Status = (int)d.Status,
            RiskR = d.RiskR,
            OwnerUserId = d.OwnerUserId,
            OwnerLogin = d.Owner != null ? d.Owner.Login : null,
            FoundAt = d.FoundAt,
            FixedAt = d.FixedAt,
            CreatedAt = d.CreatedAt,
            UpdatedAt = d.UpdatedAt
        })
        .ToListAsync(ct);

    return Results.Ok(defects);
});

// ── Vision (keep existing) ───────────────────────────────────────────────────
app.MapPost("/api/vision/analyze", async (IFormFile photo, RoadCoverVisionService visionService, CancellationToken cancellationToken) =>
{
    if (photo is null || photo.Length == 0)
        return Results.BadRequest(new { error = "Photo is required" });

    await using var stream = photo.OpenReadStream();
    var result = await visionService.AnalyzeAsync(stream, cancellationToken);

    return Results.Ok(new VisionAnalyzeResponse
    {
        Label = result.Label switch
        {
            RoadCoverLabel.Ice => "Гололёд",
            RoadCoverLabel.LooseSnow => "Рыхлый снег",
            RoadCoverLabel.Snowdrift => "Сугробы",
            RoadCoverLabel.SnowBankAtCrosswalk => "Снежный вал у перехода",
            _ => "Неопределено"
        },
        Confidence = result.Confidence,
        IceScore = result.IceScore,
        LooseSnowScore = result.LooseSnowScore,
        SnowdriftScore = result.SnowdriftScore,
        SnowBankAtCrosswalkScore = result.SnowBankAtCrosswalkScore,
        Width = result.Width,
        Height = result.Height,
        Features = result.Features
    });
})
.Accepts<IFormFile>("multipart/form-data");

// Serve photos
var photosPath = builder.Configuration["Photos:BasePath"]
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "photos");
Directory.CreateDirectory(photosPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        Path.GetFullPath(photosPath)),
    RequestPath = "/photos"
});

app.Run();
