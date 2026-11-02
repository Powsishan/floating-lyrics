namespace FloatingLyrics.Infrastructure.Settings;

public sealed record AppSettings
{
    public bool OverlayEnabled { get; init; } = true;
    public bool Locked { get; init; } = true;
    public bool AlwaysOnTop { get; init; } = true;
    public bool StartInTray { get; init; }
    public string FontFamily { get; init; } = "Segoe UI";
    public double FontSize { get; init; } = 32;
    public string TextColor { get; init; } = "#FFFFFFFF";
    public double TextOpacity { get; init; } = 1;
    public double OverlayWidth { get; init; } = 900;
    public double? Left { get; init; }
    public double? Top { get; init; }
    public int TimingOffsetMs { get; init; }
    public string? PreferredSourceId { get; init; }
    public string LrcLibClientUrl { get; init; } = "";

    public AppSettings Validate() => this with
    {
        LrcLibClientUrl = (LrcLibClientUrl ?? "").Trim(),
        FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? "Segoe UI" : FontFamily,
        FontSize = double.IsFinite(FontSize) ? Math.Clamp(FontSize, 14, 96) : 32,
        OverlayWidth = double.IsFinite(OverlayWidth) ? Math.Clamp(OverlayWidth, 240, 2400) : 900,
        TextOpacity = double.IsFinite(TextOpacity) ? Math.Clamp(TextOpacity, .2, 1) : 1,
        TimingOffsetMs = Math.Clamp(TimingOffsetMs, -30000, 30000),
        Left = Left is { } x && double.IsFinite(x) ? x : null,
        Top = Top is { } y && double.IsFinite(y) ? y : null,
        TextColor = System.Text.RegularExpressions.Regex.IsMatch(TextColor ?? "", "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$") ? TextColor! : "#FFFFFFFF"
    };
}
