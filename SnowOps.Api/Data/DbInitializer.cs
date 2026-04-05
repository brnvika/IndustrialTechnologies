using Npgsql;

namespace SnowOps.Api.Data;

public sealed class DbInitializer
{
    private readonly string _connectionString;

    public DbInitializer(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("ConnectionStrings:Postgres is missing");
    }

    public async Task InitializeAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        const string sql = """
            create table if not exists snow_zones (
                id bigserial primary key,
                code text not null unique,
                name text not null,
                latitude double precision not null,
                longitude double precision not null,
                location_type text not null,
                created_at timestamptz not null default now()
            );

            create table if not exists snow_tasks (
                id bigserial primary key,
                zone_id bigint not null references snow_zones(id) on delete cascade,
                type text not null,
                coverage_percent int not null check (coverage_percent >= 0 and coverage_percent <= 100),
                status text not null check (status in ('found','work','fixed')),
                owner text null,
                found_at timestamptz not null,
                fixed_at timestamptz null,
                photos text[] not null default '{}',
                fix_comment text null,
                created_at timestamptz not null default now(),
                updated_at timestamptz not null default now()
            );

            create table if not exists work_reports (
                id bigserial primary key,
                task_id bigint not null references snow_tasks(id) on delete cascade,
                employee text not null,
                action text not null check (action in ('created','take','fix')),
                comment text not null,
                photos text[] not null default '{}',
                created_at timestamptz not null default now()
            );

            create index if not exists idx_snow_tasks_status on snow_tasks(status);
            create index if not exists idx_snow_tasks_found_at on snow_tasks(found_at);
            create index if not exists idx_work_reports_task_id on work_reports(task_id);

            insert into snow_zones (code, name, latitude, longitude, location_type)
            values
                ('A-12', 'Северная линия', 55.761244, 37.618423, 'Остановка'),
                ('A-03', 'Узел 3', 55.751244, 37.628423, 'Обычный переход'),
                ('B-07', 'Южный сектор', 55.741244, 37.618423, 'Школа/поликлиника')
            on conflict (code) do nothing;

            insert into snow_tasks (zone_id, type, coverage_percent, status, owner, found_at, fixed_at, photos, fix_comment)
            select z.id, 'Гололёд', 75, 'found', null, now() - interval '90 minutes', null, array['seed4','seed1'], null
            from snow_zones z where z.code = 'A-12'
            and not exists (select 1 from snow_tasks t where t.zone_id = z.id and t.type = 'Гололёд');

            insert into snow_tasks (zone_id, type, coverage_percent, status, owner, found_at, fixed_at, photos, fix_comment)
            select z.id, 'Рыхлый снег или сугробы', 55, 'work', 'brnvika', now() - interval '120 minutes', null, array['seed1'], null
            from snow_zones z where z.code = 'A-03'
            and not exists (select 1 from snow_tasks t where t.zone_id = z.id and t.type = 'Рыхлый снег или сугробы');
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
