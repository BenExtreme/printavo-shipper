using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFedExCarrierFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Carrier",
                table: "ShipmentRecords",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "FedExShipmentTransactionId",
                table: "ShipmentRecords",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FedExAccountNumber",
                table: "AppSettings",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "FedExClientIdProtected",
                table: "AppSettings",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "FedExClientSecretProtected",
                table: "AppSettings",
                type: "BLOB",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FedExEnvironment",
                table: "AppSettings",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "ContractorFedExBillingAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractorId = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    AccountNumber = table.Column<string>(type: "TEXT", nullable: false),
                    AccountPostalCode = table.Column<string>(type: "TEXT", nullable: false),
                    AccountCountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorFedExBillingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorFedExBillingAccounts_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OwnFedExAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    AccountNumber = table.Column<string>(type: "TEXT", nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OwnFedExAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorFedExBillingAccounts_ContractorId",
                table: "ContractorFedExBillingAccounts",
                column: "ContractorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ContractorFedExBillingAccounts");

            migrationBuilder.DropTable(
                name: "OwnFedExAccounts");

            migrationBuilder.DropColumn(
                name: "Carrier",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "FedExShipmentTransactionId",
                table: "ShipmentRecords");

            migrationBuilder.DropColumn(
                name: "FedExAccountNumber",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FedExClientIdProtected",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FedExClientSecretProtected",
                table: "AppSettings");

            migrationBuilder.DropColumn(
                name: "FedExEnvironment",
                table: "AppSettings");
        }
    }
}
