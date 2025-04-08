using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Svema.Migrations
{
    public partial class camera : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "camera_manufacturer",
                table: "shots",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "camera_model",
                table: "shots",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "camera_manufacturer",
                table: "shots");

            migrationBuilder.DropColumn(
                name: "camera_model",
                table: "shots");
        }
    }
}
