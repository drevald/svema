using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace svema.Migrations
{
    public partial class storages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shots_ShotStorage_StorageId",
                table: "Shots");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ShotStorage_StorageId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShotStorage",
                table: "ShotStorage");

            migrationBuilder.RenameTable(
                name: "ShotStorage",
                newName: "ShotStorages");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShotStorages",
                table: "ShotStorages",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Shots_ShotStorages_StorageId",
                table: "Shots",
                column: "StorageId",
                principalTable: "ShotStorages",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ShotStorages_StorageId",
                table: "Users",
                column: "StorageId",
                principalTable: "ShotStorages",
                principalColumn: "Id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shots_ShotStorages_StorageId",
                table: "Shots");

            migrationBuilder.DropForeignKey(
                name: "FK_Users_ShotStorages_StorageId",
                table: "Users");

            migrationBuilder.DropPrimaryKey(
                name: "PK_ShotStorages",
                table: "ShotStorages");

            migrationBuilder.RenameTable(
                name: "ShotStorages",
                newName: "ShotStorage");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ShotStorage",
                table: "ShotStorage",
                column: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Shots_ShotStorage_StorageId",
                table: "Shots",
                column: "StorageId",
                principalTable: "ShotStorage",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Users_ShotStorage_StorageId",
                table: "Users",
                column: "StorageId",
                principalTable: "ShotStorage",
                principalColumn: "Id");
        }
    }
}
