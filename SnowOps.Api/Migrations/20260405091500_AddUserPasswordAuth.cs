using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SnowOps.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddUserPasswordAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "password_hash",
                table: "app_user",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "password_hash",
                table: "app_user");
        }
    }
}