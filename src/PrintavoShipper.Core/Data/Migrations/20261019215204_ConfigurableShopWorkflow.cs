using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class ConfigurableShopWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromState",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 5,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 5,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromPostalCode",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 9,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 9,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromCompanyName",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 35,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromCity",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 30,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromAddress2",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 35,
                oldNullable: true,
                oldDefaultValue: "");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromAddress1",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 35,
                oldDefaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "BoxSizePresets",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DeliveryNotifyContacts",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "FedExDeliveryMethodNames",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "HiddenOrderStatuses",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PickupDeliveryMethodNames",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PickupStatuses",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ShippingCostTaskAssigneeUserId",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ShippingFeeMarkupPercent",
                table: "AppSettings",
                type: "TEXT",
                precision: 6,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ShippingStatuses",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "TaskAssignees",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "UpsDeliveryMethodNames",
                table: "AppSettings",
                type: "TEXT",
                nullable: false,
                defaultValue: "");

            // Give a settings row that already exists (an install upgraded
            // from before these were settings) the same starting delivery
            // method names a fresh install gets from AppSettings' defaults —
            // otherwise every order would show as an unrecognized delivery
            // method. Everything else starts blank, set up in Settings.
            migrationBuilder.Sql("""
                UPDATE AppSettings SET
                    UpsDeliveryMethodNames = '["UPS","Shipping"]',
                    FedExDeliveryMethodNames = '["FedEx"]',
                    PickupDeliveryMethodNames = '["Pick-up"]'
                WHERE Id = 1;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BoxSizePresets",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "DeliveryNotifyContacts",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FedExDeliveryMethodNames",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "HiddenOrderStatuses",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "PickupDeliveryMethodNames",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "PickupStatuses",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ShippingCostTaskAssigneeUserId",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ShippingFeeMarkupPercent",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "ShippingStatuses",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "TaskAssignees",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "UpsDeliveryMethodNames",
                table: "AppSettings");

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromState",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 5,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 5);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromPostalCode",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 9,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 9);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromCompanyName",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 35);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromCity",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 30,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromAddress2",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: true,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 35,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "DefaultShipFromAddress1",
                table: "AppSettings",
                type: "TEXT",
                maxLength: 35,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "TEXT",
                oldMaxLength: 35);
        }
    }
}
