using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace svema.Migrations
{
    public partial class userstorage : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_storages_StorageId",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "StorageId",
                table: "users",
                newName: "storage_id");

            migrationBuilder.RenameIndex(
                name: "IX_users_StorageId",
                table: "users",
                newName: "IX_users_storage_id");

            migrationBuilder.AlterColumn<int>(
                name: "storage_id",
                table: "users",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_users_storages_storage_id",
                table: "users",
                column: "storage_id",
                principalTable: "storages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_storages_storage_id",
                table: "users");

            migrationBuilder.RenameColumn(
                name: "storage_id",
                table: "users",
                newName: "StorageId");

            migrationBuilder.RenameIndex(
                name: "IX_users_storage_id",
                table: "users",
                newName: "IX_users_StorageId");

            migrationBuilder.AlterColumn<int>(
                name: "StorageId",
                table: "users",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AddForeignKey(
                name: "FK_users_storages_StorageId",
                table: "users",
                column: "StorageId",
                principalTable: "storages",
                principalColumn: "id");
        }
    }
}
