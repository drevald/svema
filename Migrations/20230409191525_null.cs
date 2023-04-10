using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace svema.Migrations
{
    public partial class @null : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shots_Locations_LocationId",
                table: "Shots");

            migrationBuilder.AlterColumn<int>(
                name: "LocationId",
                table: "Shots",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_Shots_Locations_LocationId",
                table: "Shots",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shots_Locations_LocationId",
                table: "Shots");

            migrationBuilder.AlterColumn<int>(
                name: "LocationId",
                table: "Shots",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Shots_Locations_LocationId",
                table: "Shots",
                column: "LocationId",
                principalTable: "Locations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
