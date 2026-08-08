using Avalonia.Controls;
using AmphetamineNet.Native;

namespace AmphetamineNet.Views;

public sealed partial class MainWindow : Window
{
    private bool _allowClose;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void AllowClose() => _allowClose = true;

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        if (_allowClose)
        {
            base.OnClosing(e);
            return;
        }

        // Minimize to tray and remove from the Dock (Accessory)
        e.Cancel = true;
        Hide();
        MacAppActivation.ReturnToAccessory();
    }
}
