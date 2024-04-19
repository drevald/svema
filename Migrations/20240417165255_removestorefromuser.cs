using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace svema.Migrations
{
    public partial class removestorefromuser : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_storages_storage_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "IX_users_storage_id",
                table: "users");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_users_storage_id",
                table: "users",
                column: "storage_id");

            migrationBuilder.AddForeignKey(
                name: "FK_users_storages_storage_id",
                table: "users",
                column: "storage_id",
                principalTable: "storages",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
