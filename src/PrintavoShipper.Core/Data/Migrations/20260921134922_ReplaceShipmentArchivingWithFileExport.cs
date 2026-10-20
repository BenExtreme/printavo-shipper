using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceShipmentArchivingWithFileExport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "IsArchived",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "ShipmentAutoArchiveDays",
                table: "AppSettings");

            migrationBuilder.AddColumn<int>(
                name: "ShipmentArchiveIntervalWeeks",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ShipmentArchiveLastRunAt",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ShipmentArchiveIntervalWeeks",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ShipmentArchiveLastRunAt",
                table: "AppSettings");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "ArchivedAt",
                table: "ShipmentRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsArchived",
                table: "ShipmentRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShipmentAutoArchiveDays",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 30);
        }
    }
}
