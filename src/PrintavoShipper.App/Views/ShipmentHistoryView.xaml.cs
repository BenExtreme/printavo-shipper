using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using PrintavoShipper.App.ViewModels;
using PrintavoShipper.Core.Models;

namespace PrintavoShipper.App.Views;

public partial class ShipmentHistoryView : UserControl
{
    private ScrollViewer? _shipmentsGridScrollViewer;

    public ShipmentHistoryView()
    {
        InitializeComponent();
        Loaded += (_, _) => SearchBox.Focus();
    }

    private void ShipmentsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // A quick double-click on a button inside a row (expand chevron, Actions
        // column, per-package copy) still raises the grid's MouseDoubleClick —
        // don't let that also navigate away to the order.
        if (FindAncestor<ButtonBase>(e.OriginalSource as DependencyObject) is not null)
        {
            return;
        }

        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is { Item: ShipmentRecord record }
            && DataContext is ShipmentHistoryViewModel viewModel)
        {
            viewModel.OpenOrderCommand.Execute(record);
        }
    }

    private void ShipmentsGrid_ContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        // Always handled: wpf-ui's cells own a default Copy/Cut/Paste menu that
        // would otherwise open instead (see the comment on
        // ShipmentRowContextMenu in the XAML). Right-clicks on a group date
        // header, column header, or empty grid space have no shipment to copy
        // from — no menu at all there.
        e.Handled = true;
        if (FindAncestor<DataGridRow>(e.OriginalSource as DependencyObject) is not { Item: ShipmentRecord record } row)
        {
            return;
        }

        ShipmentsGrid.SelectedItem = record;
        var menu = (ContextMenu)ShipmentsGrid.Resources["ShipmentRowContextMenu"];
        menu.DataContext = record;
        menu.PlacementTarget = row;
        // CursorLeft is -1 when opened from the keyboard (Menu key/Shift+F10) —
        // anchor under the row then instead of at a stale mouse position.
        menu.Placement = e.CursorLeft < 0 ? PlacementMode.Bottom : PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void CopyMasterTrackingNumber_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ShipmentRecord record && DataContext is ShipmentHistoryViewModel viewModel)
        {
            viewModel.CopyMasterTrackingNumberCommand.Execute(record);
        }
    }

    private void CopyAllTrackingNumbers_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is ShipmentRecord record && DataContext is ShipmentHistoryViewModel viewModel)
        {
            viewModel.CopyAllTrackingNumbersCommand.Execute(record);
        }
    }

    private static T? FindAncestor<T>(DependencyObject? element) where T : DependencyObject
    {
        // Runs/other ContentElements aren't Visuals — VisualTreeHelper.GetParent
        // throws on them, so step through their logical parent instead.
        while (element is not null and not T)
        {
            element = element is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(element)
                : LogicalTreeHelper.GetParent(element);
        }

        return element as T;
    }

    private void ShipmentsGrid_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers != ModifierKeys.Shift)
        {
            return;
        }

        _shipmentsGridScrollViewer ??= FindVisualChild<ScrollViewer>(ShipmentsGrid);
        if (_shipmentsGridScrollViewer is null)
        {
            return;
        }

        _shipmentsGridScrollViewer.ScrollToHorizontalOffset(_shipmentsGridScrollViewer.HorizontalOffset - e.Delta);
        e.Handled = true;
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typed)
            {
                return typed;
            }

            var result = FindVisualChild<T>(child);
            if (result is not null)
            {
                return result;
            }
        }

        return null;
    }
}
