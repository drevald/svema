using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace svema.Migrations
{
    public partial class ccc : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shots_Films_FilmId",
                table: "Shots");

            migrationBuilder.DropTable(
                name: "Films");

            migrationBuilder.RenameColumn(
                name: "FilmId",
                table: "Shots",
                newName: "AlbumId");

            migrationBuilder.RenameIndex(
                name: "IX_Shots_FilmId",
                table: "Shots",
                newName: "IX_Shots_AlbumId");

            migrationBuilder.CreateTable(
                name: "Albums",
                columns: table => new
                {
                    AlbumId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "text", nullable: true),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatePrecision = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Albums", x => x.AlbumId);
                    table.ForeignKey(
                        name: "FK_Albums_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "LocationId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Albums_LocationId",
                table: "Albums",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shots_Albums_AlbumId",
                table: "Shots",
                column: "AlbumId",
                principalTable: "Albums",
                principalColumn: "AlbumId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Shots_Albums_AlbumId",
                table: "Shots");

            migrationBuilder.DropTable(
                name: "Albums");

            migrationBuilder.RenameColumn(
                name: "AlbumId",
                table: "Shots",
                newName: "FilmId");

            migrationBuilder.RenameIndex(
                name: "IX_Shots_AlbumId",
                table: "Shots",
                newName: "IX_Shots_FilmId");

            migrationBuilder.CreateTable(
                name: "Films",
                columns: table => new
                {
                    FilmId = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LocationId = table.Column<int>(type: "integer", nullable: true),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DatePrecision = table.Column<string>(type: "text", nullable: true),
                    Name = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Films", x => x.FilmId);
                    table.ForeignKey(
                        name: "FK_Films_Location_LocationId",
                        column: x => x.LocationId,
                        principalTable: "Location",
                        principalColumn: "LocationId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Films_LocationId",
                table: "Films",
                column: "LocationId");

            migrationBuilder.AddForeignKey(
                name: "FK_Shots_Films_FilmId",
                table: "Shots",
                column: "FilmId",
                principalTable: "Films",
                principalColumn: "FilmId");
        }
    }
}
