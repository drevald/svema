using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Svema.Migrations
{
    public partial class AddClusterQualityParameters : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<float>(
                name: "MaxDispersion",
                table: "ClusteringSettings",
                type: "real",
                nullable: false,
                defaultValue: 0f);

            migrationBuilder.AddColumn<float>(
                name: "MinPairwiseSimilarity",
                table: "ClusteringSettings",
                type: "real",
                nullable: false,
                defaultValue: 0f);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MaxDispersion",
                table: "ClusteringSettings");

            migrationBuilder.DropColumn(
                name: "MinPairwiseSimilarity",
                table: "ClusteringSettings");
        }
    }
}
