using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using FloatingLyrics.App.ViewModels;
using FloatingLyrics.Platform.Overlay;
using Microsoft.Win32;

namespace FloatingLyrics.App.Views;

public partial class OverlayWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private string _previous = "";
    public OverlayWindow(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = _viewModel = viewModel;
        SourceInitialized += (_, _) => { ApplySettings(); OverlayInterop.Place(this, viewModel.Settings.Left, viewModel.Settings.Top); };
        SystemEvents.DisplaySettingsChanged += DisplayChanged;
        Closed += (_, _) => SystemEvents.DisplaySettingsChanged -= DisplayChanged;
    }

    public void ApplySettings()
    {
        Width = _viewModel.OverlayWidth;
        OverlayInterop.Configure(this, _viewModel.Locked, _viewModel.AlwaysOnTop);
        EditToolbar.Visibility = _viewModel.Locked ? Visibility.Collapsed : Visibility.Visible;
        EditSurface.Background = _viewModel.Locked ? Brushes.Transparent : new SolidColorBrush(Color.FromArgb(230, 24, 32, 42));
        EditSurface.BorderBrush = _viewModel.Locked ? Brushes.Transparent : new SolidColorBrush(Color.FromRgb(87, 113, 134));
        EditSurface.BorderThickness = new Thickness(_viewModel.Locked ? 0 : 1);
    }

    public void AnimateLine()
    {
        if (_previous == _viewModel.CurrentLyric) return;
        _previous = _viewModel.CurrentLyric;
        if (SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast)
            LyricText.BeginAnimation(OpacityProperty, new DoubleAnimation(_viewModel.TextOpacity * .35, _viewModel.TextOpacity, TimeSpan.FromMilliseconds(140)) { FillBehavior = FillBehavior.Stop });
    }

    private void DragOverlay(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.Locked || e.OriginalSource is System.Windows.Controls.Button) return;
        try { DragMove(); }
        catch (InvalidOperationException) { return; }
        OverlayInterop.Place(this, OverlayInterop.GetPosition(this).X, OverlayInterop.GetPosition(this).Y);
        var (x, y) = OverlayInterop.GetPosition(this);
        _viewModel.SavePosition(x, y);
    }
    private void LockOverlay(object sender, RoutedEventArgs e) { e.Handled = true; _viewModel.Locked = true; }
    private void DisplayChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        var (x, y) = OverlayInterop.GetPosition(this);
        OverlayInterop.Place(this, x, y);
        (x, y) = OverlayInterop.GetPosition(this);
        _viewModel.SavePosition(x, y);
    });
}
