using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Svema.Migrations
{
    public partial class AddPersonPreview : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "preview",
                table: "persons",
                type: "bytea",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "preview",
                table: "persons");
        }
    }
}
