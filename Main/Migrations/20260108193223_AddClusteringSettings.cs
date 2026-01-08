using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Svema.Migrations
{
    public partial class AddClusteringSettings : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClusteringSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<int>(type: "integer", nullable: false),
                    Preset = table.Column<int>(type: "integer", nullable: false),
                    SimilarityThreshold = table.Column<float>(type: "real", nullable: false),
                    MinFacesPerPerson = table.Column<int>(type: "integer", nullable: false),
                    MinFaceSize = table.Column<int>(type: "integer", nullable: false),
                    MinFaceQuality = table.Column<float>(type: "real", nullable: false),
                    AutoMergeThreshold = table.Column<float>(type: "real", nullable: false),
                    IsFaceProcessingSuspended = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClusteringSettings", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClusteringSettings");
        }
    }
}
