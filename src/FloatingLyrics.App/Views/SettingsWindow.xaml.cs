using System.Windows;

namespace FloatingLyrics.App.Views;

public partial class SettingsWindow : Window
{
    public bool AllowClose { get; set; }
    public SettingsWindow()
    {
        InitializeComponent();
        Closing += (_, e) => { if (!AllowClose) { e.Cancel = true; Hide(); } };
    }
    public void Open() { Show(); if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal; Activate(); }
}
