using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.Core.Data;

public class PrintavoShipperDbContext : DbContext
{
    public PrintavoShipperDbContext(DbContextOptions<PrintavoShipperDbContext> options)
        : base(options)
    {
    }

    public DbSet<AppSettings> AppSettings => Set<AppSettings>();
    public DbSet<OwnUpsAccount> OwnUpsAccounts => Set<OwnUpsAccount>();
    public DbSet<OwnFedExAccount> OwnFedExAccounts => Set<OwnFedExAccount>();
    public DbSet<Contractor> Contractors => Set<Contractor>();
    public DbSet<ContractorShipFromAddress> ContractorShipFromAddresses => Set<ContractorShipFromAddress>();
    public DbSet<ContractorBillingAccount> ContractorBillingAccounts => Set<ContractorBillingAccount>();
    public DbSet<ContractorFedExBillingAccount> ContractorFedExBillingAccounts => Set<ContractorFedExBillingAccount>();
    public DbSet<ShipmentRecord> ShipmentRecords => Set<ShipmentRecord>();
    public DbSet<ShipmentPackage> ShipmentPackages => Set<ShipmentPackage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppSettings>(b =>
        {
            b.ToTable("AppSettings");
            b.HasKey(x => x.Id);
            // Ship-from fields start blank on a fresh install (each shop
            // enters its own in Settings); only the country has a default.
            b.Property(x => x.DefaultShipFromCompanyName).HasMaxLength(UpsAddressLimits.NameMaxLength);
            b.Property(x => x.DefaultShipFromAttentionName).HasMaxLength(UpsAddressLimits.AttentionNameMaxLength);
            b.Property(x => x.DefaultShipFromAddress1).HasMaxLength(UpsAddressLimits.AddressLineMaxLength);
            b.Property(x => x.DefaultShipFromAddress2).HasMaxLength(UpsAddressLimits.AddressLineMaxLength);
            b.Property(x => x.DefaultShipFromCity).HasMaxLength(UpsAddressLimits.CityMaxLength);
            b.Property(x => x.DefaultShipFromState).HasMaxLength(UpsAddressLimits.StateProvinceCodeMaxLength);
            b.Property(x => x.DefaultShipFromPostalCode).HasMaxLength(UpsAddressLimits.PostalCodeMaxLength);
            b.Property(x => x.DefaultShipFromCountryCode).HasMaxLength(UpsAddressLimits.CountryCodeLength).HasDefaultValue("US");
            b.Property(x => x.DefaultShipFromPhoneNumber).HasMaxLength(UpsAddressLimits.PhoneNumberMaxLength);
            b.Property(x => x.ShipmentArchiveIntervalWeeks).HasDefaultValue(2);

            b.Ignore(x => x.IsDefaultShipFromConfigured);
            JsonList(b.Property(x => x.ShippingStatuses));
            JsonList(b.Property(x => x.PickupStatuses));
            JsonList(b.Property(x => x.HiddenOrderStatuses));
            JsonList(b.Property(x => x.StaffMembers));
            JsonList(b.Property(x => x.UpsDeliveryMethodNames));
            JsonList(b.Property(x => x.FedExDeliveryMethodNames));
            JsonList(b.Property(x => x.PickupDeliveryMethodNames));
            JsonList(b.Property(x => x.BoxSizePresets));
            b.Property(x => x.ShippingFeeMarkupPercent).HasPrecision(6, 2);
        });

        modelBuilder.Entity<OwnUpsAccount>(b =>
        {
            b.ToTable("OwnUpsAccounts");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<OwnFedExAccount>(b =>
        {
            b.ToTable("OwnFedExAccounts");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<Contractor>(b =>
        {
            b.ToTable("Contractors");
            b.HasKey(x => x.Id);
            b.HasMany(x => x.ShipFromAddresses)
                .WithOne(x => x.Contractor)
                .HasForeignKey(x => x.ContractorId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.BillingAccounts)
                .WithOne(x => x.Contractor)
                .HasForeignKey(x => x.ContractorId)
                .OnDelete(DeleteBehavior.Cascade);
            b.HasMany(x => x.FedExBillingAccounts)
                .WithOne(x => x.Contractor)
                .HasForeignKey(x => x.ContractorId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ContractorShipFromAddress>(b =>
        {
            b.ToTable("ContractorShipFromAddresses");
            b.HasKey(x => x.Id);
            // UPS Shipping API limits (Shipment_ShipFrom schema) — see UpsAddressLimits.
            b.Property(x => x.AttentionName).HasMaxLength(UpsAddressLimits.AttentionNameMaxLength);
            b.Property(x => x.CompanyName).HasMaxLength(UpsAddressLimits.NameMaxLength);
            b.Property(x => x.Address1).HasMaxLength(UpsAddressLimits.AddressLineMaxLength);
            b.Property(x => x.Address2).HasMaxLength(UpsAddressLimits.AddressLineMaxLength);
            b.Property(x => x.City).HasMaxLength(UpsAddressLimits.CityMaxLength);
            b.Property(x => x.State).HasMaxLength(UpsAddressLimits.StateProvinceCodeMaxLength);
            b.Property(x => x.PostalCode).HasMaxLength(UpsAddressLimits.PostalCodeMaxLength);
            b.Property(x => x.CountryCode).HasMaxLength(UpsAddressLimits.CountryCodeLength);
            b.Property(x => x.PhoneNumber).HasMaxLength(UpsAddressLimits.PhoneNumberMaxLength);
        });

        modelBuilder.Entity<ContractorBillingAccount>(b =>
        {
            b.ToTable("ContractorBillingAccounts");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ContractorFedExBillingAccount>(b =>
        {
            b.ToTable("ContractorFedExBillingAccounts");
            b.HasKey(x => x.Id);
        });

        modelBuilder.Entity<ShipmentRecord>(b =>
        {
            b.ToTable("ShipmentRecords");
            b.HasKey(x => x.Id);
            b.Property(x => x.Carrier).HasDefaultValue(ShipmentCarrier.Ups);
            b.Property(x => x.TotalCost).HasPrecision(10, 2);
            b.Property(x => x.ReferenceNumber1).HasMaxLength(UpsAddressLimits.ReferenceNumberMaxLength);
            b.Property(x => x.ReferenceNumber2).HasMaxLength(UpsAddressLimits.ReferenceNumberMaxLength);
            b.Property(x => x.QuantumViewPrimaryEmail).HasMaxLength(UpsAddressLimits.EmailMaxLength);
            b.Property(x => x.QuantumViewSecondaryEmail).HasMaxLength(UpsAddressLimits.EmailMaxLength);
            b.HasOne(x => x.Contractor)
                .WithMany()
                .HasForeignKey(x => x.ContractorId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasMany(x => x.Packages)
                .WithOne(x => x.ShipmentRecord)
                .HasForeignKey(x => x.ShipmentRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ShipmentPackage>(b =>
        {
            b.ToTable("ShipmentPackages");
            b.HasKey(x => x.Id);
            b.Property(x => x.WeightLbs).HasPrecision(8, 2);
            b.Property(x => x.LengthIn).HasPrecision(8, 2);
            b.Property(x => x.WidthIn).HasPrecision(8, 2);
            b.Property(x => x.HeightIn).HasPrecision(8, 2);
        });
    }

    /// <summary>
    /// Stores a settings list as one JSON text column. An empty column (what
    /// SQLite backfills when the column is added) reads back as an empty list.
    /// The comparer makes change tracking compare contents, not references.
    /// </summary>
    private static void JsonList<T>(PropertyBuilder<List<T>> property) =>
        property
            .HasConversion(
                v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                v => string.IsNullOrEmpty(v) ? new List<T>() : JsonSerializer.Deserialize<List<T>>(v, (JsonSerializerOptions?)null) ?? new List<T>(),
                new ValueComparer<List<T>>(
                    (a, b) => a == null ? b == null : b != null && a.SequenceEqual(b),
                    v => v.Aggregate(0, (hash, item) => HashCode.Combine(hash, item == null ? 0 : item.GetHashCode())),
                    v => v.ToList()));
}
