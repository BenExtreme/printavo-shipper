using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class SwitchLabelFormatToEpl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LabelImageFormat",
                table: "AppSettings");

            migrationBuilder.AddColumn<bool>(
                name: "FedExUseRasterLabel",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UpsUseRasterLabel",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FedExUseRasterLabel",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UpsUseRasterLabel",
                table: "AppSettings");

            migrationBuilder.AddColumn<string>(
                name: "LabelImageFormat",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");
        }
    }
}
