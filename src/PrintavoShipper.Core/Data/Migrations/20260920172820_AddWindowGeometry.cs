using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWindowGeometry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsConfigured",
                table: "Contractors");

            migrationBuilder.AddColumn<double>(
                name: "WindowHeight",
                table: "AppSettings",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "WindowIsMaximized",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "WindowLeft",
                table: "AppSettings",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WindowTop",
                table: "AppSettings",
                type: "REAL",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "WindowWidth",
                table: "AppSettings",
                type: "REAL",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "WindowHeight",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "WindowIsMaximized",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "WindowLeft",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "WindowTop",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "WindowWidth",
                table: "AppSettings");

            migrationBuilder.AddColumn<bool>(
                name: "IsConfigured",
                table: "Contractors",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }
    }
}
