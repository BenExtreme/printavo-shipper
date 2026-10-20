using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDefaultShipFromAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromAddress1",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromAddress2",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: true,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromAttentionName",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromCity",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromCompanyName",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromCountryCode",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 2,
                nullable: false,
                defaultValue: "US");

            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromPhoneNumber",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 15,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromPostalCode",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 9,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DefaultShipFromState",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 5,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DefaultShipFromAddress1",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShipFromAddress2",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShipFromAttentionName",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShipFromCity",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShipFromCompanyName",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShipFromCountryCode",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShipFromPhoneNumber",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShipFromPostalCode",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DefaultShipFromState",
                table: "AppSettings");
        }
    }
}
