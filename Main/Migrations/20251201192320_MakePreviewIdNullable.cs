using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Svema.Migrations
{
    public partial class MakePreviewIdNullable : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_face_encodings_face_detection_id",
                table: "face_encodings");

            migrationBuilder.AlterColumn<int>(
                name: "preview_id",
                table: "albums",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_face_encodings_face_detection_id",
                table: "face_encodings",
                column: "face_detection_id",
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_face_encodings_face_detection_id",
                table: "face_encodings");

            migrationBuilder.AlterColumn<int>(
                name: "preview_id",
                table: "albums",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_face_encodings_face_detection_id",
                table: "face_encodings",
                column: "face_detection_id");
        }
    }
}
