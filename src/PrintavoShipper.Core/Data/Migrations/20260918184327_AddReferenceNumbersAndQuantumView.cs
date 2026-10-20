using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReferenceNumbersAndQuantumView : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "QuantumViewEnabled",
                table: "ShipmentRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QuantumViewPrimaryEmail",
                table: "ShipmentRecords",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QuantumViewSecondaryEmail",
                table: "ShipmentRecords",
                type: "TEXT",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber1",
                table: "ShipmentRecords",
                type: "TEXT",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferenceNumber2",
                table: "ShipmentRecords",
                type: "TEXT",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultQuantumViewSecondaryEmail",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuantumViewEnabled",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "QuantumViewPrimaryEmail",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "QuantumViewSecondaryEmail",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber1",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "ReferenceNumber2",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "DefaultQuantumViewSecondaryEmail",
                table: "AppSettings");
        }
    }
}
