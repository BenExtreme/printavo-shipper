using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PrintavoShipper.App.ViewModels;

namespace PrintavoShipper.App.Views;

public partial class NewShipmentView : UserControl
{
    public NewShipmentView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is NewShipmentViewModel oldVm)
        {
            oldVm.ShipmentCardFocusRequested -= ScrollToShipmentCard;
        }

        if (e.NewValue is NewShipmentViewModel newVm)
        {
            newVm.ShipmentCardFocusRequested += ScrollToShipmentCard;
        }
    }

    /// <summary>
    /// Scrolls the column so the card's top sits at the top of the
    /// viewport. Deferred to Loaded priority because a just-added card has
    /// no generated container or layout position until the next layout pass.
    /// </summary>
    private void ScrollToShipmentCard(object card) =>
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, () =>
        {
            if (ShipmentCardsList.ItemContainerGenerator.ContainerFromItem(card) is not FrameworkElement container
                || !container.IsDescendantOf(LeftScrollViewer))
            {
                return;
            }

            var top = container.TransformToAncestor(LeftScrollViewer).Transform(new Point(0, 0)).Y;
            LeftScrollViewer.ScrollToVerticalOffset(LeftScrollViewer.VerticalOffset + top);
        });
}
