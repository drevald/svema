using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Svema.Migrations
{
    public partial class AddIsFaceProcessedToShot : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_face_processed",
                table: "shots",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_face_processed",
                table: "shots");
        }
    }
}
