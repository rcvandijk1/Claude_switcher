using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ClaudeSwitcher.App;

internal static class PromptDialog
{
    public static string? Show(Window owner, string title, string label, string initialValue)
    {
        var win = new Window
        {
            Title = title,
            Owner = owner,
            Width = 360,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = (Brush)App.Current.Resources["BgDark"],
            Foreground = (Brush)App.Current.Resources["FgMain"],
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = 13,
        };

        var stack = new StackPanel { Margin = new Thickness(16) };
        stack.Children.Add(new TextBlock
        {
            Text = label,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)App.Current.Resources["FgDim"],
            Margin = new Thickness(0, 0, 0, 8)
        });

        var box = new TextBox
        {
            Text = initialValue,
            Background = (Brush)App.Current.Resources["BgMid"],
            Foreground = (Brush)App.Current.Resources["FgMain"],
            BorderThickness = new Thickness(0),
            Padding = new Thickness(8, 6, 8, 6),
            FontSize = 14,
            CaretBrush = (Brush)App.Current.Resources["FgMain"]
        };
        stack.Children.Add(box);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 12, 0, 0)
        };
        var cancel = new Button
        {
            Content = "Cancel",
            Style = (Style)App.Current.Resources["GhostButton"],
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };
        var ok = new Button
        {
            Content = "OK",
            Style = (Style)App.Current.Resources["AccentButton"],
            IsDefault = true
        };
        ok.Click += (_, _) => { win.DialogResult = true; };
        buttons.Children.Add(cancel);
        buttons.Children.Add(ok);
        stack.Children.Add(buttons);

        win.Content = stack;
        box.Focus();
        box.SelectAll();

        return win.ShowDialog() == true ? box.Text : null;
    }
}
