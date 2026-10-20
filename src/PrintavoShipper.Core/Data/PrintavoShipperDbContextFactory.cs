using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PrintavoShipper.Core.Data;

/// <summary>
/// Lets "dotnet ef migrations" run directly against the Core project without
/// needing the WPF App project as the EF tooling startup project.
/// </summary>
public class PrintavoShipperDbContextFactory : IDesignTimeDbContextFactory<PrintavoShipperDbContext>
{
    public PrintavoShipperDbContext CreateDbContext(string[] args)
    {
        var dbPath = DbPathProvider.GetDefaultDbPath();
        var optionsBuilder = new DbContextOptionsBuilder<PrintavoShipperDbContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");
        return new PrintavoShipperDbContext(optionsBuilder.Options);
    }
}

public static class DbPathProvider
{
    public static string GetDefaultDbPath()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintavoShipper");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, "printavoshipper.db");
    }

    public static string GetLabelsFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintavoShipper", "Labels");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetLogsFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintavoShipper", "logs");
        Directory.CreateDirectory(folder);
        return folder;
    }

    public static string GetArchiveFolder()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PrintavoShipper", "Archive");
        Directory.CreateDirectory(folder);
        return folder;
    }
}
