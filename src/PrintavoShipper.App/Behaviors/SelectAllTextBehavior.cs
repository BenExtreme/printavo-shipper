using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Threading;

namespace PrintavoShipper.App.Behaviors;

/// <summary>
/// App-wide "select all on focus" for every text box. Registered once as class
/// handlers on TextBoxBase (System.Windows.Controls.TextBox, RichTextBox, and —
/// confirmed against Wpf.Ui 4.3.0's actual type hierarchy — ui:TextBox,
/// ui:NumberBox and ui:PasswordBox all derive from it too), so nothing needs to
/// opt in per-control in XAML and it keeps working for any text box added later.
/// GotKeyboardFocus alone isn't enough for mouse clicks: the click's own default
/// MouseDown handling immediately re-collapses the selection to the click point,
/// so PreviewMouseLeftButtonDown intercepts the first click on a not-yet-focused
/// box and lets Focus() (which fires GotKeyboardFocus) do the selecting instead.
/// Read-only boxes (e.g. the notes RichTextBoxes) are skipped so users can still
/// click-drag to copy a snippet instead of the whole thing being force-selected.
/// Calling SelectAll() synchronously inside GotKeyboardFocus sets the selection
/// correctly (confirmed via live tracing: SelectedText/SelectionBrush/IsKeyboardFocused
/// all came back exactly right) but it doesn't render — something in wpf-ui
/// TextBox's own focus handling (placeholder/clear-button visibility, most
/// likely) runs afterward on the same event and clears the visible selection.
/// Deferring the call to the Input dispatcher priority lets it run after that
/// settles, which is the standard fix for this exact WPF symptom.
/// </summary>
public static class SelectAllTextBehavior
{
    public static void Register()
    {
        EventManager.RegisterClassHandler(typeof(TextBoxBase), UIElement.GotKeyboardFocusEvent, new RoutedEventHandler(OnGotKeyboardFocus));
        EventManager.RegisterClassHandler(typeof(TextBoxBase), UIElement.PreviewMouseLeftButtonDownEvent, new MouseButtonEventHandler(OnPreviewMouseLeftButtonDown));
    }

    private static void OnGotKeyboardFocus(object sender, RoutedEventArgs e)
    {
        switch (sender)
        {
            case TextBox { IsReadOnly: false } textBox:
                textBox.Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() => textBox.SelectAll()));
                break;
            case RichTextBox { IsReadOnly: false } richTextBox:
                richTextBox.Dispatcher.BeginInvoke(DispatcherPriority.Input, new System.Action(() => richTextBox.SelectAll()));
                break;
        }
    }

    private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is TextBoxBase { IsReadOnly: false, IsKeyboardFocusWithin: false } textBoxBase)
        {
            e.Handled = true;
            textBoxBase.Focus();
        }
    }
}
