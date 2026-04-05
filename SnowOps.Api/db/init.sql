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
