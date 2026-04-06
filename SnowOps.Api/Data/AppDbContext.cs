using Microsoft.EntityFrameworkCore;
using SnowOps.Api.Domain;
using SnowOps.Api.Domain.Entities;

namespace SnowOps.Api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<District> Districts => Set<District>();
    public DbSet<WeatherObservation> WeatherObservations => Set<WeatherObservation>();
    public DbSet<Defect> Defects => Set<Defect>();
    public DbSet<DefectEvent> DefectEvents => Set<DefectEvent>();
    public DbSet<DefectPhoto> DefectPhotos => Set<DefectPhoto>();
    public DbSet<DefectFixProof> DefectFixProofs => Set<DefectFixProof>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasSequence<long>("defect_public_id_seq").StartsAt(1000).IncrementsBy(1);

        modelBuilder.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Login).HasColumnName("login").IsRequired();
            e.HasIndex(x => x.Login).IsUnique();
            e.Property(x => x.PasswordHash).HasColumnName("password_hash").IsRequired();
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<District>(e =>
        {
            e.ToTable("district");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.Name).HasColumnName("name").IsRequired();
            e.Property(x => x.CenterLat).HasColumnName("center_lat");
            e.Property(x => x.CenterLon).HasColumnName("center_lon");
            e.Property(x => x.Timezone).HasColumnName("timezone");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<WeatherObservation>(e =>
        {
            e.ToTable("weather_observation");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.DistrictId).HasColumnName("district_id");
            e.Property(x => x.ObservedAt).HasColumnName("observed_at");
            e.Property(x => x.TempC).HasColumnName("temp_c");
            e.Property(x => x.Precipitation3hMm).HasColumnName("precipitation_3h_mm");
            e.Property(x => x.Snowfall3hMm).HasColumnName("snowfall_3h_mm");
            e.Property(x => x.SnowOrIceLast3h).HasColumnName("snow_or_ice_last_3h");
            e.Property(x => x.Raw).HasColumnName("raw").HasColumnType("jsonb");
            e.Property(x => x.Source).HasColumnName("source");
            e.HasOne(x => x.District).WithMany(d => d.WeatherObservations).HasForeignKey(x => x.DistrictId);
        });

        modelBuilder.Entity<Defect>(e =>
        {
            e.ToTable("defect");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.PublicId).HasColumnName("public_id").ValueGeneratedOnAdd()
                .HasDefaultValueSql("nextval('defect_public_id_seq')");
            e.HasIndex(x => x.PublicId).IsUnique();
            e.Property(x => x.DistrictId).HasColumnName("district_id");
            e.Property(x => x.Lat).HasColumnName("lat");
            e.Property(x => x.Lon).HasColumnName("lon");
            e.Property(x => x.AreaCode).HasColumnName("area_code");
            e.Property(x => x.AreaName).HasColumnName("area_name");
            e.Property(x => x.DefectType).HasColumnName("defect_type");
            e.Property(x => x.CoveragePercent).HasColumnName("coverage_percent");
            e.Property(x => x.LocationType).HasColumnName("location_type");
            e.Property(x => x.Status).HasColumnName("status");
            e.Property(x => x.RiskR).HasColumnName("risk_r");
            e.Property(x => x.OwnerUserId).HasColumnName("owner_user_id");
            e.Property(x => x.FoundAt).HasColumnName("found_at");
            e.Property(x => x.FixedAt).HasColumnName("fixed_at");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.Property(x => x.UpdatedAt).HasColumnName("updated_at");
            e.HasOne(x => x.District).WithMany(d => d.Defects).HasForeignKey(x => x.DistrictId);
            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerUserId).IsRequired(false);
        });

        modelBuilder.Entity<DefectEvent>(e =>
        {
            e.ToTable("defect_event");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.DefectId).HasColumnName("defect_id");
            e.Property(x => x.EventType).HasColumnName("event_type");
            e.Property(x => x.FromStatus).HasColumnName("from_status");
            e.Property(x => x.ToStatus).HasColumnName("to_status");
            e.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
            e.Property(x => x.Comment).HasColumnName("comment");
            e.Property(x => x.CreatedAt).HasColumnName("created_at");
            e.HasOne(x => x.Defect).WithMany(d => d.Events).HasForeignKey(x => x.DefectId);
            e.HasOne(x => x.Actor).WithMany().HasForeignKey(x => x.ActorUserId);
        });

        modelBuilder.Entity<DefectPhoto>(e =>
        {
            e.ToTable("defect_photo");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasDefaultValueSql("gen_random_uuid()");
            e.Property(x => x.DefectId).HasColumnName("defect_id");
            e.Property(x => x.Kind).HasColumnName("kind");
            e.Property(x => x.StorageProvider).HasColumnName("storage_provider");
            e.Property(x => x.ObjectKey).HasColumnName("object_key");
            e.Property(x => x.Url).HasColumnName("url");
            e.Property(x => x.ContentType).HasColumnName("content_type");
            e.Property(x => x.SizeBytes).HasColumnName("size_bytes");
            e.Property(x => x.Sha256).HasColumnName("sha256");
            e.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id");
            e.Property(x => x.UploadedAt).HasColumnName("uploaded_at");
            e.HasOne(x => x.Defect).WithMany(d => d.Photos).HasForeignKey(x => x.DefectId);
            e.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedByUserId);
        });

        modelBuilder.Entity<DefectFixProof>(e =>
        {
            e.ToTable("defect_fix_proof");
            e.HasKey(x => x.DefectId);
            e.Property(x => x.DefectId).HasColumnName("defect_id").ValueGeneratedNever();
            e.Property(x => x.Comment).HasColumnName("comment").IsRequired();
            e.Property(x => x.ConfirmedByUserId).HasColumnName("confirmed_by_user_id");
            e.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
            e.Property(x => x.AfterPhotosCount).HasColumnName("after_photos_count");
            e.HasOne(x => x.Defect).WithOne(d => d.FixProof).HasForeignKey<DefectFixProof>(x => x.DefectId);
            e.HasOne(x => x.ConfirmedBy).WithMany().HasForeignKey(x => x.ConfirmedByUserId);
        });
    }
}
