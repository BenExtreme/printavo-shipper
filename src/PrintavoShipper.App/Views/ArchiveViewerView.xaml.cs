using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PrintavoShipper.App.Views;

public partial class ArchiveViewerView : UserControl
{
    private ScrollViewer? _shipmentsGridScrollViewer;

    public ArchiveViewerView()
    {
        InitializeComponent();
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
