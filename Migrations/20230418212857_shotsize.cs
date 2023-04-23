using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace svema.Migrations
{
    public partial class shotsize : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "size",
                table: "shots",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "size",
                table: "shots");
        }
    }
}
