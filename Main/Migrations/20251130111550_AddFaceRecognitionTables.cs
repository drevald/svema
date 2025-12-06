using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Svema.Migrations
{
    public partial class AddFaceRecognitionTables : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_albums_users_UserId",
                table: "albums");

            migrationBuilder.AddColumn<int>(
                name: "profile_photo_id",
                table: "persons",
                type: "integer",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "albums",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "albums",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            // Make preview_id nullable BEFORE adding the foreign key constraint
            migrationBuilder.AlterColumn<int>(
                name: "preview_id",
                table: "albums",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: false);

            migrationBuilder.CreateTable(
                name: "face_detections",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    shot_id = table.Column<int>(type: "integer", nullable: false),
                    x = table.Column<int>(type: "integer", nullable: false),
                    y = table.Column<int>(type: "integer", nullable: false),
                    width = table.Column<int>(type: "integer", nullable: false),
                    height = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<int>(type: "integer", nullable: true),
                    is_confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    detected_at = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_face_detections", x => x.id);
                    table.ForeignKey(
                        name: "FK_face_detections_persons_person_id",
                        column: x => x.person_id,
                        principalTable: "persons",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_face_detections_shots_shot_id",
                        column: x => x.shot_id,
                        principalTable: "shots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "face_encodings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    face_detection_id = table.Column<int>(type: "integer", nullable: false),
                    encoding = table.Column<byte[]>(type: "bytea", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_face_encodings", x => x.id);
                    table.ForeignKey(
                        name: "FK_face_encodings_face_detections_face_detection_id",
                        column: x => x.face_detection_id,
                        principalTable: "face_detections",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_albums_preview_id",
                table: "albums",
                column: "preview_id");

            migrationBuilder.CreateIndex(
                name: "IX_face_detections_person_id",
                table: "face_detections",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_face_detections_shot_id",
                table: "face_detections",
                column: "shot_id");

            migrationBuilder.CreateIndex(
                name: "IX_face_encodings_face_detection_id",
                table: "face_encodings",
                column: "face_detection_id");

            // Clean up any orphaned preview_id references before adding the constraint
            migrationBuilder.Sql("UPDATE albums SET preview_id = NULL WHERE preview_id IS NOT NULL AND preview_id NOT IN (SELECT id FROM shots);");

            // Add the foreign key constraint using NOT VALID to allow existing data
            migrationBuilder.Sql("ALTER TABLE albums ADD CONSTRAINT \"FK_albums_shots_preview_id\" FOREIGN KEY (preview_id) REFERENCES shots (id) ON DELETE SET NULL NOT VALID;");

            migrationBuilder.AddForeignKey(
                name: "FK_albums_users_UserId",
                table: "albums",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_albums_shots_preview_id",
                table: "albums");

            migrationBuilder.DropForeignKey(
                name: "FK_albums_users_UserId",
                table: "albums");

            migrationBuilder.DropTable(
                name: "face_encodings");

            migrationBuilder.DropTable(
                name: "face_detections");

            migrationBuilder.DropIndex(
                name: "IX_albums_preview_id",
                table: "albums");

            migrationBuilder.DropColumn(
                name: "profile_photo_id",
                table: "persons");

            migrationBuilder.AlterColumn<string>(
                name: "name",
                table: "albums",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<int>(
                name: "UserId",
                table: "albums",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.AlterColumn<int>(
                name: "preview_id",
                table: "albums",
                type: "integer",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_albums_users_UserId",
                table: "albums",
                column: "UserId",
                principalTable: "users",
                principalColumn: "id");
        }
    }
}
