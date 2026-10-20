using System.IO;
using System.Net;
using System.Net.Http;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PrintavoShipper.App.Services;
using PrintavoShipper.App.ViewModels;
using PrintavoShipper.App.Views;
using PrintavoShipper.Core.Data;
using PrintavoShipper.Core.Interfaces;
using PrintavoShipper.Core.Services;
using PrintavoShipper.FedEx;
using PrintavoShipper.Printavo;
using PrintavoShipper.Ups;
using Serilog;
using Serilog.Events;

namespace PrintavoShipper.App;

public partial class App : Application
{
    private ServiceProvider? _services;
    private SplashWindow? _splash;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Shown before anything else so there's immediate visual feedback —
        // DI container build + EF migration + MainWindow's own blocking
        // geometry read below can take a noticeable moment, and without this
        // the user sees nothing and may think the app didn't launch.
        _splash = new SplashWindow();
        _splash.Show();

        // Without these, any exception that escapes a ViewModel command's own
        // try/catch (a binding error, an async void handler, a background
        // task) kills the process instantly with nothing logged and no
        // message to whoever's mid-shipment. Wired before the try block below
        // so startup failures (Serilog config, DI build, migration) are
        // covered too, not just post-startup ones.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnAppDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        try
        {
            Behaviors.SelectAllTextBehavior.Register();

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                // EF Core logs every SQL command (with full SQL text) at
                // Information — hundreds of synchronous file writes a day, mostly
                // from the UI thread, with no diagnostic value in normal use.
                // Warnings/errors from EF still get through.
                .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
                .WriteTo.File(
                    Path.Combine(DbPathProvider.GetLogsFolder(), "printavoshipper-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14)
                .CreateLogger();

            // Plain DI container rather than the .NET Generic Host: this app uses
            // nothing from the host beyond DI + logging (no hosted services, no
            // appsettings/IConfiguration), and Host.CreateDefaultBuilder's
            // defaults — config file watching, env-var/command-line config,
            // Console/Debug/EventLog log providers — only cost startup time and
            // exe size. Scope validation stays off (the default here) since
            // every Scoped service is deliberately resolved from the root —
            // see ConfigureServices.
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.ClearProviders().AddSerilog(dispose: false));
            ConfigureServices(services);
            _services = services.BuildServiceProvider();

            var dbContextFactory = _services.GetRequiredService<IDbContextFactory<PrintavoShipperDbContext>>();
            using (var db = await dbContextFactory.CreateDbContextAsync())
            {
                await db.Database.MigrateAsync();
            }

            ShopSettingsCache.Apply(await _services.GetRequiredService<ISettingsService>().GetSettingsAsync());

            var mainWindow = _services.GetRequiredService<MainWindow>();
            // WPF auto-assigns Application.MainWindow to the first Window
            // constructed — the splash. DialogService uses MainWindow as the
            // dialog Owner, so it must point at the real window.
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        finally
        {
            // Guaranteed to run whether startup succeeded or threw, so the
            // splash never gets stuck on screen after a startup failure.
            _splash?.Close();
            _splash = null;
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _services?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Logger.Error(e.Exception, "Unhandled UI-thread exception");
        // Plain System.Windows.MessageBox (not the wpf-ui one) deliberately: it's
        // synchronous and needs no dialog-host wiring, which matters here since
        // whatever crashed may have left the UI in an unknown state.
        MessageBox.Show(
            "Something went wrong. Please note what you were doing and report it to whoever supports this app at your shop.\n\n" + e.Exception.Message,
            "Printavo Shipper",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private void OnAppDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Logger.Fatal(ex, "Unhandled background-thread exception");
        }
    }

    private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Logger.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // A factory, not a directly-injected DbContext: this app never creates
        // per-operation DI scopes (everything resolves from one root-level
        // "scope" for the app's whole lifetime), so a Scoped DbContext would
        // behave as an app-lifetime singleton and its change tracker would
        // hand back stale cached entities on repeat queries. Each repository
        // method creates and disposes its own short-lived context instead.
        //
        // SingleQuery is already EF's default; setting it explicitly just
        // stops EF warning about it on every multi-Include contractor query.
        services.AddDbContextFactory<PrintavoShipperDbContext>(options =>
            options.UseSqlite(
                $"Data Source={DbPathProvider.GetDefaultDbPath()}",
                sqlite => sqlite.UseQuerySplittingBehavior(QuerySplittingBehavior.SingleQuery)));

        services.AddScoped<ISettingsService, SettingsService>();
        services.AddScoped<IContractorRepository, ContractorRepository>();
        services.AddScoped<IUpsAccountRepository, UpsAccountRepository>();
        services.AddScoped<IFedExAccountRepository, FedExAccountRepository>();
        services.AddScoped<IShipmentRepository, ShipmentRepository>();
        services.AddScoped<IShipmentArchiveService, ShipmentArchiveService>();
        services.AddScoped<IContractorProvisioningService, ContractorProvisioningService>();
        services.AddScoped<IShippingWorkflowService, ShippingWorkflowService>();
        services.AddSingleton<ILabelPrintService, PrintavoShipper.Core.Printing.LabelPrintService>();

        services.AddScoped<IPrintavoCredentialProvider, PrintavoCredentialProvider>();
        services.AddScoped<IUpsCredentialProvider, UpsCredentialProvider>();
        services.AddScoped<IFedExCredentialProvider, FedExCredentialProvider>();

        services.AddHttpClient<GraphQLExecutor>();
        services.AddScoped<IPrintavoClient, PrintavoClient>();

        // Plain HttpClient for downloading Printavo contractor-profile logos.
        services.AddHttpClient<IBoxLabelService, BoxLabelService>();

        services.AddHttpClient<IUpsAuthService, UpsAuthService>();
        services.AddHttpClient<IUpsClient, UpsClient>();

        // FedEx's sandbox is fronted by infrastructure that gzip/brotli-compresses
        // responses (including error pages) regardless of what the client
        // negotiates. HttpClient's default handler does NOT auto-decompress —
        // without this, a compressed error body gets read as raw bytes and
        // decoded as garbled/mojibake "text", hiding the actual error message.
        // UPS's client doesn't need this (its CIE/Production endpoints don't
        // exhibit the same behavior), so this is FedEx-specific.
        static HttpMessageHandler DecompressingHandler() => new HttpClientHandler { AutomaticDecompression = DecompressionMethods.All };
        services.AddHttpClient<IFedExAuthService, FedExAuthService>().ConfigurePrimaryHttpMessageHandler(DecompressingHandler);
        services.AddHttpClient<IFedExClient, FedExClient>().ConfigurePrimaryHttpMessageHandler(DecompressingHandler);

        services.AddSingleton<INavigationService, NavigationService>();
        services.AddSingleton<IDialogService, DialogService>();

        services.AddScoped<ShellViewModel>();
        services.AddScoped<SettingsViewModel>();
        services.AddScoped<OrdersViewModel>();
        services.AddScoped<NewShipmentViewModel>();
        services.AddScoped<ContractorsViewModel>();
        services.AddScoped<ShipmentHistoryViewModel>();
        // Transient: each order navigated to needs a fresh instance, unlike
        // the other nav targets which intentionally keep their state (e.g.
        // search results) across visits.
        services.AddTransient<OrderDetailViewModel>();
        // Transient for the same reason — each visit should start from a
        // clean "pick a file" state, not resume whatever was loaded last time.
        services.AddTransient<ArchiveViewerViewModel>();

        services.AddScoped<MainWindow>();
    }
}
