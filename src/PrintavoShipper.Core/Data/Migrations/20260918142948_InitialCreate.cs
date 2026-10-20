using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PrintavoShipper.Core.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrintavoEmail = table.Column<string>(type: "TEXT", nullable: true),
                    PrintavoApiTokenProtected = table.Column<byte[]>(type: "BLOB", nullable: true),
                    PrintavoCurrentUserId = table.Column<string>(type: "TEXT", nullable: true),
                    PrintavoCurrentUserName = table.Column<string>(type: "TEXT", nullable: true),
                    UpsClientIdProtected = table.Column<byte[]>(type: "BLOB", nullable: true),
                    UpsClientSecretProtected = table.Column<byte[]>(type: "BLOB", nullable: true),
                    UpsEnvironment = table.Column<int>(type: "INTEGER", nullable: false),
                    UpsShipperAccountNumber = table.Column<string>(type: "TEXT", nullable: true),
                    UpsShipperAccountPostalCode = table.Column<string>(type: "TEXT", nullable: true),
                    UpsShipperAccountCountryCode = table.Column<string>(type: "TEXT", nullable: true),
                    UpsMerchantId = table.Column<string>(type: "TEXT", nullable: true),
                    SelectedPrinterName = table.Column<string>(type: "TEXT", nullable: true),
                    LabelImageFormat = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSettings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contractors",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: false),
                    IsConfigured = table.Column<bool>(type: "INTEGER", nullable: false),
                    FirstSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastSeenAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contractors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ContractorBillingAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractorId = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    UpsAccountNumber = table.Column<string>(type: "TEXT", nullable: false),
                    UpsAccountPostalCode = table.Column<string>(type: "TEXT", nullable: false),
                    UpsAccountCountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Notes = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorBillingAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorBillingAccounts_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ContractorShipFromAddresses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ContractorId = table.Column<string>(type: "TEXT", nullable: false),
                    Label = table.Column<string>(type: "TEXT", nullable: false),
                    AttentionName = table.Column<string>(type: "TEXT", nullable: true),
                    CompanyName = table.Column<string>(type: "TEXT", nullable: true),
                    Address1 = table.Column<string>(type: "TEXT", nullable: false),
                    Address2 = table.Column<string>(type: "TEXT", nullable: true),
                    City = table.Column<string>(type: "TEXT", nullable: false),
                    State = table.Column<string>(type: "TEXT", nullable: false),
                    PostalCode = table.Column<string>(type: "TEXT", nullable: false),
                    CountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    PhoneNumber = table.Column<string>(type: "TEXT", nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ContractorShipFromAddresses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ContractorShipFromAddresses_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    PrintavoOrderId = table.Column<string>(type: "TEXT", nullable: false),
                    PrintavoVisualId = table.Column<string>(type: "TEXT", nullable: false),
                    ContractorId = table.Column<string>(type: "TEXT", nullable: true),
                    ShipFromLabel = table.Column<string>(type: "TEXT", nullable: false),
                    ShipFromAttentionName = table.Column<string>(type: "TEXT", nullable: true),
                    ShipFromCompanyName = table.Column<string>(type: "TEXT", nullable: true),
                    ShipFromAddress1 = table.Column<string>(type: "TEXT", nullable: false),
                    ShipFromAddress2 = table.Column<string>(type: "TEXT", nullable: true),
                    ShipFromCity = table.Column<string>(type: "TEXT", nullable: false),
                    ShipFromState = table.Column<string>(type: "TEXT", nullable: false),
                    ShipFromPostalCode = table.Column<string>(type: "TEXT", nullable: false),
                    ShipFromCountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    ShipToLabel = table.Column<string>(type: "TEXT", nullable: true),
                    ShipToAttentionName = table.Column<string>(type: "TEXT", nullable: true),
                    ShipToCompanyName = table.Column<string>(type: "TEXT", nullable: true),
                    ShipToAddress1 = table.Column<string>(type: "TEXT", nullable: false),
                    ShipToAddress2 = table.Column<string>(type: "TEXT", nullable: true),
                    ShipToCity = table.Column<string>(type: "TEXT", nullable: false),
                    ShipToState = table.Column<string>(type: "TEXT", nullable: false),
                    ShipToPostalCode = table.Column<string>(type: "TEXT", nullable: false),
                    ShipToCountryCode = table.Column<string>(type: "TEXT", nullable: false),
                    BillingType = table.Column<int>(type: "INTEGER", nullable: false),
                    BillingAccountLabel = table.Column<string>(type: "TEXT", nullable: true),
                    BillingAccountNumber = table.Column<string>(type: "TEXT", nullable: true),
                    BillingAccountPostalCode = table.Column<string>(type: "TEXT", nullable: true),
                    ServiceLevelCode = table.Column<string>(type: "TEXT", nullable: false),
                    ServiceLevelName = table.Column<string>(type: "TEXT", nullable: false),
                    PackageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    TotalCost = table.Column<decimal>(type: "TEXT", precision: 10, scale: 2, nullable: true),
                    UpsShipmentIdentificationNumber = table.Column<string>(type: "TEXT", nullable: true),
                    FirstTrackingNumber = table.Column<string>(type: "TEXT", nullable: true),
                    PrintavoTaskId = table.Column<string>(type: "TEXT", nullable: true),
                    CustomerNoteAppended = table.Column<bool>(type: "INTEGER", nullable: false),
                    Status = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedByWindowsUser = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    VoidedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentRecords_Contractors_ContractorId",
                        column: x => x.ContractorId,
                        principalTable: "Contractors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ShipmentPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ShipmentRecordId = table.Column<int>(type: "INTEGER", nullable: false),
                    PackageIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    WeightLbs = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: false),
                    LengthIn = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: true),
                    WidthIn = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: true),
                    HeightIn = table.Column<decimal>(type: "TEXT", precision: 8, scale: 2, nullable: true),
                    TrackingNumber = table.Column<string>(type: "TEXT", nullable: true),
                    LabelImagePath = table.Column<string>(type: "TEXT", nullable: true),
                    PrintedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShipmentPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ShipmentPackages_ShipmentRecords_ShipmentRecordId",
                        column: x => x.ShipmentRecordId,
                        principalTable: "ShipmentRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ContractorBillingAccounts_ContractorId",
                table: "ContractorBillingAccounts",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ContractorShipFromAddresses_ContractorId",
                table: "ContractorShipFromAddresses",
                column: "ContractorId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentPackages_ShipmentRecordId",
                table: "ShipmentPackages",
                column: "ShipmentRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ShipmentRecords_ContractorId",
                table: "ShipmentRecords",
                column: "ContractorId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSettings");

            migrationBuilder.DropTable(
                name: "ContractorBillingAccounts");

            migrationBuilder.DropTable(
                name: "ContractorShipFromAddresses");

            migrationBuilder.DropTable(
                name: "ShipmentPackages");

            migrationBuilder.DropTable(
                name: "ShipmentRecords");

            migrationBuilder.DropTable(
                name: "Contractors");
        }
    }
}
