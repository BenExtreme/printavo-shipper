using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultCarrier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DefaultCarrier",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultCarrier",
                table: "AppSettings");
        }
    }
}
