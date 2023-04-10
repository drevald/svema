using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace svema.Migrations
{
    public partial class shotdate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Date",
                table: "Shots",
                newName: "DateStart");

            migrationBuilder.AddColumn<DateTime>(
                name: "DateEnd",
                table: "Shots",
                type: "timestamp without time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DateEnd",
                table: "Shots");

            migrationBuilder.RenameColumn(
                name: "DateStart",
                table: "Shots",
                newName: "Date");
        }
    }
}
