using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using AmphetamineNet.Native;
using AmphetamineNet.Services;

namespace AmphetamineNet.Views;

/// <summary>
/// Dialog for entering a custom timer duration
/// </summary>
public sealed class CustomDurationWindow : Window
{
    /// <summary>
    /// Minutes input field
    /// </summary>
    private readonly TextBox _input;

    /// <summary>
    /// Accepted custom duration in minutes
    /// </summary>
    /// <value>Minutes chosen by the user, or null when cancelled</value>
    public int? ResultMinutes { get; private set; }

    /// <summary>
    /// Creates the custom duration dialog
    /// </summary>
    /// <param name="initialMinutes">Initial minutes shown in the input</param>
    public CustomDurationWindow(int? initialMinutes)
    {
        Title = Localization.T("custom.title");
        Width = 320;
        Height = 160;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        ShowInTaskbar = false;

        _input = new TextBox
        {
            Text = initialMinutes is > 0 ? initialMinutes.Value.ToString() : "",
            PlaceholderText = "45",
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };

        var ok = new Button
        {
            Content = Localization.T("custom.ok"),
            MinWidth = 88,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        ok.Click += (_, _) => Accept();

        var cancel = new Button
        {
            Content = Localization.T("custom.cancel"),
            MinWidth = 88,
            HorizontalContentAlignment = HorizontalAlignment.Center,
        };
        cancel.Click += (_, _) =>
        {
            ResultMinutes = null;
            Close();
        };

        Content = new StackPanel
        {
            Margin = new Thickness(20),
            Spacing = 12,
            Children =
            {
                new TextBlock
                {
                    Text = Localization.T("custom.prompt"),
                    TextWrapping = TextWrapping.Wrap,
                },
                _input,
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 8,
                    Children = { cancel, ok },
                },
            },
        };

        Opened += (_, _) =>
        {
            _input.Focus();
            _input.SelectAll();
        };

        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                Accept();
                e.Handled = true;
            }
            else if (e.Key == Avalonia.Input.Key.Escape)
            {
                ResultMinutes = null;
                Close();
                e.Handled = true;
            }
        };
    }

    /// <summary>
    /// Accepts the entered minutes and closes the dialog
    /// </summary>
    private void Accept()
    {
        if (!int.TryParse(_input.Text?.Trim(), out var minutes) || minutes <= 0 || minutes > 24 * 60)
            return;

        ResultMinutes = minutes;
        Close();
    }

    /// <summary>
    /// Returns the app to accessory activation on close
    /// </summary>
    /// <param name="e">Window closing event data</param>
    protected override void OnClosing(WindowClosingEventArgs e)
    {
        MacAppActivation.ReturnToAccessory();
        base.OnClosing(e);
    }
}
