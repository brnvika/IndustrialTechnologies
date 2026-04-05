using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnowOps.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateSequence(
                name: "defect_public_id_seq",
                startValue: 1000L);

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    login = table.Column<string>(type: "text", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_app_user", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "district",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    name = table.Column<string>(type: "text", nullable: false),
                    center_lat = table.Column<double>(type: "double precision", nullable: false),
                    center_lon = table.Column<double>(type: "double precision", nullable: false),
                    timezone = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_district", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "defect",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    public_id = table.Column<long>(type: "bigint", nullable: false, defaultValueSql: "nextval('defect_public_id_seq')"),
                    district_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lat = table.Column<double>(type: "double precision", nullable: false),
                    lon = table.Column<double>(type: "double precision", nullable: false),
                    area_code = table.Column<string>(type: "text", nullable: true),
                    area_name = table.Column<string>(type: "text", nullable: true),
                    defect_type = table.Column<short>(type: "smallint", nullable: false),
                    coverage_percent = table.Column<short>(type: "smallint", nullable: false),
                    location_type = table.Column<short>(type: "smallint", nullable: false),
                    status = table.Column<short>(type: "smallint", nullable: false),
                    risk_r = table.Column<int>(type: "integer", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    found_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fixed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_defect", x => x.Id);
                    table.ForeignKey(
                        name: "FK_defect_app_user_owner_user_id",
                        column: x => x.owner_user_id,
                        principalTable: "app_user",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_defect_district_district_id",
                        column: x => x.district_id,
                        principalTable: "district",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "weather_observation",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    district_id = table.Column<Guid>(type: "uuid", nullable: false),
                    observed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    temp_c = table.Column<decimal>(type: "numeric", nullable: false),
                    precipitation_3h_mm = table.Column<decimal>(type: "numeric", nullable: false),
                    snowfall_3h_mm = table.Column<decimal>(type: "numeric", nullable: false),
                    snow_or_ice_last_3h = table.Column<bool>(type: "boolean", nullable: false),
                    raw = table.Column<string>(type: "jsonb", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_weather_observation", x => x.Id);
                    table.ForeignKey(
                        name: "FK_weather_observation_district_district_id",
                        column: x => x.district_id,
                        principalTable: "district",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "defect_event",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    defect_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<short>(type: "smallint", nullable: false),
                    from_status = table.Column<short>(type: "smallint", nullable: true),
                    to_status = table.Column<short>(type: "smallint", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_defect_event", x => x.Id);
                    table.ForeignKey(
                        name: "FK_defect_event_app_user_actor_user_id",
                        column: x => x.actor_user_id,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_defect_event_defect_defect_id",
                        column: x => x.defect_id,
                        principalTable: "defect",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "defect_fix_proof",
                columns: table => new
                {
                    defect_id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment = table.Column<string>(type: "text", nullable: false),
                    confirmed_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    after_photos_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_defect_fix_proof", x => x.defect_id);
                    table.ForeignKey(
                        name: "FK_defect_fix_proof_app_user_confirmed_by_user_id",
                        column: x => x.confirmed_by_user_id,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_defect_fix_proof_defect_defect_id",
                        column: x => x.defect_id,
                        principalTable: "defect",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "defect_photo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    defect_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<short>(type: "smallint", nullable: false),
                    storage_provider = table.Column<string>(type: "text", nullable: false),
                    object_key = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: true),
                    content_type = table.Column<string>(type: "text", nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    sha256 = table.Column<byte[]>(type: "bytea", nullable: true),
                    uploaded_by_user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_defect_photo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_defect_photo_app_user_uploaded_by_user_id",
                        column: x => x.uploaded_by_user_id,
                        principalTable: "app_user",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_defect_photo_defect_defect_id",
                        column: x => x.defect_id,
                        principalTable: "defect",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_app_user_login",
                table: "app_user",
                column: "login",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_defect_district_id",
                table: "defect",
                column: "district_id");

            migrationBuilder.CreateIndex(
                name: "IX_defect_owner_user_id",
                table: "defect",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_defect_public_id",
                table: "defect",
                column: "public_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_defect_event_actor_user_id",
                table: "defect_event",
                column: "actor_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_defect_event_defect_id",
                table: "defect_event",
                column: "defect_id");

            migrationBuilder.CreateIndex(
                name: "IX_defect_fix_proof_confirmed_by_user_id",
                table: "defect_fix_proof",
                column: "confirmed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_defect_photo_defect_id",
                table: "defect_photo",
                column: "defect_id");

            migrationBuilder.CreateIndex(
                name: "IX_defect_photo_uploaded_by_user_id",
                table: "defect_photo",
                column: "uploaded_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_weather_observation_district_id",
                table: "weather_observation",
                column: "district_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "defect_event");

            migrationBuilder.DropTable(
                name: "defect_fix_proof");

            migrationBuilder.DropTable(
                name: "defect_photo");

            migrationBuilder.DropTable(
                name: "weather_observation");

            migrationBuilder.DropTable(
                name: "defect");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "district");

            migrationBuilder.DropSequence(
                name: "defect_public_id_seq");
        }
    }
}
