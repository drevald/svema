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

            // The FK constraint should already exist from the previous migration
            // Just recreate the index
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

            migrationBuilder.CreateIndex(
                name: "IX_face_encodings_face_detection_id",
                table: "face_encodings",
                column: "face_detection_id");
        }
    }
}
