using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace svema.Migrations
{
    public partial class locations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "latitude",
                table: "shots",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "location_precision_meters",
                table: "shots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "longitude",
                table: "shots",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "zoom",
                table: "shots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "latitude",
                table: "albums",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "location_precision_meters",
                table: "albums",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<float>(
                name: "longitude",
                table: "albums",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<int>(
                name: "zoom",
                table: "albums",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "latitude",
                table: "shots");

            migrationBuilder.DropColumn(
                name: "location_precision_meters",
                table: "shots");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "shots");

            migrationBuilder.DropColumn(
                name: "zoom",
                table: "shots");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "albums");

            migrationBuilder.DropColumn(
                name: "location_precision_meters",
                table: "albums");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "albums");

            migrationBuilder.DropColumn(
                name: "zoom",
                table: "albums");
        }
    }
}
