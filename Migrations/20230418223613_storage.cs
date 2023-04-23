using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace svema.Migrations
{
    public partial class storage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shots_storages_StorageId",
                table: "shots");

            migrationBuilder.RenameColumn(
                name: "StorageId",
                table: "shots",
                newName: "storage_id");

            migrationBuilder.RenameIndex(
                name: "IX_shots_StorageId",
                table: "shots",
                newName: "IX_shots_storage_id");

            migrationBuilder.AlterColumn<int>(
                name: "storage_id",
                table: "shots",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_shots_storages_storage_id",
                table: "shots",
                column: "storage_id",
                principalTable: "storages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_shots_storages_storage_id",
                table: "shots");

            migrationBuilder.RenameColumn(
                name: "storage_id",
                table: "shots",
                newName: "StorageId");

            migrationBuilder.RenameIndex(
                name: "IX_shots_storage_id",
                table: "shots",
                newName: "IX_shots_StorageId");

            migrationBuilder.AlterColumn<int>(
                name: "StorageId",
                table: "shots",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_shots_storages_StorageId",
                table: "shots",
                column: "StorageId",
                principalTable: "storages",
                principalColumn: "id");
        }
    }
}
