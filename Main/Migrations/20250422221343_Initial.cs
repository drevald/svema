using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Svema.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "locations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: true),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    zoom = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    first_name = table.Column<string>(type: "text", nullable: true),
                    last_name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    username = table.Column<string>(type: "text", nullable: true),
                    password_hash = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "albums",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: true),
                    UserId = table.Column<int>(type: "integer", nullable: true),
                    preview_id = table.Column<int>(type: "integer", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    zoom = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_albums", x => x.id);
                    table.ForeignKey(
                        name: "FK_albums_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "shared_users",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    host_user_id = table.Column<int>(type: "integer", nullable: false),
                    guest_user_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_users", x => x.id);
                    table.ForeignKey(
                        name: "FK_shared_users_users_guest_user_id",
                        column: x => x.guest_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_users_users_host_user_id",
                        column: x => x.host_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "storages",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<int>(type: "integer", nullable: false),
                    auth_token = table.Column<string>(type: "text", nullable: true),
                    refresh_token = table.Column<string>(type: "text", nullable: true),
                    provider = table.Column<string>(type: "text", nullable: false),
                    root = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storages", x => x.id);
                    table.ForeignKey(
                        name: "FK_storages_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "album_comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    author_username = table.Column<string>(type: "text", nullable: true),
                    album_id = table.Column<int>(type: "integer", nullable: false),
                    time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_album_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_album_comments_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_album_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shared_albums",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    guest_user_id = table.Column<int>(type: "integer", nullable: false),
                    shared_album_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shared_albums", x => x.id);
                    table.ForeignKey(
                        name: "FK_shared_albums_albums_shared_album_id",
                        column: x => x.shared_album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shared_albums_users_guest_user_id",
                        column: x => x.guest_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shots",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "text", nullable: true),
                    album_id = table.Column<int>(type: "integer", nullable: false),
                    date_start = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    date_end = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    date_uploaded = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    preview = table.Column<byte[]>(type: "bytea", nullable: true),
                    fullscreen = table.Column<byte[]>(type: "bytea", nullable: true),
                    source_uri = table.Column<string>(type: "text", nullable: true),
                    orig_path = table.Column<string>(type: "text", nullable: true),
                    MD5 = table.Column<string>(type: "text", nullable: true),
                    content_type = table.Column<string>(type: "text", nullable: true),
                    storage_id = table.Column<int>(type: "integer", nullable: false),
                    size = table.Column<long>(type: "bigint", nullable: false),
                    longitude = table.Column<double>(type: "double precision", nullable: false),
                    latitude = table.Column<double>(type: "double precision", nullable: false),
                    zoom = table.Column<int>(type: "integer", nullable: false),
                    rotate = table.Column<int>(type: "integer", nullable: false),
                    flip = table.Column<bool>(type: "boolean", nullable: false),
                    direction = table.Column<float>(type: "real", nullable: false),
                    angle = table.Column<float>(type: "real", nullable: false),
                    camera_manufacturer = table.Column<string>(type: "text", nullable: true),
                    camera_model = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shots", x => x.id);
                    table.ForeignKey(
                        name: "FK_shots_albums_album_id",
                        column: x => x.album_id,
                        principalTable: "albums",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shots_storages_storage_id",
                        column: x => x.storage_id,
                        principalTable: "storages",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PersonShot",
                columns: table => new
                {
                    PersonsPersonId = table.Column<int>(type: "integer", nullable: false),
                    ShotsShotId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonShot", x => new { x.PersonsPersonId, x.ShotsShotId });
                    table.ForeignKey(
                        name: "FK_PersonShot_persons_PersonsPersonId",
                        column: x => x.PersonsPersonId,
                        principalTable: "persons",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PersonShot_shots_ShotsShotId",
                        column: x => x.ShotsShotId,
                        principalTable: "shots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shot_comments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    author_id = table.Column<int>(type: "integer", nullable: false),
                    author_username = table.Column<string>(type: "text", nullable: true),
                    shot_id = table.Column<int>(type: "integer", nullable: false),
                    time = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    text = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shot_comments", x => x.id);
                    table.ForeignKey(
                        name: "FK_shot_comments_shots_shot_id",
                        column: x => x.shot_id,
                        principalTable: "shots",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_shot_comments_users_author_id",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_album_comments_album_id",
                table: "album_comments",
                column: "album_id");

            migrationBuilder.CreateIndex(
                name: "IX_album_comments_author_id",
                table: "album_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_albums_UserId",
                table: "albums",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonShot_ShotsShotId",
                table: "PersonShot",
                column: "ShotsShotId");

            migrationBuilder.CreateIndex(
                name: "IX_shared_albums_guest_user_id",
                table: "shared_albums",
                column: "guest_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shared_albums_shared_album_id",
                table: "shared_albums",
                column: "shared_album_id");

            migrationBuilder.CreateIndex(
                name: "IX_shared_users_guest_user_id",
                table: "shared_users",
                column: "guest_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shared_users_host_user_id",
                table: "shared_users",
                column: "host_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_shot_comments_author_id",
                table: "shot_comments",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "IX_shot_comments_shot_id",
                table: "shot_comments",
                column: "shot_id");

            migrationBuilder.CreateIndex(
                name: "IX_shots_album_id",
                table: "shots",
                column: "album_id");

            migrationBuilder.CreateIndex(
                name: "IX_shots_MD5",
                table: "shots",
                column: "MD5",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_shots_storage_id",
                table: "shots",
                column: "storage_id");

            migrationBuilder.CreateIndex(
                name: "IX_storages_user_id",
                table: "storages",
                column: "user_id");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "album_comments");

            migrationBuilder.DropTable(
                name: "locations");

            migrationBuilder.DropTable(
                name: "PersonShot");

            migrationBuilder.DropTable(
                name: "shared_albums");

            migrationBuilder.DropTable(
                name: "shared_users");

            migrationBuilder.DropTable(
                name: "shot_comments");

            migrationBuilder.DropTable(
                name: "persons");

            migrationBuilder.DropTable(
                name: "shots");

            migrationBuilder.DropTable(
                name: "albums");

            migrationBuilder.DropTable(
                name: "storages");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
