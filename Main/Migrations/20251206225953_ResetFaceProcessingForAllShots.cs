using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Svema.Migrations
{
    public partial class ResetFaceProcessingForAllShots : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Reset all shots to be reprocessed for face detection
            migrationBuilder.Sql("UPDATE shots SET is_face_processed = false;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Note: We cannot reliably determine which shots were originally processed
            // This is a one-way migration. Down() does nothing.
        }
    }
}
