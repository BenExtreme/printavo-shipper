using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PrintavoShipper.App.ViewModels;
using PrintavoShipper.Printavo.Dto;

namespace PrintavoShipper.App.Views;

public partial class OrdersView : UserControl
{
    public OrdersView()
    {
        InitializeComponent();
        Loaded += (_, _) => VisualIdBox.Focus();
    }

    private void OrdersGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        // Only when the double-click actually landed on a row (not empty
        // grid space below the last row) — walk up from whatever was
        // literally clicked until a DataGridRow is found or we run out.
        var element = e.OriginalSource as DependencyObject;
        while (element is not null and not DataGridRow)
        {
            element = VisualTreeHelper.GetParent(element);
        }

        if (element is DataGridRow { Item: PrintavoOrderSummary order } && DataContext is OrdersViewModel viewModel)
        {
            viewModel.SelectOrderCommand.Execute(order);
        }
    }
}
