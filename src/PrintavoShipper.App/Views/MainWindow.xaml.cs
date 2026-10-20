using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls.Primitives;
using PrintavoShipper.App.ViewModels;
using PrintavoShipper.Core.Interfaces;
using Wpf.Ui.Controls;

namespace PrintavoShipper.App.Views;

public partial class MainWindow : FluentWindow
{
    private readonly ShellViewModel _viewModel;
    private readonly ISettingsService _settingsService;

    public MainWindow(ShellViewModel viewModel, ISettingsService settingsService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _settingsService = settingsService;
        DataContext = viewModel;

        // Follows Windows' light/dark setting (and live-switches if it
        // changes while the app is running) instead of the hardcoded Light
        // theme App.xaml's static <ui:ThemesDictionary Theme="Light" />
        // previously locked in — that declaration is still there as the
        // pre-Watch fallback for the brief moment before this runs.
        Wpf.Ui.Appearance.SystemThemeWatcher.Watch(this);

        RestoreWindowGeometry();

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        SyncActiveNavItem();
        SyncBackButtonState();

        // NavigationView.OnBackButtonClick (its PART_BackButton's Click
        // handler) only raises BackRequested via the control's own internal
        // GoBack(), which itself no-ops unless the control's own Frame
        // journal — populated exclusively by NavigationView.Navigate(...),
        // which this app never calls — has more than one entry. So
        // BackRequested never fires here no matter what. Listening for the
        // Click event's bubble instead (it isn't marked Handled) and
        // filtering by the template part's name is what actually lets the
        // pane's back button do anything in this app.
        RootNavigationView.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler(OnNavigationViewClick));

        Closing += OnWindowClosing;
    }

    // Blocking on this one-time local SQLite read before Show() is simpler
    // than juggling an async restore around window layout, and it's fast
    // enough (single-row local read, migrations already ran in OnStartup
    // before this constructor is reached) not to be felt as a startup delay.
    // Wrapped in Task.Run rather than awaited/blocked-on directly: this runs
    // on the UI thread, and a bare .GetAwaiter().GetResult() on a Task whose
    // internal awaits capture the WPF SynchronizationContext would deadlock
    // (the continuation can never run because the UI thread that would pump
    // it is the one blocked waiting). Task.Run hands the whole call to a
    // thread-pool thread with no captured context, so it can't deadlock.
    private void RestoreWindowGeometry()
    {
        var settings = Task.Run(() => _settingsService.GetSettingsAsync()).GetAwaiter().GetResult();
        if (settings.WindowWidth is > 0 && settings.WindowHeight is > 0)
        {
            Width = settings.WindowWidth.Value;
            Height = settings.WindowHeight.Value;
        }

        if (settings.WindowLeft is not null && settings.WindowTop is not null
            && IsOnAnyScreen(settings.WindowLeft.Value, settings.WindowTop.Value))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = settings.WindowLeft.Value;
            Top = settings.WindowTop.Value;
        }

        if (settings.WindowIsMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    // Guards against restoring a position from a monitor that's since been
    // unplugged/reconfigured, which would otherwise open the window
    // off-screen with no obvious way for staff to get it back. Checked
    // against the combined virtual-screen bounds of all monitors (WPF-native
    // — no WinForms dependency needed just for this).
    private static bool IsOnAnyScreen(double left, double top) =>
        left >= SystemParameters.VirtualScreenLeft - 50
        && left < SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && top >= SystemParameters.VirtualScreenTop - 50
        && top < SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        var isMaximized = WindowState == WindowState.Maximized;
        // RestoreBounds reflects the un-maximized size/position even while
        // currently maximized — saving raw Width/Left etc. while maximized
        // would persist the full-screen dimensions instead of a sensible
        // restored size to come back to.
        var bounds = isMaximized ? RestoreBounds : new Rect(Left, Top, Width, Height);

        // Same Task.Run reasoning as RestoreWindowGeometry — avoids a
        // sync-over-async deadlock on the UI thread. Blocking here (rather
        // than a fire-and-forget) makes sure the save actually lands before
        // the window finishes closing and App.OnExit tears the host down.
        Task.Run(async () =>
        {
            var settings = await _settingsService.GetSettingsAsync();
            settings.WindowIsMaximized = isMaximized;
            settings.WindowWidth = bounds.Width;
            settings.WindowHeight = bounds.Height;
            settings.WindowLeft = bounds.Left;
            settings.WindowTop = bounds.Top;
            await _settingsService.SaveAsync(settings, null, null, null);
        }).GetAwaiter().GetResult();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is null or nameof(ShellViewModel.CurrentViewModel))
        {
            SyncActiveNavItem();
        }

        if (e.PropertyName is null or nameof(ShellViewModel.IsBackEnabled))
        {
            SyncBackButtonState();
        }
    }

    private void OnNavigationViewClick(object sender, RoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { Name: "PART_BackButton" })
        {
            _viewModel.GoBackCommand.Execute(null);
        }
    }

    // NavigationView.IsBackEnabled has no public setter (it's normally
    // driven by the control's own internal Frame/journal, which this app
    // doesn't use — navigation goes through INavigationService instead), so
    // it can't be data-bound from XAML. SetValue bypasses the CLR wrapper's
    // non-public setter and writes the underlying dependency property
    // directly, which is what actually enables/disables the pane's back
    // button and lets it raise BackRequested when clicked.
    private void SyncBackButtonState() =>
        RootNavigationView.SetValue(NavigationView.IsBackEnabledProperty, _viewModel.IsBackEnabled);

    private void SyncActiveNavItem()
    {
        var activeTag = _viewModel.CurrentViewModel switch
        {
            OrdersViewModel => "Orders",
            ContractorsViewModel => "Contractors",
            ShipmentHistoryViewModel => "History",
            SettingsViewModel => "Settings",
            _ => null,
        };

        foreach (var item in RootNavigationView.MenuItems.OfType<NavigationViewItem>())
        {
            item.IsActive = (string?)item.Tag == activeTag;
        }
    }
}
