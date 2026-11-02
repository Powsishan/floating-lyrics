using System.Collections.ObjectModel;
using System.Windows.Media;
using FloatingLyrics.Infrastructure.Settings;
using FloatingLyrics.Media.WindowsSessions;

namespace FloatingLyrics.App.ViewModels;

public sealed class SettingsViewModel(AppSettings settings) : ObservableObject
{
    public AppSettings Settings { get; private set; } = settings;
    public event EventHandler? SettingsChanged;
    public event EventHandler? SessionSelectionChanged;
    public IReadOnlyList<string> Fonts { get; } = System.Windows.Media.Fonts.SystemFontFamilies.Select(f => f.Source).OrderBy(f => f).ToArray();
    public ObservableCollection<MediaSessionInfo> Sessions { get; } = [];
    public ObservableCollection<string> SessionNames { get; } = ["Automatic · prefer playing"];
    private int _selectedSession;
    public int SelectedSessionIndex { get => _selectedSession; set { if (_selectedSession == value) return; _selectedSession = value; Notify(); SessionSelectionChanged?.Invoke(this, EventArgs.Empty); } }
    public required RelayCommand RetryCommand { get; init; }
    public required RelayCommand CenterCommand { get; init; }
    public required RelayCommand ExitCommand { get; init; }
    private string _status = "Connecting to Windows media sessions…";
    public string Status { get => _status; set { _status = value; Notify(); } }
    private string _track = "Play a song in a compatible music player";
    public string Track { get => _track; set { _track = value; Notify(); } }
    private string _source = "Windows media sessions";
    public string Source { get => _source; set { _source = value; Notify(); } }
    private string _lyric = "";
    public string CurrentLyric { get => _lyric; set { if (_lyric == value) return; _lyric = value; Notify(); } }
    private string _detail = "";
    public string Detail { get => _detail; set { _detail = value; Notify(); } }
    private string _saveStatus = "Settings save automatically on this device";
    public string SaveStatus { get => _saveStatus; set { _saveStatus = value; Notify(); } }
    private string _colorError = "";
    public string ColorError { get => _colorError; private set { _colorError = value; Notify(); } }

    public bool OverlayEnabled { get => Settings.OverlayEnabled; set => Change(Settings with { OverlayEnabled = value }); }
    public bool Locked { get => Settings.Locked; set => Change(Settings with { Locked = value }); }
    public bool AlwaysOnTop { get => Settings.AlwaysOnTop; set => Change(Settings with { AlwaysOnTop = value }); }
    public bool StartInTray { get => Settings.StartInTray; set => Change(Settings with { StartInTray = value }); }
    public string FontFamily { get => Settings.FontFamily; set => Change(Settings with { FontFamily = value }); }
    public double FontSize { get => Settings.FontSize; set => Change(Settings with { FontSize = value }); }
    public double OverlayWidth { get => Settings.OverlayWidth; set => Change(Settings with { OverlayWidth = value }); }
    public double TextOpacity { get => Settings.TextOpacity; set => Change(Settings with { TextOpacity = value }); }
    public string TextColor
    {
        get => Settings.TextColor;
        set
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(value ?? "", "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) { ColorError = "Use #RRGGBB or #AARRGGBB"; return; }
            ColorError = "";
            Change(Settings with { TextColor = value! });
            Notify(nameof(LyricBrush));
        }
    }
    public SolidColorBrush LyricBrush => new((Color)ColorConverter.ConvertFromString(Settings.TextColor));
    public int TimingOffsetMs { get => Settings.TimingOffsetMs; set => Change(Settings with { TimingOffsetMs = value }); }
    public string LrcLibClientUrl { get => Settings.LrcLibClientUrl; set => Change(Settings with { LrcLibClientUrl = value.Trim() }); }
    public void SavePosition(double x, double y) => Change(Settings with { Left = x, Top = y });
    public void PreferSource(string? source) => Change(Settings with { PreferredSourceId = source });

    private void Change(AppSettings next, [System.Runtime.CompilerServices.CallerMemberName] string? property = null)
    {
        if (next == Settings) return;
        Settings = next.Validate();
        Notify(property);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }
}
