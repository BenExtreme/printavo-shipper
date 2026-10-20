using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShipmentNotes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "ShipmentRecords",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Notes",
                table: "ShipmentRecords");
        }
    }
}
