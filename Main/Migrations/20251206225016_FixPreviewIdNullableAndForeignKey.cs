using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Svema.Migrations
{
    public partial class FixPreviewIdNullableAndForeignKey : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Step 1: Drop the problematic FK constraint if it exists
            migrationBuilder.Sql("ALTER TABLE albums DROP CONSTRAINT IF EXISTS \"FK_albums_shots_preview_id\";");
            
            // Step 2: Make preview_id nullable if it's not already
            migrationBuilder.Sql("ALTER TABLE albums ALTER COLUMN preview_id DROP NOT NULL;");
            
            // Step 3: Clean up any orphaned preview_id references
            migrationBuilder.Sql("UPDATE albums SET preview_id = NULL WHERE preview_id IS NOT NULL AND preview_id NOT IN (SELECT id FROM shots);");
            
            // Step 4: Add the foreign key constraint using NOT VALID (PostgreSQL feature)
            // This allows existing data to remain even if it violates the constraint
            migrationBuilder.Sql("ALTER TABLE albums ADD CONSTRAINT \"FK_albums_shots_preview_id\" FOREIGN KEY (preview_id) REFERENCES shots (id) ON DELETE SET NULL NOT VALID;");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert changes by dropping the constraint
            migrationBuilder.Sql("ALTER TABLE albums DROP CONSTRAINT IF EXISTS \"FK_albums_shots_preview_id\";");
        }
    }
}
