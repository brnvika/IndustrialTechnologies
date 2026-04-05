using Dapper;
using Npgsql;
using SnowOps.Api.Contracts;
using SnowOps.Api.Domain;

namespace SnowOps.Api.Data;

public sealed class SnowRepository
{
    private readonly string _connectionString;

    public SnowRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing");
    }

    private NpgsqlConnection CreateConnection() => new(_connectionString);

    public async Task<IReadOnlyList<SnowZone>> GetZonesAsync()
    {
        const string sql = """
            select id, code, name, latitude, longitude, location_type as LocationType, created_at as CreatedAt
            from snow_zones
            order by code;
            """;

        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<SnowZone>(sql);
        return rows.ToList();
    }

    public async Task<SnowZone> CreateZoneAsync(CreateZoneRequest request)
    {
        const string sql = """
            insert into snow_zones (code, name, latitude, longitude, location_type)
            values (@Code, @Name, @Latitude, @Longitude, @LocationType)
            returning id, code, name, latitude, longitude, location_type as LocationType, created_at as CreatedAt;
            """;

        await using var connection = CreateConnection();
        return await connection.QuerySingleAsync<SnowZone>(sql, request);
    }

    public async Task<IReadOnlyList<SnowTask>> GetTasksAsync(bool snowOrIceLast3h, string? status, string? owner, string? type, int? minRisk, int? maxRisk)
    {
        const string sql = """
            select
                t.id,
                t.zone_id as ZoneId,
                z.code as ZoneCode,
                z.name as ZoneName,
                z.latitude as Latitude,
                z.longitude as Longitude,
                t.type,
                t.coverage_percent as CoveragePercent,
                t.status,
                t.owner,
                t.found_at as FoundAt,
                t.fixed_at as FixedAt,
                coalesce(t.photos, '{}') as Photos,
                t.fix_comment as FixComment,
                z.location_type as LocationType
            from snow_tasks t
            inner join snow_zones z on z.id = t.zone_id
            where (@Status is null or t.status = @Status)
              and (@Owner is null or t.owner = @Owner)
              and (@Type is null or t.type = @Type)
            order by t.found_at desc;
            """;

        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<SnowTaskRow>(sql, new
        {
            Status = status,
            Owner = owner,
            Type = type
        });

        var tasks = rows.Select(r => ToTask(r, snowOrIceLast3h)).ToList();

        if (minRisk.HasValue)
        {
            tasks = tasks.Where(t => t.Risk >= minRisk.Value).ToList();
        }

        if (maxRisk.HasValue)
        {
            tasks = tasks.Where(t => t.Risk <= maxRisk.Value).ToList();
        }

        return tasks;
    }

    public async Task<SnowTask?> GetTaskByIdAsync(long id, bool snowOrIceLast3h)
    {
        const string sql = """
            select
                t.id,
                t.zone_id as ZoneId,
                z.code as ZoneCode,
                z.name as ZoneName,
                z.latitude as Latitude,
                z.longitude as Longitude,
                t.type,
                t.coverage_percent as CoveragePercent,
                t.status,
                t.owner,
                t.found_at as FoundAt,
                t.fixed_at as FixedAt,
                coalesce(t.photos, '{}') as Photos,
                t.fix_comment as FixComment,
                z.location_type as LocationType
            from snow_tasks t
            inner join snow_zones z on z.id = t.zone_id
            where t.id = @Id;
            """;

        await using var connection = CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SnowTaskRow>(sql, new { Id = id });
        return row is null ? null : ToTask(row, snowOrIceLast3h);
    }

    public async Task<SnowTask> CreateTaskAsync(CreateTaskRequest request, string createdBy, bool snowOrIceLast3h)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        const string insertTaskSql = """
            insert into snow_tasks (zone_id, type, coverage_percent, status, owner, found_at, photos)
            values (@ZoneId, @Type, @CoveragePercent, 'found', null, now(), @Photos)
            returning id;
            """;

        var taskId = await connection.ExecuteScalarAsync<long>(insertTaskSql, request, tx);

        const string reportSql = """
            insert into work_reports (task_id, employee, action, comment, photos)
            values (@TaskId, @Employee, 'created', @Comment, @Photos);
            """;

        await connection.ExecuteAsync(reportSql, new
        {
            TaskId = taskId,
            Employee = createdBy,
            Comment = "Задача создана",
            Photos = request.Photos
        }, tx);

        await tx.CommitAsync();
        return (await GetTaskByIdAsync(taskId, snowOrIceLast3h))!;
    }

    public async Task<SnowTask?> TakeTaskInWorkAsync(long id, string employee, bool snowOrIceLast3h)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        const string updateSql = """
            update snow_tasks
            set status = 'work', owner = @Employee, updated_at = now()
            where id = @Id and status = 'found';
            """;

        var affected = await connection.ExecuteAsync(updateSql, new { Id = id, Employee = employee }, tx);
        if (affected == 0)
        {
            await tx.RollbackAsync();
            return null;
        }

        const string reportSql = """
            insert into work_reports (task_id, employee, action, comment, photos)
            values (@TaskId, @Employee, 'take', 'Задача взята в работу', '{}');
            """;

        await connection.ExecuteAsync(reportSql, new { TaskId = id, Employee = employee }, tx);
        await tx.CommitAsync();

        return await GetTaskByIdAsync(id, snowOrIceLast3h);
    }

    public async Task<SnowTask?> FixTaskAsync(long id, FixTaskRequest request, bool snowOrIceLast3h)
    {
        await using var connection = CreateConnection();
        await connection.OpenAsync();
        await using var tx = await connection.BeginTransactionAsync();

        const string updateSql = """
            update snow_tasks
            set status = 'fixed',
                owner = @Employee,
                fixed_at = now(),
                fix_comment = @Comment,
                photos = case
                    when cardinality(@PhotoUrls) > 0 then @PhotoUrls
                    else photos
                end,
                updated_at = now()
            where id = @Id and status = 'work';
            """;

        var affected = await connection.ExecuteAsync(updateSql, new
        {
            Id = id,
            request.Employee,
            request.Comment,
            request.PhotoUrls
        }, tx);

        if (affected == 0)
        {
            await tx.RollbackAsync();
            return null;
        }

        const string reportSql = """
            insert into work_reports (task_id, employee, action, comment, photos)
            values (@TaskId, @Employee, 'fix', @Comment, @Photos);
            """;

        await connection.ExecuteAsync(reportSql, new
        {
            TaskId = id,
            request.Employee,
            request.Comment,
            Photos = request.PhotoUrls
        }, tx);

        await tx.CommitAsync();
        return await GetTaskByIdAsync(id, snowOrIceLast3h);
    }

    public async Task<IReadOnlyList<WorkReport>> GetWorkReportsAsync(int hours)
    {
        const string sql = """
            select
                id,
                task_id as TaskId,
                employee,
                action,
                comment,
                coalesce(photos, '{}') as Photos,
                created_at as CreatedAt
            from work_reports
            where created_at >= now() - make_interval(hours => @Hours)
            order by created_at desc;
            """;

        await using var connection = CreateConnection();
        var rows = await connection.QueryAsync<WorkReport>(sql, new { Hours = hours });
        return rows.ToList();
    }

    public async Task<object> GetSummaryAsync(int hours, bool snowOrIceLast3h)
    {
        var tasks = await GetTasksAsync(snowOrIceLast3h, null, null, null, null, null);
        var from = DateTimeOffset.UtcNow.AddHours(-hours);
        var inRange = tasks.Where(t => t.FoundAt >= from).ToList();

        var fixedDurations = inRange
            .Where(t => t.Status == "fixed" && t.FixedAt.HasValue)
            .Select(t => (t.FixedAt!.Value - t.FoundAt).TotalMinutes)
            .Where(v => v >= 0)
            .OrderBy(v => v)
            .ToList();

        var avgFix = fixedDurations.Count == 0 ? (double?)null : fixedDurations.Average();
        double? medianFix;
        if (fixedDurations.Count == 0)
        {
            medianFix = null;
        }
        else if (fixedDurations.Count % 2 == 1)
        {
            medianFix = fixedDurations[fixedDurations.Count / 2];
        }
        else
        {
            medianFix = (fixedDurations[fixedDurations.Count / 2 - 1] + fixedDurations[fixedDurations.Count / 2]) / 2;
        }

        var byType = inRange
            .GroupBy(t => t.Type)
            .ToDictionary(g => g.Key, g => g.Count());

        return new
        {
            from,
            total = inRange.Count,
            fixedCount = inRange.Count(t => t.Status == "fixed"),
            workCount = inRange.Count(t => t.Status == "work"),
            foundCount = inRange.Count(t => t.Status == "found"),
            highRiskCount = inRange.Count(t => t.Risk > 4),
            avgFixMinutes = avgFix,
            medianFixMinutes = medianFix,
            byType
        };
    }

    private static SnowTask ToTask(SnowTaskRow row, bool snowOrIceLast3h)
    {
        return new SnowTask
        {
            Id = row.Id,
            ZoneId = row.ZoneId,
            ZoneCode = row.ZoneCode,
            ZoneName = row.ZoneName,
            Latitude = row.Latitude,
            Longitude = row.Longitude,
            Type = row.Type,
            LocationType = row.LocationType,
            CoveragePercent = row.CoveragePercent,
            Status = row.Status,
            Owner = row.Owner,
            FoundAt = row.FoundAt,
            FixedAt = row.FixedAt,
            Photos = row.Photos ?? [],
            FixComment = row.FixComment,
            Risk = RiskCalculator.Compute(row.Type, row.CoveragePercent, row.LocationType, snowOrIceLast3h)
        };
    }

    private sealed class SnowTaskRow
    {
        public long Id { get; init; }
        public long ZoneId { get; init; }
        public string ZoneCode { get; init; } = string.Empty;
        public string ZoneName { get; init; } = string.Empty;
        public double Latitude { get; init; }
        public double Longitude { get; init; }
        public string Type { get; init; } = string.Empty;
        public int CoveragePercent { get; init; }
        public string Status { get; init; } = string.Empty;
        public string? Owner { get; init; }
        public DateTimeOffset FoundAt { get; init; }
        public DateTimeOffset? FixedAt { get; init; }
        public string[]? Photos { get; init; }
        public string? FixComment { get; init; }
        public string LocationType { get; init; } = "Обычный переход";
    }
}
